using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RhythmWare.Core;
using RhythmWare.Rhythm;
using RhythmWare.UI;

namespace RhythmWare.Boss
{
    public sealed class BonziStageController : MonoBehaviour
    {
        public static BonziStageController Instance { get; private set; }
        public bool IsEncounter { get; private set; }
        public bool IsContained => Integrity<=.001f;
        public float Integrity { get; private set; }=1;
        public int Phase { get; private set; }
        public string Animation { get; private set; }="idle";
        public double AnimationElapsed { get; private set; }
        public int AnimationFrame { get; private set; }
        public int AvailableAnimations=>animations!=null?animations.Names.Count():0;
        public string DialogueText => dialogue.Text;
        public bool IsSpeaking => speech!=null&&speech.activeSelf;
        readonly BonziDialogue dialogue=new BonziDialogue();
        BonziAnimations animations;
        BonziPerformance performance;
        EpicStagePresenter stage;
        SpriteRenderer actor;
        GameObject hud;
        TextMeshProUGUI heading,protocol;
        Image health;
        GameObject speech;
        TextMeshProUGUI speechText;
        CanvasGroup speechOpacity;
        bool wasReviving;
        double attackAt=-100,hurtAt=-100,containedAt=-1;
        int targetHits;
        int successfulHits;double lastAttackTime;
        readonly SpriteRenderer[] waves=new SpriteRenderer[3];
        void Awake() { Instance=this; }
        void Start()
        {
            animations=new BonziAnimations();performance=new BonziPerformance(animations.Duration);stage=FindAnyObjectByType<EpicStagePresenter>(FindObjectsInactive.Include);
            if(stage!=null)actor=stage.BossAnchor.GetComponent<SpriteRenderer>();
            if(stage!=null)for(int i=0;i<waves.Length;i++)
            {
                var go=new GameObject("BonziAttackWave"+i);go.transform.SetParent(stage.EmissionOrigin,false);
                waves[i]=go.AddComponent<SpriteRenderer>();waves[i].sprite=NetworkVisualAssets.Shape(10);waves[i].sortingOrder=4;waves[i].enabled=false;
            }
            var canvas=FindAnyObjectByType<Canvas>();
            if(canvas!=null)
            {
                var box=CombatPanelGraphic.Create(canvas.transform,"BonziEncounterHUD",new Vector2(.02f,.785f),new Vector2(.295f,.91f),new Color(.82f,.53f,1));hud=box.gameObject;
                heading=CyberUI.Label(box.transform,"BONZI BUDDY",new Vector2(.045f,.40f),new Vector2(.96f,.99f),32);heading.fontStyle=FontStyles.Bold|FontStyles.Italic;
                protocol=CyberUI.Label(box.transform,"",new Vector2(.045f,.02f),new Vector2(.96f,.25f),16);protocol.enableAutoSizing=true;protocol.fontSizeMin=13;protocol.fontSizeMax=16;
                var track=CyberUI.Box(box.transform,"CoreTrack",new Vector2(.05f,.27f),new Vector2(.95f,.40f),CyberUI.Raised);
                health=CyberUI.Box(track.transform,"Integrity",Vector2.zero,Vector2.one,new Color(1,.34f,.70f));hud.SetActive(false);
                var bubble=CombatPanelGraphic.Create(canvas.transform,"BonziSpeechBubble",new Vector2(.72f,.755f),new Vector2(.98f,.885f),new Color(.82f,.53f,1));speech=bubble.gameObject;
                speechOpacity=speech.AddComponent<CanvasGroup>();speechOpacity.blocksRaycasts=false;speechOpacity.interactable=false;
                CyberUI.Box(bubble.transform,"SpeakerAccent",new Vector2(0,0),new Vector2(.012f,1),CyberUI.Magenta);
                var speaker=CyberUI.Label(bubble.transform,"BONZI BUDDY / MENSAJE ENTRANTE",new Vector2(.045f,.74f),new Vector2(.97f,.98f),17,CyberUI.Magenta);speaker.fontStyle=FontStyles.Bold;
                speechText=CyberUI.Label(bubble.transform,"",new Vector2(.045f,.07f),new Vector2(.96f,.76f),25);
                speechText.enableAutoSizing=true;speechText.fontSizeMin=20;speechText.fontSizeMax=25;speech.SetActive(false);
            }
            NoteSpawner.Instance.OnChartLoaded+=Configure;NoteSpawner.Instance.OnNoteSpawned+=Launch;
            GameManager.Instance.OnNoteHit+=Hit;GameManager.Instance.OnStateChanged+=State;
            GameManager.Instance.OnOverclockChanged+=Overclock;
        }
        void OnDestroy()
        {
            if(NoteSpawner.Instance!=null){NoteSpawner.Instance.OnChartLoaded-=Configure;NoteSpawner.Instance.OnNoteSpawned-=Launch;}
            if(GameManager.Instance!=null){GameManager.Instance.OnNoteHit-=Hit;GameManager.Instance.OnStateChanged-=State;GameManager.Instance.OnOverclockChanged-=Overclock;}
            animations?.Dispose();if(hud!=null)Destroy(hud);if(speech!=null)Destroy(speech);if(Instance==this)Instance=null;
        }
        void Configure(ChartData chart)
        {
            IsEncounter=chart!=null&&chart.bossId==BonziEncounter.Id;
            Integrity=1;Phase=1;attackAt=hurtAt=-100;containedAt=-1;
            dialogue.Reset();performance?.Reset();wasReviving=false;if(speech!=null)speech.SetActive(false);
            BackgroundManager.Instance?.BossDesktop?.ResetEncounter();
            targetHits=chart==null?1:Mathf.Max(1,Mathf.CeilToInt(chart.notes.Sum(n=>n.isHoldNote?2:1)*.65f));
            successfulHits=0;lastAttackTime=chart!=null&&chart.notes.Count>0?chart.notes.Max(n=>n.endTimestamp):1;
            if(actor!=null){actor.color=Color.white;if(IsEncounter&&animations.Available)actor.sharedMaterial=animations.Material;}
            if(stage!=null){if(IsEncounter&&animations.Available)SetFrame("idle",0,true);else stage.SetBossSprite(null);stage.SetBossMotion(Vector2.zero,0);}
            if(hud!=null)hud.SetActive(false);
        }
        void State(GameState state)
        {
            if(state==GameState.Reviving)wasReviving=true;
            else if(state==GameState.Playing&&wasReviving){dialogue.Revived();performance?.Revived(AudioManager.Instance.SongTime);wasReviving=false;}
            else if(state!=GameState.Paused&&state!=GameState.RescueQuestion)wasReviving=false;
            bool visible=IsEncounter&&(state==GameState.Playing||state==GameState.Paused||state==GameState.RescueQuestion||state==GameState.Reviving||state==GameState.GameOver);
            if(hud!=null)hud.SetActive(visible&&state==GameState.Playing);
            if(speech!=null&&state!=GameState.Playing)speech.SetActive(false);
            if(!visible&&stage!=null)stage.SetBossSprite(null);
            foreach(var wave in waves)if(wave!=null)wave.enabled=visible&&!VisualPreferences.ReducedEffects;
            if(visible)RefreshPresentation(AudioManager.Instance.SongTime);
        }
        void Launch(NoteData note)
        {
            if(!IsEncounter||IsContained||GameManager.Instance.CurrentState!=GameState.Playing)return;
            attackAt=AudioManager.Instance.SongTime;
            performance?.Attack(note.malwareData!=null?note.malwareData.GetType().Name.Replace("Data",""):"Default",attackAt);
            if(note.malwareData!=null)
            {
                string threat=note.malwareData.GetType().Name.Replace("Data","");
                dialogue.Attack(threat,attackAt);BackgroundManager.Instance?.BossDesktop?.Attack(threat);
            }
        }
        void Overclock(int charge,bool active,bool discharged)
        { if(IsEncounter&&GameManager.Instance.CurrentState==GameState.Playing){if(active)dialogue.Overclock();if(!IsContained)performance?.Overclock(active,AudioManager.Instance.SongTime);BackgroundManager.Instance?.BossDesktop?.Overclock(active);} }
        void Hit(HitRating rating,int lane)
        {
            if(!IsEncounter||IsContained||GameManager.Instance.CurrentState!=GameState.Playing)return;
            if(rating==HitRating.Miss){dialogue.Miss(AudioManager.Instance.SongTime);performance?.Miss(AudioManager.Instance.SongTime);return;}
            successfulHits++;UpdateIntegrity(AudioManager.Instance.SongTime);hurtAt=AudioManager.Instance.SongTime;
            performance?.Hit(hurtAt);
            BackgroundManager.Instance?.BossDesktop?.Counter();
            if(IsContained)containedAt=hurtAt;
        }
        void Update() { if(IsEncounter&&GameManager.Instance.CurrentState==GameState.Playing)RefreshPresentation(AudioManager.Instance.SongTime); }
        public void RefreshPresentation(double songTime)
        {
            if(IsEncounter&&!IsContained)UpdateIntegrity(songTime);
            if(IsEncounter&&GameManager.Instance.CurrentState==GameState.Playing)
            {
                dialogue.Refresh(songTime,BonziEncounter.PhaseAt(songTime,AudioManager.Instance.Duration),Integrity);
                RefreshSpeech(songTime);
            }
            if(!IsEncounter||stage==null||animations==null||!animations.Available)return;
            Phase=BonziEncounter.PhaseAt(songTime,AudioManager.Instance.Duration);
            BackgroundManager.Instance?.BossDesktop?.Status(Phase,Integrity,IsContained);
            performance.Tick(songTime,dialogue.VisibleAt(songTime),dialogue.StartedAt,Phase,IsContained);
            Animation=performance.Current;AnimationElapsed=VisualPreferences.ReducedEffects?0:Math.Max(0,songTime-performance.StartedAt);
            BackgroundManager.Instance?.BossDesktop?.ActorAction(Animation);
            SetFrame(Animation,AnimationElapsed,performance.Loop);
            bool reduced=VisualPreferences.ReducedEffects;
            float recoil=reduced?0:Mathf.Sin(Mathf.Clamp01((float)(songTime-hurtAt)/.20f)*Mathf.PI);
            stage.SetBossMotion(reduced?Vector2.zero:new Vector2(-4*recoil,0),recoil*.025f);
            float alpha=containedAt>=0?Mathf.Clamp01(1-(float)((songTime-containedAt)/(animations.Duration("defeat")+.35))):1;
            actor.color=new Color(1,1,1,alpha);
            for(int i=0;i<waves.Length;i++)if(waves[i]!=null)
            {
                float age=(float)(songTime-attackAt),p=Mathf.Clamp01(age/(.50f+i*.13f));
                float size=.35f+p*(Phase==3?1.5f:.85f);waves[i].transform.localScale=Vector3.one*size;
                Color tint=Phase==1?new Color(.76f,.4f,1):Phase==2?CyberUI.Magenta:CyberUI.Cyan;tint.a=(1-p)*.5f;
                waves[i].color=tint;waves[i].enabled=!VisualPreferences.ReducedEffects&&!IsContained&&age>=0&&age<.9;
            }
            if(heading!=null)
            {
                heading.text=IsContained?"BONZI BUDDY\n<size=21>NÚCLEO AISLADO</size>":$"BONZI BUDDY\n<size=21>NÚCLEO {Mathf.CeilToInt(Integrity*100)}%</size>";
                health.rectTransform.anchorMax=new Vector2(Integrity,1);
                protocol.text=IsContained?"Mantén el firewall hasta el final.":Phase==1?"01 / FALSA AMISTAD · SPYWARE":Phase==2?"02 / PUBLICIDAD INVASIVA · ADWARE":"03 / ÚLTIMA BRECHA · TROYANOS";
            }
        }
        void UpdateIntegrity(double time)
        {
            // Successful defense and surviving the complete attack are both
            // required, so the last malware phase cannot lose its boss early.
            float remainingAttack=time<lastAttackTime?Mathf.Max(.002f,1-Mathf.Clamp01((float)(time/Math.Max(.1,lastAttackTime)))):0;
            Integrity=Mathf.Max(remainingAttack,Mathf.Clamp01(1-successfulHits/(float)targetHits));
            if(Integrity<=.001f&&containedAt<0)containedAt=time;
        }
        void SetFrame(string clip,double elapsed,bool loop)
        {
            var frame=animations.Frame(clip,elapsed,loop);AnimationFrame=animations.FrameNumber(clip,elapsed,loop);
            if(animations.UsesExtendedAtlas)stage.SetBossPose(frame,animations.VisibleBounds(clip),animations.SourcePivot);
            else stage.SetBossSprite(frame,425);
        }
        void RefreshSpeech(double time)
        {
            if(speech==null)return;
            bool speaking=dialogue.VisibleAt(time);speech.SetActive(speaking);if(!speaking)return;
            if(speechText.text!=dialogue.Text)speechText.text=dialogue.Text;
            speechText.maxVisibleCharacters=VisualPreferences.ReducedEffects?int.MaxValue:Mathf.Max(0,(int)((time-dialogue.StartedAt)*55));
            speechOpacity.alpha=VisualPreferences.ReducedEffects?1:Mathf.Min(Mathf.Clamp01((float)(time-dialogue.StartedAt)/.12f),Mathf.Clamp01((float)(dialogue.Until-time)/.25f));
        }
    }
}
