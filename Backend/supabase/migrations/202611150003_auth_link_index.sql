begin;
create index if not exists rw_auth_links_participant on rw_private.auth_links(participant_id);
commit;
