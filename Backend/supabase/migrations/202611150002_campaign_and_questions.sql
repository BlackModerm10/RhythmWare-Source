begin;
insert into rw_private.campaigns(chart_id,revision,heads,judgments,max_score,last_tail,duration) values
('rw-campaign-sara-full-guided-5keys-v6','a31b45a6cdc3ac012357d5d6f88368ca37f5d29399816af4f766c6df0e083bf2',201,209,528450,151.550866,155),
('rw-campaign-chroma-atat-5keys-osu-5012369-v1','a93077f3aeb49bce774d58a3441aaba011c962ff4b0c0721fdf6c8eaf7220cbf',308,341,1024110,100.704,107),
('rw-campaign-bonzi-dragon-5k-hyper-lein-328379-v1','be015c5ebc62c9e413ce13f41b2ffaabed6f02f3c5af749cd09de3d18a3a1103',898,898,5034510,119.422,129)
on conflict(chart_id,revision) do update set heads=excluded.heads,judgments=excluded.judgments,max_score=excluded.max_score,last_tail=excluded.last_tail,duration=excluded.duration;
insert into rw_private.question_keys values
('v2-spy-1','spy-identify',2,0),
('v2-spy-2','spy-prevent',2,1),
('v2-spy-3','spy-contain',2,2),
('v2-spy-4','spy-recover',2,0),
('v2-ad-1','ad-identify',2,1),
('v2-ad-2','ad-install',2,2),
('v2-ad-3','ad-source',2,0),
('v2-ad-4','ad-compare',2,1),
('v2-trojan-1','trojan-disguise',2,2),
('v2-trojan-2','trojan-vector',2,0),
('v2-trojan-3','trojan-usb',2,1),
('v2-trojan-4','trojan-response',2,2),
('v2-ransom-1','ransom-identify',2,0),
('v2-ransom-2','ransom-contain',2,1),
('v2-ransom-3','ransom-backup',2,2),
('v2-ransom-4','ransom-recover',2,0),
('v2-ddos-1','ddos-identify',2,1),
('v2-ddos-2','ddos-detect',2,2),
('v2-ddos-3','ddos-mitigate',2,0),
('v2-ddos-4','ddos-compare',2,1)
on conflict(question_id) do update set objective_id=excluded.objective_id,content_version=excluded.content_version,correct_option=excluded.correct_option;
commit;
