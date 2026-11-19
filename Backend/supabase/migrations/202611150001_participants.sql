begin;

create schema if not exists rw_private;
revoke all on schema rw_private from public, anon, authenticated;

create table if not exists rw_private.participants (
 id uuid primary key default gen_random_uuid(), alias text not null, tag text not null,
 recovery_hash text not null unique, created_at timestamptz not null default now()
);
create unique index if not exists rw_participant_alias_tag on rw_private.participants(lower(alias),tag);
create table if not exists rw_private.auth_links (
 auth_id uuid primary key references auth.users(id) on delete cascade,
 participant_id uuid not null references rw_private.participants(id)
);
create table if not exists rw_private.campaigns (
 chart_id text not null, revision text not null, heads integer not null, judgments integer not null,
 max_score integer not null, last_tail double precision not null, duration double precision not null, primary key(chart_id,revision)
);
create table if not exists rw_private.question_keys (
 question_id text primary key, objective_id text not null, content_version integer not null,
 correct_option integer not null check(correct_option between 0 and 2)
);
create table if not exists rw_private.game_attempts (
 participant_id uuid not null references rw_private.participants(id), event_id uuid not null,
 session_id uuid not null, chart_id text not null, revision text not null,
 outcome text not null, game_version text not null, score integer not null,
 perfect integer not null, good integer not null, miss integer not null, max_combo integer not null,
 accuracy real not null, started_utc timestamptz not null, ended_utc timestamptz not null,
 elapsed_seconds double precision not null, played_seconds double precision not null,
 note_speed real not null, offset_ms real not null, has_final_stats boolean not null,
 received_at timestamptz not null default now(), primary key(participant_id,event_id)
);
create table if not exists rw_private.soc_answers (
 participant_id uuid not null references rw_private.participants(id), event_id uuid not null,
 session_id uuid not null, question_id text not null, objective_id text not null,
 content_version integer not null, selected_option integer not null, correct boolean not null,
 first_attempt boolean not null, source text not null, client_utc timestamptz not null, prior_local_attempts integer not null default 0,
 game_version text not null, received_at timestamptz not null default now(), primary key(participant_id,event_id)
);
create index if not exists rw_soc_question_history on rw_private.soc_answers(participant_id,question_id,content_version,received_at);
create table if not exists public.rw_leaderboard (
 entry_id uuid primary key default gen_random_uuid(), chart_id text not null, chart_revision text not null,
 player_name text not null, score integer not null, accuracy real not null,
 max_combo integer not null, rank text not null, achieved_at timestamptz not null default now()
);
create table if not exists rw_private.public_entries (
 participant_id uuid not null references rw_private.participants(id), chart_id text not null, revision text not null,
 entry_id uuid not null unique references public.rw_leaderboard(entry_id), primary key(participant_id,chart_id,revision)
);

alter table rw_private.participants enable row level security;
alter table rw_private.auth_links enable row level security;
alter table rw_private.campaigns enable row level security;
alter table rw_private.question_keys enable row level security;
alter table rw_private.game_attempts enable row level security;
alter table rw_private.soc_answers enable row level security;
alter table rw_private.public_entries enable row level security;
alter table public.rw_leaderboard enable row level security;
revoke all on all tables in schema rw_private from public,anon,authenticated;
revoke all on public.rw_leaderboard from public,anon,authenticated;
grant select on public.rw_leaderboard to anon,authenticated;
drop policy if exists rw_public_read on public.rw_leaderboard;
create policy rw_public_read on public.rw_leaderboard for select to anon,authenticated using(true);

create or replace function rw_private.require_participant() returns uuid language plpgsql set search_path='' as $$
declare p uuid;
begin
 if auth.uid() is null then raise exception 'Authentication required'; end if;
 select participant_id into p from rw_private.auth_links where auth_id=auth.uid();
 if p is null then raise exception 'Profile required'; end if;
 return p;
end $$;
create or replace function rw_private.valid_alias(a text) returns boolean language sql immutable set search_path='' as $$
 select a is not null and a=btrim(a) and a ~ '^[A-Za-z0-9ÁÉÍÓÚÜÑáéíóúüñ][A-Za-z0-9ÁÉÍÓÚÜÑáéíóúüñ _.-]{2,19}$';
$$;
create or replace function rw_private.profile_json(p uuid) returns jsonb language sql stable set search_path='' as $$
 select jsonb_build_object('participantId',id,'alias',alias,'tag',tag) from rw_private.participants where id=p;
