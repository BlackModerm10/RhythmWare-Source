-- Execute ONLY from Supabase SQL Editor as the project owner.
-- Export each result separately with the Dashboard CSV download.
-- No authentication IDs or recovery hashes are included.

-- 1. Complete attempt history (all outcomes), linked by a private participant code.
select p.id as participant_code,p.alias||'#'||p.tag as public_alias,
 row_number() over(partition by a.participant_id,a.chart_id,a.revision order by a.received_at,a.event_id) as attempt_number,
 a.chart_id,a.revision,a.outcome,a.score,a.accuracy,a.perfect,a.good,a.miss,a.max_combo,
 a.elapsed_seconds,a.played_seconds,a.note_speed,a.offset_ms,a.has_final_stats,
 a.game_version,a.session_id,a.started_utc,a.ended_utc,a.received_at
from rw_private.game_attempts a join rw_private.participants p on p.id=a.participant_id
order by a.received_at,a.event_id;

-- 2. Detailed SOC answers. Context separates academy, practice, rescues and debriefs.
select p.id as participant_code,p.alias||'#'||p.tag as public_alias,
 s.question_id,s.objective_id,s.content_version,s.selected_option,s.correct,
 s.first_attempt,s.prior_local_attempts,s.source,s.game_version,s.session_id,s.client_utc,s.received_at
from rw_private.soc_answers s join rw_private.participants p on p.id=s.participant_id
order by s.received_at,s.event_id;

-- 3. Participant summary. Rhythm accuracy is separate from learning responses.
select p.id as participant_code,p.alias||'#'||p.tag as public_alias,
 (select count(*) from rw_private.game_attempts a where a.participant_id=p.id) as attempts,
 (select count(*) from rw_private.game_attempts a where a.participant_id=p.id and a.outcome='victory') as victories,
 (select count(*) from rw_private.soc_answers s where s.participant_id=p.id and s.source='academy') as academy_responses,
 (select count(*) from rw_private.soc_answers s where s.participant_id=p.id and s.source='academy' and s.correct) as academy_correct
from rw_private.participants p order by p.created_at;
