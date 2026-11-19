using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using RhythmWare.Rhythm;
using RhythmWare.Education;

namespace RhythmWare.Core
{
    [Serializable] public sealed class GameAttemptRecord
    {
        public string eventId,sessionId,chartId,chartRevision,gameVersion,outcome,startedUtc,endedUtc;
        public double elapsedSeconds,playedSeconds;
        public int score,perfect,good,miss,maxCombo;
        public float noteSpeed,offsetMs;
        public bool hasFinalStats;
    }
    [Serializable] public sealed class SocAnswerRecord
    {
        public string eventId,sessionId,questionId,objectiveId,source,utc,gameVersion;
        public int selectedOption,contentVersion,priorLocalAttempts;
    }
    [Serializable] public sealed class UploadEnvelope {public string kind;public GameAttemptRecord attempt;public SocAnswerRecord soc;}
    public static class PrivateStudyStore
    {
        static readonly object gate=new object();
        static readonly HashSet<string> completed=new HashSet<string>();
        public static readonly string SessionId=Guid.NewGuid().ToString("N");
        static string Checkpoint(string id)=>Path.Combine(ProfileStorage.For(id),"active-attempt.json");
        public static void SaveCheckpoint(string id,GameAttemptRecord value,bool background)
        {
            string json=JsonUtility.ToJson(value),path=Checkpoint(id);Action write=()=>{lock(gate){if(!completed.Contains(value.eventId))ProfileStorage.AtomicWrite(path,json);}};
            if(background)Task.Run(()=>{try{write();}catch{ /* The final append retries on the foreground; keep the previous checkpoint. */ }});else write();
        }
        public static void Finish(string id,GameAttemptRecord record)
        {
            lock(gate)
            {
                string archive=Path.Combine(ProfileStorage.For(id),"game-attempts-v1.jsonl");
                if(completed.Contains(record.eventId))return;
                if(File.Exists(archive)&&File.ReadLines(archive).Any(l=>!string.IsNullOrWhiteSpace(l)&&JsonUtility.FromJson<GameAttemptRecord>(l).eventId==record.eventId)){completed.Add(record.eventId);DeleteCheckpoint(id);return;}
                Directory.CreateDirectory(Path.GetDirectoryName(archive));
                // Persist the idempotent upload before the archive: a crash never loses both.
                Enqueue(id,new UploadEnvelope{kind="attempt",attempt=record});
                File.AppendAllText(archive,JsonUtility.ToJson(record)+"\n");completed.Add(record.eventId);DeleteCheckpoint(id);
            }
        }
        static void DeleteCheckpoint(string id){string p=Checkpoint(id);if(File.Exists(p))File.Delete(p);}
        public static void DiscardPracticeCheckpoint(string id,string eventId){lock(gate){completed.Add(eventId);DeleteCheckpoint(id);}}
        public static void RecoverInterrupted()
        {
            var profile=ParticipantProfiles.Current;if(profile==null)return;string file=Checkpoint(profile.localId);if(!File.Exists(file))return;
            try{var record=JsonUtility.FromJson<GameAttemptRecord>(File.ReadAllText(file));if(record==null||!Guid.TryParse(record.eventId,out _))return;record.outcome="interrupted";record.endedUtc=DateTime.UtcNow.ToString("o");record.hasFinalStats=false;Finish(profile.localId,record);}
            catch(Exception){Debug.LogWarning("No se pudo recuperar el intento interrumpido. Se conserva su archivo.");}
        }
        public static void CaptureSoc(SocLearningProgress.Attempt attempt)
        {
            var profile=ParticipantProfiles.Current;if(profile==null)return;
            Enqueue(profile.localId,new UploadEnvelope{kind="soc",soc=new SocAnswerRecord{eventId=attempt.attemptId,sessionId=SessionId,questionId=attempt.questionId,objectiveId=attempt.objectiveId,source=attempt.source,utc=attempt.utc,selectedOption=attempt.selectedOption,contentVersion=attempt.contentVersion,priorLocalAttempts=Math.Max(0,SocLearningProgress.Data.attempts.Count(a=>a.questionId==attempt.questionId&&a.contentVersion==attempt.contentVersion)-1),gameVersion=Application.version}});
        }
        static void Enqueue(string id,UploadEnvelope value)
        {
            string eventId=value.kind=="attempt"?value.attempt.eventId:value.soc.eventId;
            if(!Guid.TryParseExact(eventId,"N",out _))throw new ArgumentException("Evento inválido.");
            string path=Path.Combine(ProfileStorage.For(id),"outbox",eventId+".json");
            if(!File.Exists(path))ProfileStorage.AtomicWrite(path,JsonUtility.ToJson(value));
        }
        public static string[] Pending(string id){string folder=Path.Combine(ProfileStorage.For(id),"outbox");return Directory.Exists(folder)?Directory.GetFiles(folder,"*.json").OrderBy(File.GetCreationTimeUtc).ToArray():Array.Empty<string>();}
        public static void Acknowledge(string id,string path)
        {string expected=Path.GetFullPath(Path.Combine(ProfileStorage.For(id),"outbox"))+Path.DirectorySeparatorChar;if(!Path.GetFullPath(path).StartsWith(expected,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException();File.Delete(path);}
        public static void RetainRejected(string id,string path)
        {
            string folder=Path.Combine(ProfileStorage.For(id),"outbox")+Path.DirectorySeparatorChar;
            if(!Path.GetFullPath(path).StartsWith(Path.GetFullPath(folder),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException();
            string retained=Path.Combine(ProfileStorage.For(id),"retained");Directory.CreateDirectory(retained);File.Move(path,Path.Combine(retained,Guid.NewGuid().ToString("N")+".json"));
        }
        public static void RetryRetained(string id)
        {
            string folder=Path.Combine(ProfileStorage.For(id),"retained");if(!Directory.Exists(folder))return;
            foreach(var path in Directory.GetFiles(folder,"*.json"))try{var value=JsonUtility.FromJson<UploadEnvelope>(File.ReadAllText(path));string eventId=value.kind=="attempt"?value.attempt.eventId:value.soc.eventId;if(!Guid.TryParseExact(eventId,"N",out _))continue;string dest=Path.Combine(ProfileStorage.For(id),"outbox",eventId+".json");if(!File.Exists(dest))File.Move(path,dest);}catch{ /* Keep unreadable records for the owner. */ }
        }
    }
    public sealed class AttemptRecorder : MonoBehaviour
    {
        public static AttemptRecorder Instance {get;private set;}
#if UNITY_EDITOR
        public bool EnableValidationCapture {get;set;}
#endif
        GameAttemptRecord active;string owner;double started,nextSnapshot;
        void Awake(){Instance=this;GameManager.Instance.OnStateChanged+=State;}
        void OnDestroy(){if(GameManager.Instance!=null)GameManager.Instance.OnStateChanged-=State;}
        void Update()
        {
            var gm=GameManager.Instance;if(gm==null)return;
            if(active!=null&&RhythmWare.UI.TutorialGuide.Instance?.IsTraining==true){PrivateStudyStore.DiscardPracticeCheckpoint(owner,active.eventId);active=null;}
            if(active==null)BeginCurrent();
            if(active!=null&&AudioManager.Instance?.IsPlaying==true)active.playedSeconds=Math.Max(active.playedSeconds,Math.Max(0,AudioManager.Instance.SongTime));
            if(active!=null&&gm.CurrentState==GameState.Playing&&Time.realtimeSinceStartupAsDouble>=nextSnapshot){Snapshot();nextSnapshot=Time.realtimeSinceStartupAsDouble+10;PrivateStudyStore.SaveCheckpoint(owner,Clone(active),true);}
        }
        public void BeginCurrent()
        {
            var gm=GameManager.Instance;if(gm==null||active!=null)return;
            bool testCapture=false;
#if UNITY_EDITOR
            testCapture=EnableValidationCapture;
#endif
            bool eligible=ParticipantProfiles.Current!=null&&(!ProfileStorage.IsValidation||testCapture)&&(!RhythmWare.UI.GameplayUI.IsTestPlayFromEditor||testCapture)&&RhythmWare.UI.TutorialGuide.Instance?.NeedsGuidedPractice!=true&&!gm.TrainingSafety;
            if(active==null&&eligible&&gm.CurrentState==GameState.Playing)
            {
                var definition=OnlineCampaigns.Find(ChartManager.Instance?.SelectedChart);if(definition==null)return;
                owner=ParticipantProfiles.Current.localId;started=Time.realtimeSinceStartupAsDouble;nextSnapshot=started+10;
                active=new GameAttemptRecord{eventId=Guid.NewGuid().ToString("N"),sessionId=PrivateStudyStore.SessionId,chartId=definition.id,chartRevision=definition.revision,gameVersion=Application.version,startedUtc=DateTime.UtcNow.ToString("o"),outcome="interrupted"};
                Snapshot();PrivateStudyStore.SaveCheckpoint(owner,active,false);
            }
        }
        static GameAttemptRecord Clone(GameAttemptRecord record)=>JsonUtility.FromJson<GameAttemptRecord>(JsonUtility.ToJson(record));
        void State(GameState state)
        {if(state==GameState.Victory)Finish("victory");else if(state==GameState.GameOver)Finish("defeat");else if(state==GameState.Paused&&active!=null){Snapshot();PrivateStudyStore.SaveCheckpoint(owner,Clone(active),true);}}
        void Snapshot()
        {
            var gm=GameManager.Instance;active.score=gm.CurrentScore;active.perfect=gm.PerfectHits;active.good=gm.GoodHits;active.miss=gm.MissHits;active.maxCombo=gm.MaxCombo;
            active.elapsedSeconds=Math.Max(0,Time.realtimeSinceStartupAsDouble-started);active.playedSeconds=Math.Max(active.playedSeconds,Math.Max(0,AudioManager.Instance?.SongTime??0));active.noteSpeed=GameSettings.NoteSpeed;active.offsetMs=GameSettings.AudioOffsetSec*1000;
        }
        public void Finish(string outcome)
        {
            if(active==null)return;Snapshot();active.outcome=outcome;active.endedUtc=DateTime.UtcNow.ToString("o");active.hasFinalStats=true;
            // The victory path stops transport before raising OnVictory.
            if(outcome=="victory")active.playedSeconds=Math.Max(active.playedSeconds,AudioManager.Instance?.Duration??0);
            try{PrivateStudyStore.Finish(owner,active);active=null;}catch(Exception){Debug.LogWarning("No se pudo guardar el intento completo. Su punto de recuperación se conserva.");}
        }
        void OnApplicationQuit(){Finish("interrupted");}
    }
}