$$;
create or replace function public.rw_register_profile(p_alias text,p_recovery_code text) returns jsonb
language plpgsql security definer set search_path='' as $$
declare p uuid; t text; tries int;
begin
 if auth.uid() is null then raise exception 'Authentication required'; end if;
 -- Serialize register retries for one authenticated identity.
 perform pg_advisory_xact_lock(hashtextextended(auth.uid()::text,0));
 select participant_id into p from rw_private.auth_links where auth_id=auth.uid();
 if p is not null then return rw_private.profile_json(p); end if;
 if not rw_private.valid_alias(p_alias) or p_recovery_code is null or p_recovery_code !~ '^RW-[A-Za-z0-9_-]{43}$' then raise exception 'Invalid profile'; end if;
 for tries in 1..20 loop
  t:=lpad(floor(random()*10000)::integer::text,4,'0');
  begin
   insert into rw_private.participants(alias,tag,recovery_hash)
    values(p_alias,t,encode(sha256(convert_to(p_recovery_code,'UTF8')),'hex')) returning id into p;
   exit;
  exception when unique_violation then if tries=20 then raise exception 'Profile unavailable'; end if;
  end;
 end loop;
 insert into rw_private.auth_links values(auth.uid(),p);
 return rw_private.profile_json(p);
end $$;

create or replace function public.rw_restore_profile(p_recovery_code text) returns jsonb
language plpgsql security definer set search_path='' as $$
declare p uuid; old_p uuid;
begin
 if auth.uid() is null then raise exception 'Authentication required'; end if;
 if p_recovery_code is null or p_recovery_code !~ '^RW-[A-Za-z0-9_-]{43}$' then raise exception 'Recovery failed'; end if;
 perform pg_advisory_xact_lock(hashtextextended(auth.uid()::text,0));
 select id into p from rw_private.participants where recovery_hash=encode(sha256(convert_to(p_recovery_code,'UTF8')),'hex') for update;
 if p is null then raise exception 'Recovery failed'; end if;
 select participant_id into old_p from rw_private.auth_links where auth_id=auth.uid();
 if old_p is not null and old_p<>p then raise exception 'Use a new session for recovery'; end if;
 insert into rw_private.auth_links values(auth.uid(),p) on conflict(auth_id) do nothing;
 return rw_private.profile_json(p);
end $$;

create or replace function public.rw_set_alias(p_alias text) returns jsonb
language plpgsql security definer set search_path='' as $$
declare p uuid:=rw_private.require_participant();t text;tries int;
begin
 if not rw_private.valid_alias(p_alias) then raise exception 'Invalid alias'; end if;
 perform 1 from rw_private.participants where id=p for update;
 for tries in 1..20 loop
  begin
   update rw_private.participants set alias=p_alias,tag=coalesce(t,tag) where id=p;exit;
  exception when unique_violation then
   t:=lpad(floor(random()*10000)::integer::text,4,'0');
   if tries=20 then raise exception 'Alias unavailable'; end if;
  end;
 end loop;
 update public.rw_leaderboard l set player_name=x.alias||'#'||x.tag
  from rw_private.public_entries e,rw_private.participants x where e.participant_id=p and x.id=p and l.entry_id=e.entry_id;
 return rw_private.profile_json(p);
end $$;

create or replace function public.rw_submit_attempt(p_event jsonb) returns jsonb
language plpgsql security definer set search_path='' as $$
declare p uuid:=rw_private.require_participant();c rw_private.campaigns;ev uuid;total int;s int;perfect int;good int;miss int;combo int;
 acc real;outcome text;entry uuid;old_score int;old_acc real;old_combo int;grade text;inserted int;
