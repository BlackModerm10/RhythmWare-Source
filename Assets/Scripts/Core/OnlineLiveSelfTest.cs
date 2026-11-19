using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using RhythmWare.Education;
using RhythmWare.Rhythm;
using RhythmWare.UI;

namespace RhythmWare.Core
{
    // Explicit standalone diagnostics only. Uses isolated local profiles and fictitious remote records.
    public static class OnlineLiveSelfTest
    {
        [Serializable] sealed class Manifest {public List<RemoteProfile> profiles=new List<RemoteProfile>();public string defeatEvent,victoryEvent,socEvent;}
        [Serializable] sealed class RemoteProfile {public string alias,participantId,authUserId;}
        [Serializable] sealed class OwnProgress {public List<SocLearningProgress.Attempt> attempts;}
        [Serializable] sealed class AuthProbe {public UserProbe user;}
        [Serializable] sealed class UserProbe {public bool is_anonymous;}
        sealed class LiveTransport : IOnlineTransport
        {
            readonly UnityOnlineTransport wire=new UnityOnlineTransport();
            public bool offline,dropNextAck;public int anonymousSignIns,attemptPosts;public string lastToken;
            public IEnumerator Send(string url,string key,string token,string json,Action<OnlineReply> done)
            {
                if(offline){done(new OnlineReply{status=0,body=""});yield break;}
                if(!string.IsNullOrEmpty(token))lastToken=token;
                if(url.EndsWith("/rw_submit_attempt"))attemptPosts++;
                OnlineReply reply=null;yield return wire.Send(url,key,token,json,r=>reply=r);
                if(reply?.Ok==true&&url.EndsWith("/auth/v1/signup")&&JsonUtility.FromJson<AuthProbe>(reply.body)?.user?.is_anonymous==true)anonymousSignIns++;
                if(dropNextAck&&reply?.Ok==true&&url.EndsWith("/rw_submit_attempt")){dropNextAck=false;reply=new OnlineReply{status=0,body=""};}
                done(reply);
            }
        }
        static IEnumerator Wait(Func<bool> ready,Action<bool,string> check,string message)
        {
            double until=Time.realtimeSinceStartupAsDouble+40;
            while(!ready()&&Time.realtimeSinceStartupAsDouble<until)yield return new WaitForSecondsRealtime(.1f);
            check(ready(),message);
        }
        public static IEnumerator Run(Action<bool,string> check,Action<string> capture,string output)
        {
            check(ProfileStorage.IsValidation&&!Application.isEditor,"Live checks use an explicitly requested isolated Windows player");
            var online=OnlineService.Instance;check(online!=null&&online.Configured,"Exported player includes a valid enabled Supabase connection");
            check(ParticipantProfiles.All.Count==0,"Remote test starts in a fresh local profile directory");
            var config=JsonUtility.FromJson<OnlineConfiguration>(Resources.Load<TextAsset>("OnlineSettings").text);
            var wire=new LiveTransport();online.Transport=wire;
            var manifest=new Manifest();string nonce=Guid.NewGuid().ToString("N").Substring(0,8);
            Action<ParticipantProfile> remember=p=>{
                if(manifest.profiles.All(x=>x.authUserId!=p.authUserId))manifest.profiles.Add(new RemoteProfile{alias=p.alias,participantId=p.participantId,authUserId=p.authUserId});
                File.WriteAllText(Path.Combine(output,"remote-qa-ids.json"),JsonUtility.ToJson(manifest,true));
            };
            var a=ParticipantProfiles.Create("QA RW "+nonce+" A");
            yield return Wait(()=>a.registered,check,"Actual anonymous authentication registers the first alias");remember(a);
            check(wire.anonymousSignIns==1&&!string.IsNullOrEmpty(a.tag),"Supabase creates an anonymous user and a tagged public alias");
            var chart=CampaignCatalog.CreateChart("Sara");var definition=OnlineCampaigns.Find(chart);
            var loss=Attempt(definition,"defeat");manifest.defeatEvent=loss.eventId;
            wire.offline=true;PrivateStudyStore.Finish(a.localId,loss);online.Retry();yield return new WaitForSecondsRealtime(.8f);
            check(PrivateStudyStore.Pending(a.localId).Length==1,"Offline attempt remains in its participant upload queue");
            wire.offline=false;online.Retry();yield return Wait(()=>PrivateStudyStore.Pending(a.localId).Length==0,check,"Reconnection uploads the queued defeat through the real API");
            online.RequestBoard(chart);yield return Wait(()=>online.Cached(chart)!=null,check,"The real leaderboard RPC returns a board");
            check(online.Cached(chart).entries.Count==0,"A defeat does not publish a public score");
            var victory=Attempt(definition,"victory");manifest.victoryEvent=victory.eventId;
            wire.dropNextAck=true;int before=wire.attemptPosts;PrivateStudyStore.Finish(a.localId,victory);online.Retry();
            yield return Wait(()=>PrivateStudyStore.Pending(a.localId).Length==0&&wire.attemptPosts>=before+2,check,"A lost acknowledgement resends the same event until confirmed");
            var question=CyberQuestionBank.GetAllQuestions()[0];SocLearningProgress.Record(question,question.correctIndex,"academy");
            manifest.socEvent=SocLearningProgress.Data.attempts.Last().attemptId;remember(a);online.Retry();
            yield return Wait(()=>PrivateStudyStore.Pending(a.localId).Length==0,check,"A SOC answer uploads privately through the real API");
            online.RequestBoard(chart);yield return Wait(()=>online.Cached(chart)?.own?.score==victory.score,check,"Victory is visible as the participant best global score");
            MainMenuUI.Instance.OpenLevelSelect();yield return new WaitForSecondsRealtime(.6f);capture("player-global-live.png");MainMenuUI.Instance.BackToMainMenu();
            var b=ParticipantProfiles.Create("QA RW "+nonce+" B");
            check(online.Cached(chart)==null,"Switching participant clears the previous own-position cache");
            yield return Wait(()=>b.registered,check,"A second participant receives a separate real identity");remember(b);
            check(a.participantId!=b.participantId&&SocLearningProgress.Data.attempts.Count==0,"Separate participants retain separate SOC histories");
            online.RequestBoard(chart);yield return Wait(()=>online.Cached(chart)!=null,check,"The second participant can read the shared leaderboard");
            check(online.Cached(chart).own==null&&online.Cached(chart).entries.Single().playerName==a.PublicName,"Global records are shared without transferring ownership");
            OnlineReply reply=null;yield return wire.Send(config.projectUrl+"/rest/v1/rpc/rw_my_soc_progress",config.publishableKey,wire.lastToken,"{}",r=>reply=r);
            check(reply?.Ok==true&&JsonUtility.FromJson<OwnProgress>(reply.body).attempts.Count==0,"Another participant own-progress RPC returns none of the first participant answers");
            using(var request=UnityWebRequest.Get(config.projectUrl+"/rest/v1/game_attempts?select=event_id"))
            {
                request.timeout=12;request.SetRequestHeader("apikey",config.publishableKey);request.SetRequestHeader("Authorization","Bearer "+wire.lastToken);request.SetRequestHeader("Accept-Profile","rw_private");
                yield return request.SendWebRequest();check(request.responseCode==403||request.responseCode==406,"Data API refuses direct access to the private history schema");
            }
            string recoveryCode=ProfileVault.Load(a.localId).recoveryCode;bool recovered=false,recoveryOk=false;
            online.Recover(recoveryCode,(ok,message)=>{recoveryOk=ok;recovered=true;});
            yield return Wait(()=>recovered,check,"Recovery completes using only the private code");
            var recoveredProfile=ParticipantProfiles.Current;remember(recoveredProfile);
            check(recoveryOk&&recoveredProfile.participantId==a.participantId&&recoveredProfile.authUserId!=a.authUserId,"A fresh anonymous session recovers the original stable participant identity");
            check(SocLearningProgress.Data.attempts.Count==1&&SocLearningProgress.Data.attempts[0].questionId==question.id,"Recovery restores the synchronized SOC answer");
            online.RequestBoard(chart);yield return Wait(()=>online.Cached(chart)?.own?.score==victory.score,check,"Recovered identity retains its best score and own ranking");
            check(wire.anonymousSignIns==3,"All three real sessions were created without email or password");
            MainMenuUI.Instance.OpenParticipantProfiles();yield return null;capture("player-profile-live.png");MainMenuUI.Instance.BackToMainMenu();
            wire.offline=true;UnityEngine.Object.Destroy(chart);
        }
        static GameAttemptRecord Attempt(OnlineCampaigns.Definition chart,string outcome)
        {
            bool win=outcome=="victory";
            return new GameAttemptRecord{eventId=Guid.NewGuid().ToString("N"),sessionId=PrivateStudyStore.SessionId,chartId=chart.id,chartRevision=chart.revision,gameVersion=Application.version,outcome=outcome,startedUtc=DateTime.UtcNow.AddMinutes(-3).ToString("o"),endedUtc=DateTime.UtcNow.ToString("o"),elapsedSeconds=180,playedSeconds=win?chart.duration:20,score=win?420000:1000,perfect=win?chart.judgments:1,miss=win?0:3,maxCombo=win?chart.judgments:1,noteSpeed=1,offsetMs=0,hasFinalStats=true};
        }
    }
}
