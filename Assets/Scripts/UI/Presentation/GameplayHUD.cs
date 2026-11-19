using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RhythmWare.Core;
using RhythmWare.Rhythm;

namespace RhythmWare.UI
{
    public sealed class GameplayHUD : MonoBehaviour
    {
        TextMeshProUGUI score,accuracy,combo,judgement,integrity,overclock,healthValue,chargeValue,rescueStatus,speedNotice;
        float shownSpeed,speedAge=10;
        TextMeshProUGUI volumeNotice;Image volumeFill;CanvasGroup volumeToast;
        float shownMaster,shownEffects,volumeAge=10;
        Image progress;
        CombatPanelGraphic healthMeter,chargeMeter;
        float judgementAge=10,comboPunch,displayedCharge,chargePunch,dischargeAge=10;
        int targetCharge;
        bool overclockReady;
        public void Build()
        {
            score=Type(transform,"00000000",new Vector2(.76f,.94f),new Vector2(.977f,.99f),45);score.alignment=TextAlignmentOptions.MidlineRight;
            var track=CyberUI.Box(transform,"SongProgress",new Vector2(.337f,.947f),new Vector2(.73f,.951f),CyberUI.Raised);
            progress=CyberUI.Box(track.transform,"Fill",Vector2.zero,Vector2.one,EpicStageLayout.LaneColor(0));
            accuracy=Type(transform,"100% / S+",new Vector2(.83f,.895f),new Vector2(.977f,.930f),20);accuracy.alignment=TextAlignmentOptions.MidlineRight;
            combo=Type(transform,"",new Vector2(.022f,.225f),new Vector2(.19f,.325f),57);combo.alignment=TextAlignmentOptions.Center;
            judgement=Type(transform,"",new Vector2(.022f,.192f),new Vector2(.19f,.235f),28);judgement.alignment=TextAlignmentOptions.Center;
            var left=CombatPanelGraphic.Create(transform,"FirewallPlate",new Vector2(.012f,.918f),new Vector2(.30f,.982f),EpicStageLayout.LaneColor(0));
            integrity=Type(left.transform,"FIREWALL",new Vector2(.04f,.08f),new Vector2(.44f,.94f),28);
            healthMeter=CombatPanelGraphic.Create(left.transform,"FirewallFill",new Vector2(.455f,.17f),new Vector2(.965f,.83f),EpicStageLayout.LaneColor(0),true);
            healthValue=Type(healthMeter.transform,"100%",Vector2.zero,Vector2.one,20);healthValue.alignment=TextAlignmentOptions.Center;healthValue.outlineColor=CyberUI.Background;healthValue.outlineWidth=.2f;
            var right=CombatPanelGraphic.Create(transform,"OverclockPlate",new Vector2(.70f,.023f),new Vector2(.988f,.078f),EpicStageLayout.LaneColor(2));
            overclock=Type(right.transform,"OVERCLOCK",new Vector2(.045f,.08f),new Vector2(.53f,.94f),26);
            chargeMeter=CombatPanelGraphic.Create(right.transform,"OverclockFill",new Vector2(.535f,.17f),new Vector2(.96f,.83f),EpicStageLayout.LaneColor(2),true);chargeMeter.Fill=0;
            chargeValue=Type(chargeMeter.transform,"0 / 20",Vector2.zero,Vector2.one,19);chargeValue.alignment=TextAlignmentOptions.Center;chargeValue.outlineColor=CyberUI.Background;chargeValue.outlineWidth=.2f;
            rescueStatus=Type(transform,"",new Vector2(.73f,.001f),new Vector2(.978f,.022f),14);rescueStatus.alignment=TextAlignmentOptions.MidlineRight;
            speedNotice=Type(transform,"",new Vector2(.34f,.895f),new Vector2(.73f,.93f),18);speedNotice.alignment=TextAlignmentOptions.Center;shownSpeed=GameSettings.NoteSpeed;
            var volumePanel=CyberUI.Box(transform,"GameplayVolumeToast",new Vector2(.34f,.825f),new Vector2(.73f,.885f),new Color(.045f,.025f,.10f,.94f));
            volumeToast=volumePanel.gameObject.AddComponent<CanvasGroup>();volumeToast.blocksRaycasts=false;volumeToast.interactable=false;volumeToast.alpha=0;
            volumeNotice=Type(volumePanel.transform,"",new Vector2(.025f,.29f),new Vector2(.975f,.98f),20);volumeNotice.alignment=TextAlignmentOptions.Center;
            var volumeTrack=CyberUI.Box(volumePanel.transform,"VolumeTrack",new Vector2(.04f,.10f),new Vector2(.96f,.20f),CyberUI.Raised);
            volumeFill=CyberUI.Box(volumeTrack.transform,"VolumeLevel",Vector2.zero,Vector2.one,CandyMenuGraphic.Mint);
            shownMaster=GameSettings.MasterVolume;shownEffects=GameSettings.SFXVolume;
        }
        static TextMeshProUGUI Type(Transform parent,string value,Vector2 min,Vector2 max,float size)
        {
            var label=CyberUI.Label(parent,value,min,max,size);label.fontStyle=FontStyles.Bold|FontStyles.Italic;
            label.textWrappingMode=TextWrappingModes.NoWrap;label.margin=Vector4.zero;
            label.rectTransform.offsetMin=Vector2.zero;label.rectTransform.offsetMax=Vector2.zero;return label;
        }
        public void Health(float current,float max)
        {
            float ratio=Mathf.Clamp01(current/Mathf.Max(1,max));healthValue.text=$"{ratio*100:0}%";healthMeter.Fill=ratio;
            integrity.color=ratio>.25f?CandyMenuGraphic.Cream:CyberUI.Red;
        }
        public void Stats(int points,int chain)
        {
            score.text=points.ToString("D8");combo.text=chain>0?$"{chain}<size=28> COMBO</size>":"";comboPunch=1;
            var gm=GameManager.Instance;int total=gm.PerfectHits+gm.GoodHits+gm.MissHits;
            float acc=total==0?100:(gm.PerfectHits+.5f*gm.GoodHits)*100/total;
            accuracy.text=$"{acc:F2}% / {GameplayUI.GetSimpleRank(acc,gm.MissHits)}";
        }
        public void Judge(HitRating rating)
        {
            judgement.text=rating.ToString().ToUpperInvariant();judgement.color=rating==HitRating.Perfect?EpicStageLayout.LaneColor(0):rating==HitRating.Good?new Color(1,.85f,.35f):CyberUI.Red;judgementAge=0;
        }
        public void Overclock(int count,bool ready,bool used=false)
        {
            if(count>targetCharge||ready&&!overclockReady)chargePunch=1;
            if(used){dischargeAge=0;chargePunch=1;}
            targetCharge=Mathf.Clamp(count,0,GameManager.OverclockMaximum);overclockReady=ready;
            rescueStatus.text=GameManager.Instance!=null&&GameManager.Instance.HasAchievedOverclock&&!GameManager.Instance.HasUsed2FARescue?"RESCATE SOC DISPONIBLE":"";
        }
        public void Corrupt(string value){score.text=value;combo.text="? COMBO";accuracy.text="??.??%";}
        void Update()
        {
            float master=GameSettings.MasterVolume,effects=GameSettings.SFXVolume;
            if(Mathf.Abs(master-shownMaster)>.0001f||Mathf.Abs(effects-shownEffects)>.0001f)
            {
                bool fx=Mathf.Abs(effects-shownEffects)>.0001f;float value=fx?effects:master;volumeAge=0;
                volumeNotice.text=(fx?"EFECTOS ":"VOLUMEN GENERAL ")+(value*100).ToString("0")+"%"+(value<=.001f?" / SILENCIO":"")+(fx?" · SHIFT + RUEDA":" · RUEDA / SHIFT: EFECTOS");
                volumeFill.rectTransform.anchorMax=new Vector2(value,1);shownMaster=master;shownEffects=effects;
            }
            volumeAge+=Time.unscaledDeltaTime;volumeToast.alpha=Mathf.Clamp01(3-volumeAge);
            float currentSpeed=GameSettings.NoteSpeed;if(Mathf.Abs(currentSpeed-shownSpeed)>.001f){shownSpeed=currentSpeed;speedAge=0;speedNotice.text="NOTAS "+currentSpeed.ToString("0.0")+"×  /  F3 −   F4 +";}speedAge+=Time.unscaledDeltaTime;speedNotice.alpha=Mathf.Clamp01(3-speedAge);
            var chart=ChartManager.Instance?.SelectedChart;
            if(AudioManager.Instance!=null&&AudioManager.Instance.Duration>0)progress.rectTransform.anchorMax=new Vector2(Mathf.Clamp01((float)(AudioManager.Instance.SongTime/NoteSpawner.Instance.PlayableDuration)),1);
            judgementAge+=Time.unscaledDeltaTime;comboPunch=Mathf.MoveTowards(comboPunch,0,Time.unscaledDeltaTime*7);
            bool reduced=VisualPreferences.ReducedEffects;
            var infection=RhythmWare.Malware.MalwareEffectManager.Instance?.Infections;
            bool leak=infection!=null&&infection.Active(RhythmWare.Malware.InfectionKind.Spyware);
            bool vulnerable=infection!=null&&infection.Active(RhythmWare.Malware.InfectionKind.Trojan);
            bool warning=infection!=null&&infection.ActiveCount>0;
            // Keep combo/judgment feedback clear of the enlarged infection card.
            combo.rectTransform.anchorMin=warning?new Vector2(.022f,.752f):new Vector2(.022f,.225f);
            combo.rectTransform.anchorMax=warning?new Vector2(.16f,.777f):new Vector2(.19f,.325f);
            combo.fontSize=warning?26:57;
            judgement.rectTransform.anchorMin=warning?new Vector2(.175f,.752f):new Vector2(.022f,.192f);
            judgement.rectTransform.anchorMax=warning?new Vector2(.29f,.777f):new Vector2(.19f,.235f);
            judgement.fontSize=warning?24:28;
            integrity.text=leak?"FUGA / −"+infection.LeakPerSecond.ToString("0.#")+"/s":vulnerable?"VULNERABLE":"FIREWALL";
            integrity.color=leak?new Color(.80f,.62f,1):vulnerable?new Color(1,.40f,.65f):GameManager.Instance.CurrentHealth<=25?CyberUI.Red:CandyMenuGraphic.Cream;
            healthMeter.SetAccent(leak?new Color(.72f,.48f,1):vulnerable?new Color(1,.35f,.66f):EpicStageLayout.LaneColor(0));
            combo.transform.localScale=Vector3.one*(reduced?1:1+.04f*comboPunch);judgement.alpha=Mathf.Clamp01(1-judgementAge/.38f);
            displayedCharge=reduced?targetCharge:Mathf.MoveTowards(displayedCharge,targetCharge,Time.unscaledDeltaTime*48);
            chargeMeter.Fill=displayedCharge/GameManager.OverclockMaximum;chargeValue.text=overclockReady?"READY":$"{targetCharge} / 20";
            chargePunch=Mathf.MoveTowards(chargePunch,0,Time.unscaledDeltaTime*6);dischargeAge+=Time.unscaledDeltaTime;
            overclock.color=dischargeAge<.4f?EpicStageLayout.LaneColor(2):CandyMenuGraphic.Cream;
            overclock.transform.localScale=Vector3.one*(reduced?1:1+.025f*chargePunch);
        }
    }
}