begin
 if jsonb_typeof(p_event)<>'object' or octet_length(p_event::text)>16384 then raise exception 'Invalid event'; end if;
 if not (p_event ?& array['eventId','sessionId','chartId','chartRevision','gameVersion','outcome','startedUtc','endedUtc','elapsedSeconds','playedSeconds','score','perfect','good','miss','maxCombo','noteSpeed','offsetMs','hasFinalStats']) then raise exception 'Incomplete event'; end if;
 ev:=(p_event->>'eventId')::uuid;
 perform 1 from rw_private.participants where id=p for update;
 if exists(select 1 from rw_private.game_attempts where participant_id=p and event_id=ev) then return jsonb_build_object('accepted',true,'duplicate',true); end if;
 select * into c from rw_private.campaigns where chart_id=p_event->>'chartId' and revision=p_event->>'chartRevision';
 if c.chart_id is null then raise exception 'Unknown map version'; end if;
 s:=(p_event->>'score')::int;perfect:=(p_event->>'perfect')::int;good:=(p_event->>'good')::int;miss:=(p_event->>'miss')::int;combo:=(p_event->>'maxCombo')::int;total:=perfect+good+miss;outcome:=p_event->>'outcome';
 if s<0 or s>c.max_score or least(perfect,good,miss,combo)<0 or total>c.judgments or combo>perfect+good then raise exception 'Impossible statistics'; end if;
 if (p_event->>'noteSpeed')::real not between .5 and 8 or (p_event->>'offsetMs')::real not between -500 and 500 or (p_event->>'playedSeconds')::double precision not between 0 and c.duration+1 or (p_event->>'elapsedSeconds')::double precision not between 0 and 100000000 then raise exception 'Invalid timing'; end if;
 if outcome not in ('victory','defeat','abandoned','interrupted') or p_event->>'gameVersion' !~ '^[A-Za-z0-9][A-Za-z0-9._+-]{0,31}$' then raise exception 'Invalid outcome'; end if;
 if outcome='victory' and (total<c.heads or not (p_event->>'hasFinalStats')::boolean or (p_event->>'playedSeconds')::double precision<c.last_tail-.3 or (p_event->>'elapsedSeconds')::double precision<c.last_tail-.5) then raise exception 'Incomplete victory'; end if;
 if (select count(*) from rw_private.game_attempts where participant_id=p and received_at>now()-interval '1 hour')>=600 then raise exception 'Rate limited'; end if;
 acc:=case when total=0 then 100 else (perfect*100.0+good*50.0)/total end;
 insert into rw_private.game_attempts(participant_id,event_id,session_id,chart_id,revision,outcome,game_version,score,perfect,good,miss,max_combo,accuracy,started_utc,ended_utc,elapsed_seconds,played_seconds,note_speed,offset_ms,has_final_stats)
 values(p,ev,(p_event->>'sessionId')::uuid,c.chart_id,c.revision,outcome,p_event->>'gameVersion',s,perfect,good,miss,combo,acc,(p_event->>'startedUtc')::timestamptz,(p_event->>'endedUtc')::timestamptz,(p_event->>'elapsedSeconds')::double precision,(p_event->>'playedSeconds')::double precision,(p_event->>'noteSpeed')::real,(p_event->>'offsetMs')::real,(p_event->>'hasFinalStats')::boolean);
 if outcome<>'victory' then return jsonb_build_object('accepted',true); end if;
 select e.entry_id into entry from rw_private.public_entries e where participant_id=p and chart_id=c.chart_id and revision=c.revision;
 grade:=case when acc>=99.5 and miss=0 then 'S+' when acc>=95 and miss=0 then 'S' when acc>=90 then 'A' when acc>=80 then 'B' when acc>=70 then 'C' else 'F' end;
 if entry is null then
  insert into public.rw_leaderboard(chart_id,chart_revision,player_name,score,accuracy,max_combo,rank)
   select c.chart_id,c.revision,alias||'#'||tag,s,acc,combo,grade from rw_private.participants where id=p returning entry_id into entry;
  insert into rw_private.public_entries values(p,c.chart_id,c.revision,entry);
 else
  select score,accuracy,max_combo into old_score,old_acc,old_combo from public.rw_leaderboard where entry_id=entry;
  if (s,acc,combo)>(old_score,old_acc,old_combo) then update public.rw_leaderboard set score=s,accuracy=acc,max_combo=combo,rank=grade,achieved_at=now() where entry_id=entry;end if;
 end if;
 return jsonb_build_object('accepted',true);
end $$;

