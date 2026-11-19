using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using RhythmWare.Rhythm;

namespace RhythmWare.Core
{
    [Serializable] public sealed class OnlineConfiguration
    {
        public bool enabled;public string projectUrl,publishableKey;
        public bool IsValid
        {
            get {
                if(!enabled||!Uri.TryCreate(projectUrl,UriKind.Absolute,out var url)||url.Scheme!="https"||url.UserInfo.Length>0||url.Query.Length>0||url.Fragment.Length>0||url.AbsolutePath!="/")return false;
                if(string.IsNullOrEmpty(publishableKey))return false;
                if(publishableKey.StartsWith("sb_publishable_"))return true;
                try{var parts=publishableKey.Split('.');string part=parts[1].Replace('-','+').Replace('_','/');part=part.PadRight((part.Length+3)/4*4,'=');return JsonUtility.FromJson<KeyClaims>(Encoding.UTF8.GetString(Convert.FromBase64String(part))).role=="anon";}catch{return false;}
            }
        }
        [Serializable] sealed class KeyClaims {public string role;}
    }
    public sealed class OnlineReply {public long status;public string body;public bool Ok=>status>=200&&status<300;}
    public interface IOnlineTransport {IEnumerator Send(string url,string key,string token,string json,Action<OnlineReply> reply);}
    public sealed class UnityOnlineTransport : IOnlineTransport
    {
        public IEnumerator Send(string url,string key,string token,string json,Action<OnlineReply> done)
        {
            using(var request=new UnityWebRequest(url,"POST"))
            {
                request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));request.downloadHandler=new DownloadHandlerBuffer();request.timeout=12;
                request.SetRequestHeader("Content-Type","application/json");request.SetRequestHeader("apikey",key);
                if(!string.IsNullOrEmpty(token))request.SetRequestHeader("Authorization","Bearer "+token);
                yield return request.SendWebRequest();done(new OnlineReply{status=request.responseCode,body=request.downloadHandler.text});
            }
        }
    }
    [Serializable] public sealed class GlobalBoard
    {public List<LeaderboardScore> entries=new List<LeaderboardScore>();public LeaderboardScore own;}
    public sealed class OnlineService : MonoBehaviour
    {
        public static OnlineService Instance {get;private set;}
        public string Status {get;private set;}="Clasificación global aún no disponible.";
        public bool Configured=>configuration!=null&&configuration.IsValid;
        public event Action Changed;
        public IOnlineTransport Transport {get;set;}=new UnityOnlineTransport();
        OnlineConfiguration configuration;string token,sessionOwner;double expires,retryAt;int failures,generation;
        string recoveryRequest;Action<bool,string> recoveryDone;
        readonly Dictionary<string,GlobalBoard> boards=new Dictionary<string,GlobalBoard>();
        readonly Queue<OnlineCampaigns.Definition> requests=new Queue<OnlineCampaigns.Definition>();
        [Serializable] sealed class Auth {public string access_token,refresh_token;public int expires_in;public User user;}
        [Serializable] sealed class User {public string id;}
        [Serializable] sealed class Refresh {public string refresh_token;}
        [Serializable] sealed class Register {public string p_alias,p_recovery_code;}
        [Serializable] sealed class Alias {public string p_alias;}
        [Serializable] sealed class Restore {public string p_recovery_code;}
        [Serializable] sealed class ProfileReply {public string participantId,alias,tag;}
        [Serializable] sealed class BoardRequest {public string p_chart_id,p_revision;}
        [Serializable] sealed class AttemptRequest {public GameAttemptRecord p_event;}
        [Serializable] sealed class SocRequest {public SocAnswerRecord p_event;}
        [Serializable] sealed class SocProgress {public List<RhythmWare.Education.SocLearningProgress.Attempt> attempts;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot()
        {if(Instance!=null)return;var root=new GameObject("[OnlineParticipants]");DontDestroyOnLoad(root);root.AddComponent<OnlineService>();root.AddComponent<AttemptRecorder>();}
        void Awake()
        {
            if(Instance!=null){Destroy(gameObject);return;}Instance=this;
            var asset=Resources.Load<TextAsset>("OnlineSettings");configuration=asset!=null?JsonUtility.FromJson<OnlineConfiguration>(asset.text):new OnlineConfiguration();
            ParticipantProfiles.ActiveChanged+=ActiveChanged;StartCoroutine(Worker());
        }
        void OnDestroy(){ParticipantProfiles.ActiveChanged-=ActiveChanged;}
        void ActiveChanged(){generation++;token=null;sessionOwner=null;retryAt=0;failures=0;requests.Clear();boards.Clear();Signal("Perfil cambiado.");}
        void Signal(string text){Status=text;Changed?.Invoke();}
        public GlobalBoard Cached(ChartData chart)
        {var d=OnlineCampaigns.Find(chart);return d!=null&&boards.TryGetValue(d.id+":"+d.revision,out var board)?board:null;}
        public void RequestBoard(ChartData chart)
        {var d=OnlineCampaigns.Find(chart);if(d==null||requests.Any(x=>x.id==d.id))return;requests.Enqueue(d);retryAt=0;}
        public void Retry(){if(ParticipantProfiles.Current!=null)PrivateStudyStore.RetryRetained(ParticipantProfiles.Current.localId);retryAt=0;failures=0;}
        public void Recover(string code,Action<bool,string> done)
        {
            if(!Configured){done(false,"La recuperación online estará disponible cuando se active la clasificación global.");return;}
            if(recoveryRequest!=null){done(false,"Ya hay una recuperación en curso.");return;}
            if(!ProfileVault.ValidRecoveryCode(code)){done(false,"Código de recuperación inválido.");return;}
            recoveryRequest=code;recoveryDone=done;retryAt=0;
        }
        public void ConfigureForValidation(OnlineConfiguration value){if(!ProfileStorage.IsValidation)throw new InvalidOperationException();configuration=value;retryAt=0;}
        IEnumerator Call(string endpoint,object payload,Action<OnlineReply> done,bool authenticated=true)
        {yield return Transport.Send(configuration.projectUrl.TrimEnd('/')+endpoint,configuration.publishableKey,authenticated?token:null,payload is string raw?raw:JsonUtility.ToJson(payload),done);}
        IEnumerator Authenticate(ParticipantProfile profile,Action<bool> done)
        {
            if(sessionOwner==profile.localId&&!string.IsNullOrEmpty(token)&&Time.realtimeSinceStartupAsDouble<expires-30){done(true);yield break;}
            ProfileVault.Secrets secrets=null;try{secrets=ProfileVault.Load(profile.localId);}catch{Signal("Usa tu código para recuperar el acceso de este perfil.");done(false);yield break;}
            bool existing=!string.IsNullOrEmpty(secrets.refreshToken);OnlineReply response=null;
            yield return Call(existing?"/auth/v1/token?grant_type=refresh_token":"/auth/v1/signup",existing?(object)new Refresh{refresh_token=secrets.refreshToken}:"{}",r=>response=r,false);
            if(response==null||!response.Ok){Signal(existing&&response?.status==400?"El acceso necesita recuperación; tus datos locales se conservan.":"Sin conexión. Tus resultados siguen guardados en este equipo.");done(false);yield break;}
            Auth auth=null;try{auth=JsonUtility.FromJson<Auth>(response.body);if(string.IsNullOrEmpty(auth.access_token)||string.IsNullOrEmpty(auth.refresh_token)||auth.user==null)throw new InvalidDataException();secrets.refreshToken=auth.refresh_token;ProfileVault.Save(profile.localId,secrets);}
            catch{Signal("No se pudo guardar el acceso de forma segura.");done(false);yield break;}
            profile.authUserId=auth.user.id;ParticipantProfiles.SaveInfo();token=auth.access_token;sessionOwner=profile.localId;expires=Time.realtimeSinceStartupAsDouble+auth.expires_in;done(true);
        }
        IEnumerator Worker()
        {
            while(true)
            {
                yield return new WaitForSecondsRealtime(.5f);
                if(!Configured||(ProfileStorage.IsValidation&&Transport is UnityOnlineTransport)||Time.realtimeSinceStartupAsDouble<retryAt)continue;
                if(recoveryRequest!=null){yield return RecoverPending();continue;}
                var profile=ParticipantProfiles.Current;if(profile==null||!profile.consent)continue;int version=generation;bool authenticated=false;
                yield return Authenticate(profile,ok=>authenticated=ok);if(version!=generation)continue;if(!authenticated){Backoff();continue;}
                if(!profile.registered)
                {
                    var secrets=ProfileVault.Load(profile.localId);OnlineReply registered=null;
                    yield return Call("/rest/v1/rpc/rw_register_profile",new Register{p_alias=profile.alias,p_recovery_code=secrets.recoveryCode},r=>registered=r);
                    if(version!=generation)continue;if(registered==null||!registered.Ok){Fail(registered);continue;}
                    if(!ApplyProfile(profile,registered)){Backoff();continue;}ParticipantProfiles.SaveInfo();
                }
                if(profile.pendingAlias)
                {
                    string requested=profile.alias;OnlineReply renamed=null;yield return Call("/rest/v1/rpc/rw_set_alias",new Alias{p_alias=requested},r=>renamed=r);
                    if(version!=generation)continue;if(renamed==null||!renamed.Ok){Fail(renamed);continue;}
                    if(profile.alias==requested){ApplyProfile(profile,renamed);profile.pendingAlias=false;ParticipantProfiles.SaveInfo();}
                }
                if(requests.Count>0)
                {
                    var chart=requests.Dequeue();OnlineReply result=null;yield return Call("/rest/v1/rpc/rw_get_board",new BoardRequest{p_chart_id=chart.id,p_revision=chart.revision},r=>result=r);
                    if(version!=generation)continue;
                    if(result!=null&&result.Ok){try{var board=JsonUtility.FromJson<GlobalBoard>(result.body);if(board?.entries==null)throw new InvalidDataException();if(board.own!=null&&board.own.place<=0)board.own=null;boards[chart.id+":"+chart.revision]=board;Signal("Clasificación global actualizada.");}catch{Signal("No se pudo leer la clasificación global.");}}
                    else{requests.Enqueue(chart);Fail(result);continue;}
                }
                bool failed=false;
                foreach(string path in PrivateStudyStore.Pending(profile.localId).Take(10))
                {
                    UploadEnvelope entry=null;try{entry=JsonUtility.FromJson<UploadEnvelope>(File.ReadAllText(path));}catch{Signal("Hay un registro local que requiere revisión; se conserva.");failed=true;break;}
                    OnlineReply response=null;
                    if(entry.kind=="attempt")yield return Call("/rest/v1/rpc/rw_submit_attempt",new AttemptRequest{p_event=entry.attempt},r=>response=r);
                    else if(entry.kind=="soc")yield return Call("/rest/v1/rpc/rw_submit_soc",new SocRequest{p_event=entry.soc},r=>response=r);
                    else{Signal("Registro local desconocido; se conserva.");failed=true;break;}
                    if(version!=generation){failed=true;break;}
                    if(response==null||!response.Ok){if((response?.status==400||response?.status==422)&&response.body?.Contains("Rate limited")!=true){PrivateStudyStore.RetainRejected(profile.localId,path);Signal("Un registro necesita revisión y se conserva en este equipo.");continue;}Fail(response);failed=true;break;}
                    PrivateStudyStore.Acknowledge(profile.localId,path);
                }
                if(!failed&&version==generation){failures=0;retryAt=Time.realtimeSinceStartupAsDouble+5;Signal("Perfil conectado · registros privados sincronizados.");}
            }
        }
        bool ApplyProfile(ParticipantProfile p,OnlineReply response)
        {
            try{var data=JsonUtility.FromJson<ProfileReply>(response.body);if(!Guid.TryParse(data.participantId,out _))return false;p.participantId=data.participantId;p.alias=data.alias;p.tag=data.tag;p.registered=true;return true;}catch{return false;}
        }
        IEnumerator RecoverPending()
        {
            string code=recoveryRequest;var callback=recoveryDone;recoveryRequest=null;recoveryDone=null;ParticipantProfile draft=null;
            try{draft=ParticipantProfiles.BeginRecovery(code);}catch{callback?.Invoke(false,"No se pudo preparar la recuperación.");yield break;}
            token=null;sessionOwner=null;bool ok=false;yield return Authenticate(draft,result=>ok=result);
            if(!ok){callback?.Invoke(false,"No se pudo conectar. Reintenta la recuperación.");yield break;}
            OnlineReply response=null;yield return Call("/rest/v1/rpc/rw_restore_profile",new Restore{p_recovery_code=code},r=>response=r);
            if(response==null||!response.Ok||!ApplyProfile(draft,response)){token=null;sessionOwner=null;callback?.Invoke(false,"No se pudo recuperar el perfil. Revisa el código y la conexión.");yield break;}
            ParticipantProfiles.CommitRecovery(draft);
            // ActiveChanged intentionally clears auth caches. Authenticate again before reading own progress.
            yield return Authenticate(draft,result=>ok=result);
            if(ok){OnlineReply progress=null;yield return Call("/rest/v1/rpc/rw_my_soc_progress","{}",r=>progress=r);if(progress?.Ok==true){try{var imported=JsonUtility.FromJson<SocProgress>(progress.body);RhythmWare.Education.SocLearningProgress.ImportOwnProgress(imported.attempts);}catch{Signal("Perfil recuperado; el progreso SOC se podrá recuperar al reintentar.");}}}
            callback?.Invoke(true,"Perfil recuperado. Tus mejores puntuaciones siguen en la clasificación global.");
        }
        void Backoff(){failures++;retryAt=Time.realtimeSinceStartupAsDouble+Math.Min(300,Math.Pow(2,Math.Min(failures,8))*2);}
        void Fail(OnlineReply reply){if(reply?.status==401){token=null;sessionOwner=null;}Signal(reply?.status>=400&&reply.status<500?"El servidor no aceptó un registro; se conserva para reintentar.":"Sin conexión. Tus resultados siguen guardados en este equipo.");Backoff();}
    }
}
