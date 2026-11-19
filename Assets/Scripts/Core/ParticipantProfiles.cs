using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace RhythmWare.Core
{
    public static class ProfileStorage
    {
        static string validationRoot;
        static string cachedBase;static bool? cachedValidation;
        public static void SetValidationRoot(string path){if(!IsValidation)throw new InvalidOperationException();validationRoot=path;ParticipantProfiles.RefreshStorage();}
        public static bool IsValidation
        {
            get {
                if(cachedValidation.HasValue)return cachedValidation.Value;
#if UNITY_EDITOR
                if(UnityEditor.SessionState.GetBool("RWVisualValidation",false)){cachedValidation=true;return true;}
#endif
                cachedValidation=Array.IndexOf(Environment.GetCommandLineArgs(),"--rw-self-test")>=0;return cachedValidation.Value;
            }
        }
        public static string BaseRoot
        {
            get {
                if(validationRoot!=null&&IsValidation)return validationRoot;
                if(cachedBase!=null)return cachedBase;
#if UNITY_EDITOR
                if(UnityEditor.SessionState.GetBool("RWVisualValidation",false))return cachedBase=Path.Combine(Path.GetDirectoryName(Application.dataPath),"docs",UnityEditor.SessionState.GetString("RWValidationFolder","validation"),"player-data");
#endif
                var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--rw-self-test");
                return cachedBase=i>=0&&i+1<args.Length?Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[i+1])),"player-data"):Application.persistentDataPath;
            }
        }
        public static string DataRoot=>ParticipantProfiles.Current==null?BaseRoot:For(ParticipantProfiles.Current.localId);
        public static string For(string id)
        {if(!Guid.TryParseExact(id,"N",out _))throw new ArgumentException("Identidad local inválida.");return Path.Combine(BaseRoot,"Participants",id);}
        public static void AtomicWrite(string path,string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".new";
            File.WriteAllText(temp,text,new System.Text.UTF8Encoding(false));
            if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset(){validationRoot=null;cachedBase=null;cachedValidation=null;}
    }
    [Serializable] public sealed class ParticipantProfile
    {
        public string localId,alias,tag,participantId,authUserId,createdUtc;
        public bool registered,pendingAlias,consent=true;
        public string PublicName=>alias+(string.IsNullOrEmpty(tag)?"":"#"+tag);
    }
    public static class ParticipantProfiles
    {
        [Serializable] sealed class Index {public int version=1;public string selected;public List<ParticipantProfile> profiles=new List<ParticipantProfile>();public ParticipantProfile recoveryDraft;}
        static Index index;static string loadedRoot;
        public static event Action ActiveChanged,InfoChanged;
        static string FilePath=>Path.Combine(ProfileStorage.BaseRoot,"participants-v1.json");
        static Index Data
        {
            get {
                if(index!=null&&loadedRoot==ProfileStorage.BaseRoot)return index;
                loadedRoot=ProfileStorage.BaseRoot;
                if(File.Exists(FilePath))
                {
                    try {index=JsonUtility.FromJson<Index>(File.ReadAllText(FilePath));if(index==null||index.version!=1||index.profiles==null||index.profiles.Any(p=>p==null||!Guid.TryParseExact(p.localId,"N",out _)))throw new InvalidDataException();}
                    catch {index=null;throw new IOException("No se pudo leer el índice de participantes. El archivo original se conserva.");}
                }
                else index=new Index();return index;
            }
        }
        public static ParticipantProfile Current=>Data.profiles.FirstOrDefault(p=>p.localId==Data.selected);
        public static IReadOnlyList<ParticipantProfile> All=>Data.profiles;
        public static ParticipantProfile RecoveryDraft=>Data.recoveryDraft;
        public static bool ValidAlias(string value)=>value!=null&&Regex.IsMatch(value.Trim(),@"^[A-Za-z0-9ÁÉÍÓÚÜÑáéíóúüñ][A-Za-z0-9ÁÉÍÓÚÜÑáéíóúüñ _.-]{2,19}$");
        public static ParticipantProfile Create(string alias)
        {
            if(!ValidAlias(alias))throw new ArgumentException("Usa entre 3 y 20 letras, números, espacios, puntos, guiones o guion bajo.");
            bool first=Data.profiles.Count==0;var profile=NewProfile(alias.Trim());
            ProfileVault.Save(profile.localId,new ProfileVault.Secrets{recoveryCode=ProfileVault.NewRecoveryCode()});
            if(first)MigrateLegacy(profile.localId);
            Data.profiles.Add(profile);Data.selected=profile.localId;Save();Changed();return profile;
        }
        static ParticipantProfile NewProfile(string alias)=>new ParticipantProfile{localId=Guid.NewGuid().ToString("N"),alias=alias,createdUtc=DateTime.UtcNow.ToString("o")};
        static void MigrateLegacy(string id)
        {
            string root=ProfileStorage.For(id);Directory.CreateDirectory(root);
            foreach(string name in new[]{"soc-learning-v2.json","scores-v1.json"}){string old=Path.Combine(ProfileStorage.BaseRoot,name);if(File.Exists(old))File.Copy(old,Path.Combine(root,name),false);}
            string prefix="RWProfile_"+id+"_";
            foreach(string key in new[]{"NoteScrollSpeed","AudioOffsetSec","MasterVolume","MusicVolume","SFXVolume"})if(PlayerPrefs.HasKey(key))PlayerPrefs.SetFloat(prefix+key,PlayerPrefs.GetFloat(key));
            if(PlayerPrefs.HasKey("AudioSetupSeenV1"))PlayerPrefs.SetInt(prefix+"AudioSetupSeenV1",PlayerPrefs.GetInt("AudioSetupSeenV1"));
            for(int i=0;i<5;i++)foreach(string kind in new[]{"Keys_","Pad_"}){string key=kind+LaneLayout.Id+"_"+i;if(PlayerPrefs.HasKey(key))PlayerPrefs.SetInt(prefix+key,PlayerPrefs.GetInt(key));}
            PlayerPrefs.Save();
        }
        public static void Select(string id)
        {
            var state=GameManager.Instance?.CurrentState;
            if(state==GameState.Playing||state==GameState.Paused||state==GameState.RescueQuestion||state==GameState.Reviving||state==GameState.TutorialInstruction)throw new InvalidOperationException("Vuelve al menú para cambiar de participante.");
            if(!Data.profiles.Any(p=>p.localId==id))throw new ArgumentException("Participante desconocido.");
            if(Data.selected==id)return;Data.selected=id;Save();Changed();
        }
        public static void Rename(string alias)
        {if(Current==null||!ValidAlias(alias))throw new ArgumentException("Alias inválido: usa entre 3 y 20 caracteres.");Current.alias=alias.Trim();Current.pendingAlias=true;Save();InfoChanged?.Invoke();}
        public static ParticipantProfile BeginRecovery(string code)
        {
            if(!ProfileVault.ValidRecoveryCode(code))throw new ArgumentException("El código de recuperación no tiene el formato correcto.");
            if(Data.recoveryDraft!=null&&ProfileVault.Load(Data.recoveryDraft.localId).recoveryCode==code)return Data.recoveryDraft;
            var draft=NewProfile("Recuperando");ProfileVault.Save(draft.localId,new ProfileVault.Secrets{recoveryCode=code});Data.recoveryDraft=draft;Save();return draft;
        }
        public static void CommitRecovery(ParticipantProfile profile)
        {if(Data.recoveryDraft?.localId!=profile.localId)throw new InvalidOperationException("Recuperación desactualizada.");Data.profiles.Add(profile);Data.recoveryDraft=null;Data.selected=profile.localId;Save();Changed();}
        public static void SaveInfo(){Save();InfoChanged?.Invoke();}
        public static void RefreshStorage(){Changed();}
        static void Save()=>ProfileStorage.AtomicWrite(FilePath,JsonUtility.ToJson(Data,true));
        static void Changed()
        {
            RhythmWare.Education.SocLearningProgress.Reload();
            if(Leaderboards.Provider is LocalLeaderboardProvider)Leaderboards.Provider=new LocalLeaderboardProvider();
            InputManager.Instance?.ReloadBindings();GameSettings.NotifyProfileChanged();AudioManager.Instance?.UpdateVolumes();
            PrivateStudyStore.RecoverInterrupted();ActiveChanged?.Invoke();InfoChanged?.Invoke();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset(){index=null;loadedRoot=null;ActiveChanged=null;InfoChanged=null;}
    }
}