create or replace function public.rw_submit_soc(p_event jsonb) returns jsonb
language plpgsql security definer set search_path='' as $$
declare p uuid:=rw_private.require_participant();q rw_private.question_keys;ev uuid;choice int;first_answer boolean;
begin
 if jsonb_typeof(p_event)<>'object' or octet_length(p_event::text)>8192 or not (p_event ?& array['eventId','sessionId','questionId','objectiveId','contentVersion','selectedOption','source','utc','gameVersion']) then raise exception 'Invalid answer'; end if;
 ev:=(p_event->>'eventId')::uuid;
 perform 1 from rw_private.participants where id=p for update;
 if exists(select 1 from rw_private.soc_answers where participant_id=p and event_id=ev) then return jsonb_build_object('accepted',true,'duplicate',true); end if;
 select * into q from rw_private.question_keys where question_id=p_event->>'questionId' and content_version=(p_event->>'contentVersion')::int;
 choice:=(p_event->>'selectedOption')::int;
 if q.question_id is null or q.objective_id<>p_event->>'objectiveId' or choice not between 0 and 2 or p_event->>'source' not in ('academy','rescue','tutorial-rescue','defeat','debrief') or p_event->>'gameVersion' !~ '^[A-Za-z0-9][A-Za-z0-9._+-]{0,31}$' then raise exception 'Invalid answer'; end if;
 if (select count(*) from rw_private.soc_answers where participant_id=p and received_at>now()-interval '1 hour')>=3600 then raise exception 'Rate limited'; end if;
 if coalesce((p_event->>'priorLocalAttempts')::integer,0) not between 0 and 1000000 then raise exception 'Invalid local history'; end if;
 first_answer:=not exists(select 1 from rw_private.soc_answers where participant_id=p and question_id=q.question_id and content_version=q.content_version) and coalesce((p_event->>'priorLocalAttempts')::integer,0)=0;
 insert into rw_private.soc_answers(participant_id,event_id,session_id,question_id,objective_id,content_version,selected_option,correct,first_attempt,source,client_utc,game_version,prior_local_attempts)
 values(p,ev,(p_event->>'sessionId')::uuid,q.question_id,q.objective_id,q.content_version,choice,choice=q.correct_option,first_answer,p_event->>'source',(p_event->>'utc')::timestamptz,p_event->>'gameVersion',coalesce((p_event->>'priorLocalAttempts')::integer,0));
 return jsonb_build_object('accepted',true);
end $$;

create or replace function public.rw_get_board(p_chart_id text,p_revision text) returns jsonb
language plpgsql security definer set search_path='' as $$
declare result jsonb;own_entry uuid;
begin
 select e.entry_id into own_entry from rw_private.public_entries e join rw_private.auth_links a on a.participant_id=e.participant_id where a.auth_id=auth.uid() and e.chart_id=p_chart_id and e.revision=p_revision;
 with ranked as (
  select l.*,row_number() over(order by score desc,accuracy desc,max_combo desc,achieved_at,entry_id)::int place from public.rw_leaderboard l where chart_id=p_chart_id and chart_revision=p_revision
 ), formatted as (
  select entry_id,place,jsonb_build_object('playerName',player_name,'chartId',chart_id,'score',score,'accuracy',accuracy,'maxCombo',max_combo,'rank',rank,'timestamp',achieved_at,'place',place) value from ranked
 ) select jsonb_build_object('entries',coalesce((select jsonb_agg(value order by place) from formatted where place<=50),'[]'::jsonb),'own',(select value from formatted where entry_id=own_entry)) into result;
 return result;
end $$;

create or replace function public.rw_my_soc_progress() returns jsonb
language plpgsql security definer set search_path='' as $$
declare p uuid:=rw_private.require_participant();result jsonb;
begin
 -- First and latest answer per question are sufficient to restore the learning UI.
 with ranked as (
  select s.*,row_number() over(partition by question_id,content_version order by received_at,event_id) first_row,row_number() over(partition by question_id,content_version order by received_at desc,event_id desc) last_row from rw_private.soc_answers s where participant_id=p
 ) select jsonb_build_object('attempts',coalesce(jsonb_agg(jsonb_build_object('attemptId',replace(event_id::text,'-',''),'questionId',question_id,'objectiveId',objective_id,'source',source,'utc',client_utc,'selectedOption',selected_option,'contentVersion',content_version,'correct',correct,'firstAttempt',first_attempt) order by received_at,event_id),'[]'::jsonb)) into result from ranked where first_row=1 or last_row=1;
 return result;
end $$;

revoke all on all functions in schema rw_private from public,anon,authenticated;
revoke all on function public.rw_register_profile(text,text),public.rw_restore_profile(text),public.rw_set_alias(text),public.rw_submit_attempt(jsonb),public.rw_submit_soc(jsonb),public.rw_get_board(text,text),public.rw_my_soc_progress() from public,anon,authenticated;
grant execute on function public.rw_register_profile(text,text),public.rw_restore_profile(text),public.rw_set_alias(text),public.rw_submit_attempt(jsonb),public.rw_submit_soc(jsonb),public.rw_my_soc_progress() to authenticated;
grant execute on function public.rw_get_board(text,text) to anon,authenticated;
commit;
