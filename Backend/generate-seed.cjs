const fs=require('fs'),crypto=require('crypto');
let sql='begin;\n';const values=[];
for(const key of ['Sara','Jack','Dinner']){
 const file='Assets/Resources/CampaignFive/'+key+'.json',text=fs.readFileSync(file,'utf8').replace(/\r\n/g,'\n'),c=JSON.parse(text);
 const hash=crypto.createHash('sha256').update(text).digest('hex'),heads=c.notes.length,judgments=heads+c.notes.filter(n=>(n.duration??0)>0).length;
 const last=Math.max(...c.notes.map(n=>n.timestamp+(n.duration??0))),duration=key==='Sara'?155:key==='Jack'?107:129,maxScore=judgments*1000+5*judgments*(judgments+1)+100000;
 values.push(`('${c.chartId}','${hash}',${heads},${judgments},${maxScore},${last},${duration})`);
}
sql+='insert into rw_private.campaigns(chart_id,revision,heads,judgments,max_score,last_tail,duration) values\n'+values.join(',\n')+'\non conflict(chart_id,revision) do update set heads=excluded.heads,judgments=excluded.judgments,max_score=excluded.max_score,last_tail=excluded.last_tail,duration=excluded.duration;\n';
const bank=fs.readFileSync('Assets/Scripts/Education/CyberQuestionBank.cs','utf8');const questions=[...bank.matchAll(/Add\("([^"]+)","([^"]+)","([^"]+)"/g)];
if(questions.length!==20)throw Error('Question source changed; review the seed generator');
sql+='insert into rw_private.question_keys values\n'+questions.map((q,i)=>`('v2-${q[1]}','${q[3]}',2,${i%3})`).join(',\n')+'\non conflict(question_id) do update set objective_id=excluded.objective_id,content_version=excluded.content_version,correct_option=excluded.correct_option;\ncommit;\n';
fs.writeFileSync('Backend/supabase/migrations/202611150002_campaign_and_questions.sql',sql);console.log('Generated server definitions for 3 charts and 20 SOC questions.');
