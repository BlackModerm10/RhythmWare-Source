using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmWare.Education
{
    public static class SocLearningProgress
    {
        [Serializable] public sealed class Attempt
        {
            public string questionId,objectiveId,source,utc,attemptId;
            public int selectedOption,contentVersion;
            public bool correct,firstAttempt;
        }
        [Serializable] public sealed class Store
        {
            public int schemaVersion=2;
            public string learnerId=Guid.NewGuid().ToString("N");
            public List<string> seen=new List<string>();
            public List<Attempt> attempts=new List<Attempt>();
        }
        static Store store;
        static string overridePath;
        public static string LastThreat="Spyware";
        public static bool LastSaveSucceeded { get; private set; }=true;
        public static string Pathname=>overridePath??Path.Combine(RhythmWare.Core.ProfileStorage.DataRoot,"soc-learning-v2.json");
        public static void Reload(){store=null;LastThreat="Spyware";}
        public static Store Data
        {
            get
            {
                if(store!=null)return store;
                try{if(File.Exists(Pathname)){store=JsonUtility.FromJson<Store>(File.ReadAllText(Pathname));if(store?.schemaVersion!=2)store=null;}}
                catch(Exception e){Debug.LogWarning("No se pudo leer el progreso SOC: "+e.Message);}
                if(store==null)store=new Store();if(store.seen==null)store.seen=new List<string>();if(store.attempts==null)store.attempts=new List<Attempt>();store.attempts.RemoveAll(a=>a==null);return store;
            }
        }
        public static Dictionary<string,int> LatestAnswers()
        {
            var result=new Dictionary<string,int>();foreach(var a in Data.attempts)if(a.contentVersion==CyberQuestionBank.ContentVersion&&a.selectedOption>=0&&a.selectedOption<3&&!string.IsNullOrEmpty(a.questionId))result[a.questionId]=a.selectedOption;return result;
        }
        public static void Seen(CyberQuestion q)
        {if(q!=null&&!Data.seen.Contains(q.id)){Data.seen.Add(q.id);Save();}}
        public static void Record(CyberQuestion q,int canonicalOption,string source)
        {
            if(q==null||canonicalOption<0||canonicalOption>=q.options.Length)return;
            Seen(q);bool first=!Data.attempts.Any(a=>a.questionId==q.id&&a.contentVersion==CyberQuestionBank.ContentVersion);
            var attempt=new Attempt{attemptId=Guid.NewGuid().ToString("N"),questionId=q.id,objectiveId=q.objectiveId,source=source,utc=DateTime.UtcNow.ToString("o"),selectedOption=canonicalOption,contentVersion=CyberQuestionBank.ContentVersion,correct=canonicalOption==q.correctIndex,firstAttempt=first};Data.attempts.Add(attempt);Save();if(LastSaveSucceeded)RhythmWare.Core.PrivateStudyStore.CaptureSoc(attempt);
        }
        public static void ImportOwnProgress(List<Attempt> rows)
        {
            if(rows==null)return;
            foreach(var row in rows){if(string.IsNullOrEmpty(row.attemptId)||Data.attempts.Any(a=>a.attemptId==row.attemptId))continue;Data.attempts.Add(row);if(!Data.seen.Contains(row.questionId))Data.seen.Add(row.questionId);}
            Data.attempts=Data.attempts.OrderBy(a=>DateTime.TryParse(a.utc,out var time)?time.ToUniversalTime():DateTime.MinValue).ToList();Save();
        }
        public static int FirstTryCorrect => Data.attempts.Count(a=>a.firstAttempt&&a.correct&&a.contentVersion==CyberQuestionBank.ContentVersion);
        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Pathname));string temp=Pathname+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(Data,true));
                if(File.Exists(Pathname))File.Replace(temp,Pathname,null);else File.Move(temp,Pathname);LastSaveSucceeded=true;
            }
            catch(Exception e){LastSaveSucceeded=false;Debug.LogWarning("No se pudo guardar el progreso SOC: "+e.Message);}
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){store=null;overridePath=null;LastThreat="Spyware";LastSaveSucceeded=true;}
#if UNITY_EDITOR
        public static void SetValidationPath(string path){overridePath=path;store=null;}
#endif
    }
}
