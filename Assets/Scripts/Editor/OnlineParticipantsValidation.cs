#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using RhythmWare.Core;
using RhythmWare.Rhythm;
using RhythmWare.Education;
using RhythmWare.UI;

namespace RhythmWare.EditorTools
{
    public static class OnlineParticipantsValidation
    {
        static int step,polls;static string root,legacy,code;static ParticipantProfile a,b;static FakeTransport fake;static ChartData chart;static bool recovered;static string recoveryMessage;
        public static bool Run(Action<bool,string> check,Action<string> capture,Action<int,int> size)
        {
            var service=OnlineService.Instance;
            switch(step)
            {
                case 0:
                    root=Path.Combine(Path.GetDirectoryName(Application.dataPath),"docs","participants-validation","profiles-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);ProfileStorage.SetValidationRoot(root);
                    check(ParticipantProfiles.Current==null&&PlayerProfile.DisplayName=="OPERADOR","A fresh installation never exposes the Windows username as an online alias");
                    var old=new SocLearningProgress.Store{learnerId="qa-legacy"};old.seen.Add("v2-spy-1");old.attempts.Add(new SocLearningProgress.Attempt{questionId="v2-spy-1",objectiveId="spy-identify",selectedOption=0,contentVersion=2,source="academy",utc="2026-01-01T00:00:00Z",correct=true,firstAttempt=true});legacy=JsonUtility.ToJson(old,true);File.WriteAllText(Path.Combine(root,"soc-learning-v2.json"),legacy);
                    MainMenuUI.Instance.OpenParticipantProfiles(true);step++;return false;
                case 1:
                    capture("profile-welcome-1920.png");
                    a=ParticipantProfiles.Create("NeonFox");code=ProfileVault.Load(a.localId).recoveryCode;
                    check(ProfileVault.ValidRecoveryCode(code),"Every participant receives a high-entropy private recovery code");
                    check(!File.ReadAllText(Path.Combine(ProfileStorage.For(a.localId),"access.bin")).Contains(code),"Windows encrypts the stored recovery credential");
                    check(SocLearningProgress.Data.attempts.Count==1&&SocLearningProgress.Data.learnerId=="qa-legacy","The first participant receives a copy of legacy learning progress");
                    check(File.ReadAllText(Path.Combine(root,"soc-learning-v2.json"))==legacy,"Migration preserves the complete original legacy file");
                    GameSettings.NoteSpeed=1.6f;GameSettings.AudioOffsetSec=.075f;InputManager.Instance.RemapKey(0,UnityEngine.InputSystem.Key.Q);
                    b=ParticipantProfiles.Create("SecondPlayer");check(SocLearningProgress.Data.attempts.Count==0,"New participants start with separate SOC histories");
                    check(GameSettings.NoteSpeed==1&&GameSettings.AudioOffsetSec==0&&InputManager.Instance.GetKeyForLane(0)==UnityEngine.InputSystem.Key.D,"Input, offset and scroll preferences are separated per participant");
                    ParticipantProfiles.Select(a.localId);check(Math.Abs(GameSettings.NoteSpeed-1.6f)<.01&&Math.Abs(GameSettings.AudioOffsetSec-.075f)<.001&&InputManager.Instance.GetKeyForLane(0)==UnityEngine.InputSystem.Key.Q,"Returning to a participant restores their controls and rhythm preferences");
                    check(SocLearningProgress.Data.attempts.Count==1,"Switching back restores only that participant SOC data");
                    MainMenuUI.Instance.BackToMainMenu();MainMenuUI.Instance.OpenParticipantProfiles();step++;return false;
                case 2:
                    capture("profiles-private-code-hidden-1920.png");size(1280,720);step++;return false;
                case 3:
                    capture("profiles-1280.png");size(1920,1080);
                    chart=CampaignCatalog.CreateChart("Sara");var definition=OnlineCampaigns.Find(chart);
                    check(definition!=null&&definition.judgments==209&&definition.revision.Length==64,"Official chart identity includes the exact resource revision");
                    var custom=ChartData.FromJson(chart.ToJson());custom.notes[0].timestamp+=.01;check(OnlineCampaigns.Find(custom)==null,"Modified charts cannot submit as the official campaign");UnityEngine.Object.Destroy(custom);
                    var record=new GameAttemptRecord{eventId=Guid.NewGuid().ToString("N"),sessionId=PrivateStudyStore.SessionId,chartId=definition.id,chartRevision=definition.revision,gameVersion="0.1.0-beta.1",outcome="victory",startedUtc=DateTime.UtcNow.AddMinutes(-3).ToString("o"),endedUtc=DateTime.UtcNow.ToString("o"),elapsedSeconds=180,playedSeconds=154.9,score=420000,perfect=209,maxCombo=209,noteSpeed=1.6f,offsetMs=75,hasFinalStats=true};
                    PrivateStudyStore.Finish(a.localId,record);PrivateStudyStore.Finish(a.localId,record);
                    check(File.ReadAllLines(Path.Combine(ProfileStorage.For(a.localId),"game-attempts-v1.jsonl")).Length==1,"Each completed attempt is recorded once locally");
                    var q=CyberQuestionBank.GetAllQuestions()[0];SocLearningProgress.Record(q,q.correctIndex,"academy");
                    check(PrivateStudyStore.Pending(a.localId).Length==2,"Completed attempts and SOC answers are queued separately while offline");
                    ParticipantProfiles.Select(b.localId);check(PrivateStudyStore.Pending(b.localId).Length==0,"Changing participant never exposes another participant upload queue");ParticipantProfiles.Select(a.localId);
                    GameplayUI.IsTestPlayFromEditor=true;AttemptRecorder.Instance.EnableValidationCapture=true;chart.audioClip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Levels/Sara.mp3");ChartManager.Instance.SelectChart(chart);NoteSpawner.Instance.LoadChart(chart);NoteSpawner.Instance.StopAndClear();AudioManager.Instance.PlaySong(chart.audioClip,1);GameManager.Instance.StartGame();step=31;return false;
                case 31:
                    GameManager.Instance.RegisterHit(HitRating.Perfect,0);GameManager.Instance.TriggerGameOver();AttemptRecorder.Instance.EnableValidationCapture=false;AudioManager.Instance.StopSong();NoteSpawner.Instance.StopAndClear();GameManager.Instance.SetState(GameState.MainMenu);GameplayUI.IsTestPlayFromEditor=false;MainMenuUI.Instance.BackToMainMenu();
                    var histories=File.ReadAllLines(Path.Combine(ProfileStorage.For(a.localId),"game-attempts-v1.jsonl")).Select(JsonUtility.FromJson<GameAttemptRecord>).ToList();
                    check(histories.Count==2&&histories.Any(h=>h.outcome=="defeat"&&h.perfect==1&&h.score==1010),"Real GameManager defeat events persist the actual attempt and statistics");
                    GameplayUI.IsTestPlayFromEditor=true;AttemptRecorder.Instance.EnableValidationCapture=true;ChartManager.Instance.SelectChart(chart);NoteSpawner.Instance.LoadChart(chart);NoteSpawner.Instance.StopAndClear();AudioManager.Instance.PlaySong(chart.audioClip,1);GameManager.Instance.StartGame();step=32;return false;
                case 32:
                    typeof(AttemptRecorder).GetField("started",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(AttemptRecorder.Instance,Time.realtimeSinceStartupAsDouble-160);
                    for(int i=0;i<209;i++)GameManager.Instance.RegisterHit(HitRating.Perfect,0);AudioManager.Instance.StopSong();GameManager.Instance.TriggerVictory();AttemptRecorder.Instance.EnableValidationCapture=false;GameManager.Instance.SetState(GameState.MainMenu);GameplayUI.IsTestPlayFromEditor=false;MainMenuUI.Instance.BackToMainMenu();
                    var won=File.ReadAllLines(Path.Combine(ProfileStorage.For(a.localId),"game-attempts-v1.jsonl")).Select(JsonUtility.FromJson<GameAttemptRecord>).Last();
                    check(won.outcome=="victory"&&won.playedSeconds>151.55&&won.perfect==209,"Victory retains full song progress even though transport stops before the victory event");
                    fake=new FakeTransport();service.Transport=fake;service.ConfigureForValidation(new OnlineConfiguration{enabled=true,projectUrl="https://validation.invalid",publishableKey="sb_publishable_validation"});service.RequestBoard(chart);step=4;return false;
                case 4:
                    if(PrivateStudyStore.Pending(a.localId).Length>0||!a.registered){service.Retry();if(++polls>30)throw new Exception("Online sync did not complete: "+service.Status);return false;}
                    check(fake.attempts.Count==3&&fake.soc.Count==1,"A lost acknowledgement retries the same event without duplicate server records");
                    check(a.registered&&!string.IsNullOrEmpty(a.tag)&&!string.IsNullOrEmpty(ProfileVault.Load(a.localId).refreshToken),"Anonymous access persists without an email or password");
                    check(fake.calls.All(c=>!c.Contains("email")&&!c.Contains("password")),"Identity requests never ask players for email or password");
                    check(!File.ReadAllText(Path.Combine(root,"participants-v1.json")).Contains(code)&&!File.ReadAllText(Path.Combine(root,"participants-v1.json")).Contains("refreshToken"),"Public profile metadata excludes recovery codes and session credentials");
                    service.RequestBoard(chart);MainMenuUI.Instance.BackToMainMenu();MainMenuUI.Instance.OpenLevelSelect();step++;return false;
                case 5:
                    if(service.Cached(chart)?.entries.Count!=1){service.Retry();return false;}
                    check(service.Cached(chart).entries[0].playerName==a.PublicName,"The public leaderboard displays the chosen alias and tag");capture("global-leaderboard-1920.png");
                    check(!JsonUtility.ToJson(service.Cached(chart)).Contains("questionId")&&!JsonUtility.ToJson(service.Cached(chart)).Contains("refreshToken"),"The public score model cannot expose private research records");
                    recovered=false;service.Recover(code,(ok,message)=>{recovered=ok;recoveryMessage=message;});step++;return false;
                case 6:
                    if(!recovered){if(recoveryMessage!=null)throw new Exception(recoveryMessage);return false;}
                    check(ParticipantProfiles.Current.participantId==a.participantId&&ParticipantProfiles.Current.localId!=a.localId,"Recovery links a new local session to the same global participant");
                    check(SocLearningProgress.Data.attempts.Any(x=>x.attemptId==fake.soc.Keys.First()),"Recovered access restores that participant’s own SOC progress");
                    check(ParticipantProfiles.All.Count==3&&File.ReadAllText(Path.Combine(root,"soc-learning-v2.json"))==legacy,"Recovery preserves all other local participants and the legacy original");
                    check(!new OnlineConfiguration{enabled=true,projectUrl="http://example.com",publishableKey="sb_publishable_test"}.IsValid,"Production refuses unencrypted backend URLs");
                    check(!new OnlineConfiguration{enabled=true,projectUrl="https://example.com",publishableKey="sb_secret_example"}.IsValid,"A server secret cannot be configured as a client key");
                    MainMenuUI.Instance.OpenParticipantProfiles();step++;return false;
                case 7:
                    capture("recovered-profile-1920.png");
                    service.ConfigureForValidation(new OnlineConfiguration());service.Transport=new UnityOnlineTransport();MainMenuUI.Instance.BackToMainMenu();
                    foreach(var p in ParticipantProfiles.All){string prefix="RWProfile_"+p.localId+"_";foreach(var name in new[]{"NoteScrollSpeed","AudioOffsetSec","MasterVolume","MusicVolume","SFXVolume","AudioSetupSeenV1"})PlayerPrefs.DeleteKey(prefix+name);for(int i=0;i<5;i++){PlayerPrefs.DeleteKey(prefix+"Keys_"+LaneLayout.Id+"_"+i);PlayerPrefs.DeleteKey(prefix+"Pad_"+LaneLayout.Id+"_"+i);}}
                    ProfileStorage.SetValidationRoot(null);return true;
            }
            return false;
        }
        sealed class FakeTransport : IOnlineTransport
        {
            public readonly List<string> calls=new List<string>();public readonly Dictionary<string,GameAttemptRecord> attempts=new Dictionary<string,GameAttemptRecord>();public readonly Dictionary<string,SocAnswerRecord> soc=new Dictionary<string,SocAnswerRecord>();
            readonly Dictionary<string,string> refresh=new Dictionary<string,string>(),owner=new Dictionary<string,string>();readonly Dictionary<string,FakeProfile> profiles=new Dictionary<string,FakeProfile>();bool dropAck=true;int tag;
            [Serializable] sealed class FakeProfile {public string participantId,alias,tag,recovery;}
            [Serializable] sealed class Register {public string p_alias,p_recovery_code;}
            [Serializable] sealed class Refresh {public string refresh_token;}
            [Serializable] sealed class Attempt {public GameAttemptRecord p_event;}
            [Serializable] sealed class Soc {public SocAnswerRecord p_event;}
            [Serializable] sealed class Progress {public List<SocLearningProgress.Attempt> attempts;}
            public IEnumerator Send(string url,string key,string token,string json,Action<OnlineReply> done)
            {
                yield return null;string endpoint=url.Substring("https://validation.invalid".Length);calls.Add(endpoint+" "+(endpoint.StartsWith("/auth")?"{}":endpoint.Contains("register")||endpoint.Contains("restore")?"credential-redacted":json));
                if(endpoint.StartsWith("/auth"))
                {
                    string user;if(endpoint.Contains("signup")){user=Guid.NewGuid().ToString();refresh["refresh-"+user]=user;}else user=refresh[JsonUtility.FromJson<Refresh>(json).refresh_token];
                    done(new OnlineReply{status=200,body="{\"access_token\":\"token-"+user+"\",\"refresh_token\":\"refresh-"+user+"\",\"expires_in\":3600,\"user\":{\"id\":\""+user+"\"}}"});yield break;
                }
                string auth=token.Substring(6);string body="{}";
                if(endpoint.EndsWith("rw_register_profile")){var r=JsonUtility.FromJson<Register>(json);if(!owner.ContainsKey(auth)){var profile=new FakeProfile{participantId=Guid.NewGuid().ToString(),alias=r.p_alias,tag=(++tag).ToString("0000"),recovery=r.p_recovery_code};profiles[profile.participantId]=profile;owner[auth]=profile.participantId;}body=JsonUtility.ToJson(profiles[owner[auth]]);}
                else if(endpoint.EndsWith("rw_submit_attempt")){var entry=JsonUtility.FromJson<Attempt>(json).p_event;attempts[entry.eventId]=entry;if(dropAck){dropAck=false;done(new OnlineReply{status=0,body=""});yield break;}}
                else if(endpoint.EndsWith("rw_submit_soc")){var entry=JsonUtility.FromJson<Soc>(json).p_event;soc[entry.eventId]=entry;}
                else if(endpoint.EndsWith("rw_get_board")){var p=profiles.Values.First();var entries=attempts.Values.Where(a=>a.outcome=="victory").GroupBy(a=>a.chartId).Select(g=>g.OrderByDescending(a=>a.score).First()).Select(a=>new LeaderboardScore{playerName=p.alias+"#"+p.tag,chartId=a.chartId,score=a.score,accuracy=100,maxCombo=a.maxCombo,rank="S+",place=1,timestamp=a.endedUtc}).ToList();body=JsonUtility.ToJson(new GlobalBoard{entries=entries,own=entries.FirstOrDefault()});}
                else if(endpoint.EndsWith("rw_restore_profile")){var r=JsonUtility.FromJson<Register>(json);var p=profiles.Values.Single(x=>x.recovery==r.p_recovery_code);owner[auth]=p.participantId;body=JsonUtility.ToJson(p);}
                else if(endpoint.EndsWith("rw_my_soc_progress")){body=JsonUtility.ToJson(new Progress{attempts=soc.Values.Select(s=>new SocLearningProgress.Attempt{attemptId=s.eventId,questionId=s.questionId,objectiveId=s.objectiveId,source=s.source,utc=s.utc,selectedOption=s.selectedOption,contentVersion=s.contentVersion,correct=true,firstAttempt=true}).ToList()});}
                done(new OnlineReply{status=200,body=body});
            }
        }
    }
}
#endif
