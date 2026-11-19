const fs=require('fs'),crypto=require('crypto');
const path=require('path');process.chdir(path.resolve(__dirname,'../..'));
const {PGlite}=require('@electric-sql/pglite');
const results=[];function check(ok,label){results.push((ok?'PASS: ':'FAIL: ')+label);if(!ok)throw Error(label);}
const db=new PGlite();const users=['00000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000002','00000000-0000-0000-0000-000000000003'];
const codeA='RW-'+('A'.repeat(43)),codeB='RW-'+('B'.repeat(43));
async function query(sql,args=[]){return (await db.query(sql,args)).rows;}
async function as(user,fn,role='authenticated'){await db.exec('set role '+role);await query("select set_config('request.jwt.claim.sub',$1,false)",[user??'']);try{return await fn();}finally{await db.exec('reset role');}}
async function reject(fn,label){let failed=false;try{await fn();}catch{failed=true;}check(failed,label);}
async function rpc(name,params=[],uid=users[0]){return as(uid,async()=>{const slots=params.map((_,i)=>'$'+(i+1)).join(',');return (await query('select public.'+name+'('+slots+') as result',params.map(x=>typeof x==='object'?JSON.stringify(x):x)))[0].result;});}
const id=()=>crypto.randomUUID();
async function run(){
 await db.exec("create role anon nologin;create role authenticated nologin;create schema auth;create table auth.users(id uuid primary key);create function auth.uid() returns uuid language sql stable as $$select nullif(current_setting('request.jwt.claim.sub',true),'')::uuid$$;grant usage on schema auth to anon,authenticated;");
 for(const user of users)await query('insert into auth.users values($1)',[user]);
 for(const p of fs.readdirSync('Backend/supabase/migrations').sort())await db.exec(fs.readFileSync('Backend/supabase/migrations/'+p,'utf8'));
 check((await query('select count(*)::int n from rw_private.campaigns'))[0].n===3,'Three exact campaign revisions are seeded');
 check((await query('select count(*)::int n from rw_private.question_keys'))[0].n===20,'Twenty server-side SOC answer keys are seeded');
 const a=await rpc('rw_register_profile',['NeonFox',codeA]),b=await rpc('rw_register_profile',['NeonFox',codeB],users[1]);
 check(a.participantId!==b.participantId&&a.tag!==b.tag,'Duplicate aliases receive distinct identities and visible tags');
 check((await rpc('rw_register_profile',['DifferentName',codeA])).participantId===a.participantId,'Registration retry preserves identity');
 await reject(()=>rpc('rw_register_profile',['User',codeA],null),'Unauthenticated profile creation is rejected');
 await reject(()=>rpc('rw_register_profile',['<b>bad</b>',codeA],users[2]),'Aliases cannot inject rich text or formulas');
 await reject(()=>as(users[1],()=>query('select * from rw_private.soc_answers')),'Another player cannot query raw SOC answers');
 await reject(()=>as(users[0],()=>query('select * from rw_private.game_attempts')),'The client cannot query the private attempt table directly');
 await reject(()=>as(users[0],()=>query('select * from rw_private.participants')),'Recovery hashes and identity mapping are not readable by clients');
 await reject(()=>as(users[0],()=>query('select * from rw_private.question_keys')),'Private answer keys are not readable through the Data API');
 await reject(()=>as(users[0],()=>query('select rw_private.profile_json($1)',[b.participantId])),'Private helper functions are not callable by players');
 await reject(()=>as(null,()=>query("insert into public.rw_leaderboard(chart_id,chart_revision,player_name,score,accuracy,max_combo,rank) values('x','x','spoof',999,100,1,'S+')"),'anon'),'Anonymous users cannot insert a leaderboard row');
 await reject(()=>as(users[0],()=>query("delete from public.rw_leaderboard")),'Authenticated users cannot directly delete scores');
 const campaign=(await query('select * from rw_private.campaigns order by heads limit 1'))[0];
 function attempt(extra={}){return {eventId:id(),sessionId:id(),chartId:campaign.chart_id,chartRevision:campaign.revision,gameVersion:'0.1.0-beta.1',outcome:'victory',startedUtc:'2026-11-15T10:00:00Z',endedUtc:'2026-11-15T10:03:00Z',elapsedSeconds:180,playedSeconds:154.9,score:420000,perfect:209,good:0,miss:0,maxCombo:209,noteSpeed:1.5,offsetMs:0,hasFinalStats:true,...extra};}
 const lost=attempt({outcome:'defeat',playedSeconds:20,score:1000,perfect:1,miss:3,maxCombo:1});
 await rpc('rw_submit_attempt',[lost]);await rpc('rw_submit_attempt',[lost]);
 check((await query('select count(*)::int n from rw_private.game_attempts'))[0].n===1,'Retrying a defeat upload is idempotent');
 check((await as(null,()=>query('select count(*)::int n from public.rw_leaderboard'),'anon'))[0].n===0,'Defeats remain private and do not create public rankings');
 const win=attempt();await rpc('rw_submit_attempt',[win]);await rpc('rw_submit_attempt',[attempt({score:400000})]);
 let board=await rpc('rw_get_board',[campaign.chart_id,campaign.revision]);
 check(board.entries.length===1&&board.entries[0].score===420000&&board.own.place===1,'Only the best completed run per participant is public');
 check(board.entries[0].rank==='S+'&&board.entries[0].accuracy===100,'Public accuracy and rank are calculated by the server');
 await rpc('rw_submit_attempt',[attempt({score:410000,participantId:a.participantId})],users[1]);
 board=await rpc('rw_get_board',[campaign.chart_id,campaign.revision],users[1]);
 check(board.entries.length===2&&board.own.place===2,'Players share a global ranking and can see their own position');
 check((await query('select count(*)::int n from rw_private.game_attempts where participant_id=$1',[b.participantId]))[0].n===1,'A spoofed participantId cannot write into another participant history');
 check(!JSON.stringify(board).includes(a.participantId)&&!JSON.stringify(board).includes(users[0])&&!JSON.stringify(board).includes('selectedOption')&&!JSON.stringify(board).includes(codeA),'Public DTOs contain no authentication IDs, recovery codes or SOC data');
 await reject(()=>rpc('rw_submit_attempt',[attempt({score:campaign.max_score+1})]),'Impossible scores are rejected');
 await reject(()=>rpc('rw_submit_attempt',[attempt({chartRevision:'old-version'})]),'Incompatible map versions cannot enter the same ranking');
 await reject(()=>rpc('rw_submit_attempt',[attempt({perfect:1,maxCombo:1})]),'Incomplete victories are rejected');
 await reject(()=>rpc('rw_submit_attempt',[attempt({noteSpeed:9})]),'Invalid settings are rejected');
 await reject(()=>rpc('rw_submit_attempt',[attempt({playedSeconds:1000})]),'Impossible song position is rejected');
 await reject(()=>rpc('rw_submit_attempt',[attempt({elapsedSeconds:2})]),'A full song cannot be completed in two seconds');
 await reject(()=>rpc('rw_submit_attempt',[{...attempt(),large:'x'.repeat(17000)}]),'Oversized submissions are rejected');
 await reject(()=>rpc('rw_submit_attempt',[attempt({eventId:null})]),'Null idempotency IDs do not bypass validation');
 const q=(await query('select * from rw_private.question_keys order by question_id limit 1'))[0];
 const answer={eventId:id(),sessionId:id(),questionId:q.question_id,objectiveId:q.objective_id,contentVersion:q.content_version,selectedOption:(q.correct_option+1)%3,source:'academy',utc:'2026-11-15T10:05:00Z',gameVersion:'0.1.0-beta.1',correct:true};
 await rpc('rw_submit_soc',[answer]);await rpc('rw_submit_soc',[answer]);
 let privateAnswers=await query('select * from rw_private.soc_answers');
 check(privateAnswers.length===1&&!privateAnswers[0].correct&&privateAnswers[0].first_attempt,'SOC correctness is server-derived and duplicate retries are ignored');
 await rpc('rw_submit_soc',[{...answer,eventId:id(),selectedOption:q.correct_option}]);
 privateAnswers=await query('select * from rw_private.soc_answers order by received_at,event_id');
 check(privateAnswers.length===2&&privateAnswers.filter(x=>x.first_attempt).length===1,'First-attempt learning metrics survive repeated answers');
 check((await rpc('rw_my_soc_progress',[],users[1])).attempts.length===0,'A player cannot recover someone else’s SOC progress');
 await rpc('rw_submit_soc',[{...answer,eventId:id(),priorLocalAttempts:4}],users[1]);
 check((await query('select first_attempt,prior_local_attempts from rw_private.soc_answers where participant_id=$1',[b.participantId]))[0].first_attempt===false,'Migrated local exposure is not mislabeled as a first attempt');
 check((await rpc('rw_my_soc_progress')).attempts.length===2,'An authenticated owner can restore first/latest learning responses');
 await reject(()=>rpc('rw_submit_soc',[{...answer,eventId:id(),selectedOption:9}]),'Out-of-range answer choices are rejected');
 await reject(()=>rpc('rw_submit_soc',[{...answer,eventId:id(),contentVersion:999}]),'Unknown question revisions are rejected');
 await reject(()=>rpc('rw_submit_soc',[{...answer,eventId:id(),source:'leaderboard'}]),'Unknown learning contexts are rejected');
 await reject(()=>rpc('rw_restore_profile',['NeonFox'],users[2]),'Knowing a public alias cannot recover an identity');
 await reject(()=>rpc('rw_restore_profile',['RW-'+'Z'.repeat(43)],users[2]),'An incorrect recovery code is rejected');
 await reject(()=>rpc('rw_restore_profile',[codeA],users[1]),'Recovery cannot reassign an existing different participant session');
 const recovered=await rpc('rw_restore_profile',[codeA],users[2]);
 check(recovered.participantId===a.participantId,'A valid secret recovers the same identity without email');
 check((await rpc('rw_my_soc_progress',[],users[2])).attempts.length===2,'Recovered access restores only that participant learning progress');
 await query('update rw_private.participants set alias=$1,tag=$2 where id=$3',['Occupied',a.tag,b.participantId]);
 const renamedCollision=await rpc('rw_set_alias',['Occupied'],users[2]);check(renamedCollision.tag!==a.tag,'Renaming resolves a tag collision atomically without losing identity');
 await rpc('rw_set_alias',['RenamedFox'],users[2]);
 board=await rpc('rw_get_board',[campaign.chart_id,campaign.revision],users[2]);
 check(board.own.place===1&&board.own.playerName.startsWith('RenamedFox#'),'Renaming preserves rank and updates the public alias');
 const columns=(await query("select column_name from information_schema.columns where table_schema='public' and table_name='rw_leaderboard'")).map(x=>x.column_name);
 check(!columns.some(c=>/participant|auth|email|question|attempt|recovery|session/.test(c)),'The public table physically contains only public fields');
 const superuserRows=(await query('select count(*)::int n from rw_private.game_attempts'))[0].n;
 check(superuserRows===4,'The owner can export all winning and losing attempts, beyond leaderboard best scores');
 fs.writeFileSync(process.env.RW_DB_REPORT||'Backend/tests/report.txt','PASS\n'+results.join('\n')+'\n');console.log(JSON.stringify({status:'PASS',checks:results.length,privateAttempts:superuserRows},null,2));
}
run().catch(error=>{fs.writeFileSync(process.env.RW_DB_REPORT||'Backend/tests/report.txt','FAIL\n'+results.join('\n')+'\n'+error.stack);console.error(error.message);process.exitCode=1;}).finally(()=>db.close());
