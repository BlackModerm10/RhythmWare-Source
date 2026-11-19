using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using RhythmWare.Core;
using RhythmWare.Rhythm;
using RhythmWare.Malware;

namespace RhythmWare.UI
{
    public sealed class TutorialGuide : MonoBehaviour
    {
        public static TutorialGuide Instance { get; private set; }
        public bool IsTraining { get; private set; }
        public bool NeedsGuidedPractice=>requested||IsTraining;
        public int CurrentLesson { get; private set; }
        public bool CardOpen { get; private set; }
        public bool Observing { get; private set; }
        public bool Recovery { get; private set; }
        public int RemainingTargets { get; private set; }
        ChartData song,practice;AudioClip pulse;
        GameObject panel,curtain;RectTransform rect;CanvasGroup opacity;
        TextMeshProUGUI heading,body,footer,stepText;Button confirm,skip,repeat;
        readonly TextMeshProUGUI[] labels=new TextMeshProUGUI[5];
        readonly MalwareData[] threats=new MalwareData[3];
        Action next;string text,title;float age;double finishAt=-1;int misses,lastConfirm=-1;bool requested,oldRescue,rescuing;
        const double Beat=60.0/126;
        Button speedDown,speedUp;TextMeshProUGUI speedLabel;
        static readonly string[] titles={"EL PULSO EMPIEZA CONTIGO","COMPLETA LAS COLUMNAS LATERALES","EL CENTRO ES DORADO","MANTÉN HASTA EL FINAL","ACIERTO, COMBO Y FIREWALL","SPYWARE","ADWARE","TROYANO","ACTIVA OVERCLOCK","TU RESCATE SOC","ENCUENTRA TU VELOCIDAD"};
        static readonly string[] lessonText={"Pulsa {0} y {2} cuando el centro del rombo llegue a la línea inferior. Escucha el pulso: no pulses al primer contacto de los bordes.","Ahora {1} a la izquierda y {3} a la derecha. Las cinco columnas llegan a la misma línea: {0}, {1}, {4}, {2}, {3}.","La nota dorada viaja centrada. Pulsa {4}. En esta práctica no necesitas combinarla con otra tecla.","Mantén {0} mientras pasa la cola. Suelta cuando termine; no basta con acertar la cabeza.","Perfect y Good suman combo y reparan firewall. Los MISS rompen el combo y dañan la barra. Aquí puedes practicar sin perder.","Spyware recoge datos sin autorización. Revisa permisos y fuentes; ante una fuga, aísla y avisa a SOC/TI. Dejaremos pasar un paquete de ejemplo para mostrar el robo de firewall. Después podrás limpiarlo.","Adware muestra publicidad no deseada. Usa fuentes oficiales y rechaza extras del instalador. En el juego sus anuncios bloquean la reparación: observa la infección y después neutraliza otro Adware.","Un troyano puede disfrazarse de utilidad legítima: verifica su origen antes de ejecutarlo. Dejaremos pasar uno y un segundo paquete de prueba para mostrar el daño extra. Después cerrarás la brecha.","OVERCLOCK necesita 20 Perfect sin MISS. Te preparamos 17/20: consigue los tres restantes. Los Good mantienen la carga.","Tras lograr OVERCLOCK puedes responder una pregunta SOC al perder. Una respuesta correcta devuelve 30% de firewall y prepara tres segundos antes de volver."};
        void Awake(){Instance=this;}
        void Start()
        {
            var canvas=FindAnyObjectByType<Canvas>();
            curtain=CyberUI.Box(canvas.transform,"TutorialCurtain",Vector2.zero,Vector2.one,new Color(.025f,.012f,.07f,.80f)).gameObject;
            curtain.GetComponent<Image>().raycastTarget=true;
            var box=CombatPanelGraphic.Create(canvas.transform,"GuidedTutorialCard",new Vector2(.18f,.235f),new Vector2(.82f,.785f),CandyMenuGraphic.Mint);panel=box.gameObject;rect=box.rectTransform;opacity=panel.AddComponent<CanvasGroup>();
            stepText=CyberUI.Label(rect,"",new Vector2(.12f,.84f),new Vector2(.88f,.94f),18,CyberUI.Cyan);
            heading=CyberUI.Label(rect,"",new Vector2(.12f,.66f),new Vector2(.88f,.82f),34);heading.enableAutoSizing=true;heading.fontSizeMin=20;
            body=CyberUI.Label(rect,"",new Vector2(.12f,.36f),new Vector2(.88f,.65f),25);body.enableAutoSizing=true;body.fontSizeMin=16;
            for(int row=0;row<5;row++){int lane=LaneLayout.VisualOrder[row];float x=.125f+row*.153f;var key=CombatPanelGraphic.Create(rect,"TutorialKey"+lane,new Vector2(x,.22f),new Vector2(x+.138f,.34f),EpicStageLayout.LaneColor(lane));labels[lane]=CyberUI.Label(key.transform,"",Vector2.zero,Vector2.one,20);labels[lane].alignment=TextAlignmentOptions.Center;labels[lane].enableAutoSizing=true;labels[lane].fontSizeMin=11;}
            confirm=CyberUI.Button(rect,"PRACTICAR / ENTER",new Vector2(.125f,.09f),new Vector2(.455f,.205f),Continue);
            skip=CyberUI.Button(rect,"OMITIR PASO",new Vector2(.68f,.09f),new Vector2(.875f,.205f),SkipLesson);
            repeat=CyberUI.Button(rect,"REPETIR",new Vector2(.475f,.09f),new Vector2(.66f,.205f),StartTrial);
            footer=CyberUI.Label(rect,"",new Vector2(.125f,.018f),new Vector2(.875f,.075f),12,CyberUI.Muted);footer.enableAutoSizing=true;footer.fontSizeMin=9;footer.textWrappingMode=TextWrappingModes.NoWrap;
            foreach(var label in new[]{stepText,heading,body,footer}){label.alignment=TextAlignmentOptions.Center;label.rectTransform.offsetMin=Vector2.one*2;label.rectTransform.offsetMax=Vector2.one*-2;}
            heading.fontSizeMax=32;heading.fontSizeMin=22;heading.textWrappingMode=TextWrappingModes.NoWrap;body.fontSizeMax=25;body.fontSizeMin=18;
            footer.fontSizeMax=14;foreach(var label in labels)label.fontSizeMax=28;
            speedDown=CyberUI.Button(rect,"− / F3",new Vector2(.10f,.03f),new Vector2(.32f,.25f),()=>GameSettings.NoteSpeed-=.1f);
            speedUp=CyberUI.Button(rect,"+ / F4",new Vector2(.68f,.03f),new Vector2(.90f,.25f),()=>GameSettings.NoteSpeed+=.1f);
            speedLabel=CyberUI.Label(rect,"",new Vector2(.34f,.03f),new Vector2(.66f,.25f),20,CyberUI.Cyan);speedLabel.alignment=TextAlignmentOptions.Center;
            speedDown.gameObject.SetActive(false);speedUp.gameObject.SetActive(false);speedLabel.gameObject.SetActive(false);
            panel.SetActive(false);curtain.SetActive(false);
            GameManager.Instance.OnStateChanged+=State;NoteSpawner.Instance.OnNoteResolved+=Resolved;
        }
        public void Request(ChartData chart){if(chart?.tutorialId!="sara-basics-5")return;Cancel();song=chart;requested=true;}
        void Update()
        {
            var gm=GameManager.Instance;
            if(requested&&gm.CurrentState==GameState.Playing){requested=false;IsTraining=true;oldRescue=GameManager.Enable2FARescue;CurrentLesson=0;ShowLesson();}
            if(!IsTraining)return;
            if(CardOpen&&(Keyboard.current?.enterKey.wasPressedThisFrame==true||Gamepad.current?.buttonSouth.wasPressedThisFrame==true))Continue();
            if(gm.CurrentState==GameState.TutorialInstruction&&(Keyboard.current?.escapeKey.wasPressedThisFrame==true||Gamepad.current?.startButton.wasPressedThisFrame==true)){FindAnyObjectByType<GameplayUI>(FindObjectsInactive.Include).ExitToMainMenu();return;}
            Animate();
            if(gm.CurrentState==GameState.Playing&&finishAt>=0&&AudioManager.Instance.SongTime>=finishAt)
            {
                finishAt=-1;
                if(Observing){Observing=false;Recovery=true;Show("AHORA TÚ: CONTÉN LA AMENAZA",CurrentLesson==5?"Spyware está robando firewall. Acierta el siguiente Spyware para detener la fuga.":CurrentLesson==6?"Adware impide curarte. Acierta el siguiente Adware para recuperar la reparación.":"El Troyano aumentó el daño del paquete de prueba. Acierta el siguiente Troyano para cerrar la brecha.",StartRecovery);}
                else CompleteTrial();
            }
        }
        void Animate()
        {
            if(panel.activeSelf)
            {
                age+=Time.unscaledDeltaTime;float t=VisualPreferences.ReducedEffects?1:Mathf.Clamp01(age/.32f),e=1-Mathf.Pow(1-t,3);
                opacity.alpha=e;rect.localScale=Vector3.one*(1+(VisualPreferences.ReducedEffects?0:Mathf.Sin(t*Mathf.PI)*.025f));rect.anchoredPosition=VisualPreferences.ReducedEffects?Vector2.zero:new Vector2((1-e)*90,(1-e)*-28);rect.localRotation=Quaternion.Euler(0,0,VisualPreferences.ReducedEffects?0:(1-e)*-3);
                speedLabel.text=GameSettings.NoteSpeed.ToString("0.0")+"×";
                body.text=Format(text);heading.text=title;for(int i=0;i<5;i++)labels[i].text=InputManager.Instance.LabelForLane(i);
                if(CardOpen)confirm.GetComponentInChildren<TextMeshProUGUI>().text="CONTINUAR / "+(InputManager.Instance.UsingGamepad?InputManager.Instance.ConfirmLabel:"ENTER");
            }
        }
        string Format(string value){if(value==null)return "";for(int i=0;i<5;i++)value=value.Replace("{"+i+"}",InputManager.Instance.LabelForLane(i));return value;}
        public void Continue(){if(!CardOpen||age<.20f||lastConfirm==Time.frameCount)return;lastConfirm=Time.frameCount;next?.Invoke();}
        public void RestartCurrent()=>ShowLesson();
        public void SkipLesson(){if(!IsTraining)return;if(CurrentLesson>=titles.Length-1)StartSong();else{CurrentLesson++;ShowLesson();}}
        void ShowLesson(){Recovery=false;Observing=false;rescuing=false;RemainingTargets=0;finishAt=-1;NoteSpawner.Instance.StopAndClear();MalwareEffectManager.Instance.ResetInfections();GameManager.Instance.TrainingSafety=true;Show(titles[CurrentLesson],CurrentLesson==10?"F3 reduce la velocidad y muestra las notas con más anticipación. F4 la aumenta y separa más las notas. La música y el momento de pulsar no cambian. Ejemplo: pasar de 1,0× a 1,5× separa más los mismos ritmos. Practica con seis paquetes y prueba F3/F4 o los botones.":lessonText[CurrentLesson],StartTrial);}
        void Show(string caption,string message,Action action)
        {
            AudioManager.Instance.PauseSong();GameManager.Instance.SetState(GameState.TutorialInstruction);
            CardOpen=true;title=caption;text=message;next=action;age=0;
            speedDown.gameObject.SetActive(false);speedUp.gameObject.SetActive(false);speedLabel.gameObject.SetActive(false);
            rect.anchorMin=new Vector2(.18f,.235f);rect.anchorMax=new Vector2(.82f,.785f);rect.offsetMin=rect.offsetMax=Vector2.zero;
            heading.rectTransform.anchorMin=new Vector2(.12f,.66f);heading.rectTransform.anchorMax=new Vector2(.88f,.82f);
            body.rectTransform.anchorMin=new Vector2(.12f,.36f);body.rectTransform.anchorMax=new Vector2(.88f,.65f);
            stepText.text="ACADEMIA DE CAMPO / "+(CurrentLesson+1)+" DE "+titles.Length;footer.text="Práctica protegida · puedes repetir u omitir · ESC / START: salir";
            stepText.gameObject.SetActive(true);
            confirm.gameObject.SetActive(true);skip.gameObject.SetActive(true);repeat.gameObject.SetActive(true);foreach(var l in labels)l.transform.parent.gameObject.SetActive(true);
            curtain.SetActive(true);panel.SetActive(true);curtain.transform.SetAsLastSibling();panel.transform.SetAsLastSibling();
        }
        void Dock(string message)
        {
            CardOpen=false;curtain.SetActive(false);text=message;title=titles[CurrentLesson];age=0;
            stepText.gameObject.SetActive(false);
            rect.anchorMin=new Vector2(.29f,.755f);rect.anchorMax=new Vector2(.71f,.925f);rect.offsetMin=rect.offsetMax=Vector2.zero;
            heading.rectTransform.anchorMin=new Vector2(.09f,.57f);heading.rectTransform.anchorMax=new Vector2(.91f,.90f);
            body.rectTransform.anchorMin=new Vector2(.09f,.08f);body.rectTransform.anchorMax=new Vector2(.91f,.59f);
            bool speedLesson=CurrentLesson==10;speedDown.gameObject.SetActive(speedLesson);speedUp.gameObject.SetActive(speedLesson);speedLabel.gameObject.SetActive(speedLesson);
            if(speedLesson)body.rectTransform.anchorMin=new Vector2(.09f,.28f);
            confirm.gameObject.SetActive(false);skip.gameObject.SetActive(false);repeat.gameObject.SetActive(false);footer.text="";foreach(var l in labels)l.transform.parent.gameObject.SetActive(false);panel.SetActive(true);
        }
        AudioClip Pulse()
        {
            if(pulse!=null)return pulse;const int hz=16000;var data=new float[hz*30];for(int beat=0;beat*Beat<30;beat++){int at=(int)(beat*Beat*hz);for(int i=0;i<720&&at+i<data.Length;i++)data[at+i]=Mathf.Sin(i*(beat%4==0?950:700)*Mathf.PI*2/hz)*Mathf.Exp(-i/95f)*.26f;}
            pulse=AudioClip.Create("TutorialPulse126",data.Length,1,hz,false);pulse.SetData(data,0);return pulse;
        }
        MalwareData Threat(){int index=CurrentLesson-5;if(threats[index]==null)threats[index]=index==0?ScriptableObject.CreateInstance<SpywareData>():index==1?ScriptableObject.CreateInstance<AdwareData>():ScriptableObject.CreateInstance<TrojanData>();return threats[index];}
        public void StartTrial()
        {
            if(CurrentLesson==9){StartRescue();return;}
            NoteSpawner.Instance.StopAndClear();misses=0;finishAt=-1;RemainingTargets=0;Observing=CurrentLesson>=5&&CurrentLesson<=7;Recovery=false;
            if(practice!=null)Destroy(practice);practice=ScriptableObject.CreateInstance<ChartData>();practice.tutorialId="sara-training-5";practice.songTitle="Práctica guiada";practice.bpm=126;practice.approachSeconds=2.2;practice.timingWindowScale=2;practice.audioClip=Pulse();practice.chartId="tutorial-sandbox";
            double first=Beat*6;
            if(Observing){practice.notes.Add(new NoteData{timestamp=first,laneIndex=CurrentLesson-5,malwareData=Threat(),trainingObserve=true});RemainingTargets=1;if(CurrentLesson==7){practice.notes.Add(new NoteData{timestamp=first+Beat*2,laneIndex=0,trainingObserve=true});RemainingTargets++;}}
            else
            {
                int[] lanes=CurrentLesson==10?new[]{0,2,1,3,4,0}:CurrentLesson==0?new[]{0,2}:CurrentLesson==1?new[]{1,3}:CurrentLesson==2?new[]{4,4}:CurrentLesson==3?new[]{0}:CurrentLesson==4?new[]{0,2,1,3}:new[]{0,2,4};
                for(int i=0;i<lanes.Length;i++)practice.notes.Add(new NoteData{timestamp=first+i*Beat*2,laneIndex=lanes[i],duration=CurrentLesson==3?Beat*2:0});RemainingTargets=lanes.Length;
            }
            ChartManager.Instance.SelectChart(practice);NoteSpawner.Instance.LoadChart(practice);GameManager.Instance.StartGame();GameManager.Instance.TrainingSafety=true;
            if(CurrentLesson==4)GameManager.Instance.TakeDamage(30);
            if(CurrentLesson==8)for(int i=0;i<17;i++)GameManager.Instance.RegisterHit(HitRating.Perfect,i%5);
            AudioManager.Instance.PlaySong(Pulse(),practice.PlaybackLeadIn);InputManager.Instance.FlushHeld();Dock(CurrentLesson==10?"Prueba F3 para más anticipación y F4 para más separación. El golpe musical sigue en el mismo momento.":Observing?"El primer paquete está protegido. Mira el efecto en tu firewall.":lessonText[CurrentLesson]);
        }
        void StartRecovery()
        {
            AudioManager.Instance.ResumeSong();GameManager.Instance.SetState(GameState.Playing);GameManager.Instance.TrainingSafety=true;
            var fx=MalwareEffectManager.Instance;if(CurrentLesson==5)fx.TriggerSpywareGlitch();else if(CurrentLesson==6)fx.TriggerAdwarePopups();else fx.TriggerTrojanPayload();
            double target=Math.Ceiling((AudioManager.Instance.SongTime+GameSettings.NoteTravelSeconds+.1)/Beat)*Beat;
            // Keep the demonstration active until the counterexample can arrive,
            // including the longest reading window chosen by a beginner.
            fx.Infections.Infect((InfectionKind)(CurrentLesson-5),(float)(target-AudioManager.Instance.SongTime+.6));
            RemainingTargets=1;misses=0;NoteSpawner.Instance.QueueTrainingNotes(new[]{new NoteData{timestamp=target,laneIndex=CurrentLesson-5,malwareData=Threat()}});
            InputManager.Instance.FlushHeld();Dock("Ahora sí: acierta el virus con "+"{"+(CurrentLesson-5)+"}"+" y observa cómo desaparece la infección.");
        }
        void Resolved(NoteData note,HitRating rating)
        {
            if(!IsTraining||rescuing||RemainingTargets<=0)return;RemainingTargets--;if(rating==HitRating.Miss&&!note.trainingObserve)misses++;
            if(RemainingTargets==0){finishAt=AudioManager.Instance.SongTime+(Observing?1.5:.25);if(Observing&&CurrentLesson==6)GameManager.Instance.Heal(8);}
        }
        void CompleteTrial()
        {
            if(misses>0||CurrentLesson==8&&!GameManager.Instance.IsOverclockActive){Show("VUELVE A INTENTARLO",CurrentLesson==8?"Busca el Perfect cuando los centros se alineen. Prepararemos de nuevo 17/20; también puedes omitir el paso.":"No has perdido la práctica. Espera al centro del receptor y prueba de nuevo con calma.",StartTrial);return;}
            if(CurrentLesson==10){Show("TU VELOCIDAD ESTÁ LISTA","Usa una velocidad que puedas leer. Se guarda para las siguientes canciones y puedes cambiarla durante la partida. Ahora juega Sara completa.",StartSong);return;}
            Show("BIEN HECHO / MECÁNICA COMPLETADA",Recovery?"La amenaza ha quedado neutralizada. En una canción también caduca por tiempo; acertar otro virus del mismo tipo la limpia antes.":"Has completado el ejercicio. Puedes seguir al siguiente paso o repetir esta práctica desde el tutorial.",()=>{CurrentLesson++;ShowLesson();});
        }
        void StartRescue()
        {
            CardOpen=false;panel.SetActive(false);curtain.SetActive(false);rescuing=true;GameManager.Enable2FARescue=true;
            var gm=GameManager.Instance;gm.ResetStats();gm.SetState(GameState.Playing);gm.TrainingSafety=false;
            for(int i=0;i<20;i++)gm.RegisterHit(HitRating.Perfect,i%5);gm.TakeDamage(1000);
        }
        void State(GameState state)
        {
            if(!IsTraining)return;
            if(rescuing&&state==GameState.Playing&&GameManager.Instance.HasUsed2FARescue){StartCoroutine(AfterRescue(true));return;}
            if(rescuing&&state==GameState.GameOver){StartCoroutine(AfterRescue(false));return;}
            if(state==GameState.MainMenu||state==GameState.Editor){Cancel();}
        }
        IEnumerator AfterRescue(bool success)
        {
            yield return null;if(!IsTraining)yield break;rescuing=false;
            FindAnyObjectByType<GameplayUI>(FindObjectsInactive.Include).DismissPracticeOverlays();GameManager.Instance.TrainingSafety=true;
            Show(success?"RESCATE COMPLETADO / A JUGAR":"UNA RESPUESTA PARA APRENDER",success?"Recuperaste 30% y tuviste tres segundos de preparación. Ya puedes llevar lo aprendido al ritmo de tu mapa Sara.":"En la práctica puedes repetir sin perder. Revisa la explicación SOC o empieza Sara; en una partida real el rescate exige responder correctamente.",()=>{CurrentLesson=10;ShowLesson();});
        }
        public void StartSong()
        {
            var original=song;IsTraining=false;requested=false;rescuing=false;CardOpen=false;GameManager.Instance.TrainingSafety=false;GameManager.Enable2FARescue=oldRescue;panel.SetActive(false);curtain.SetActive(false);
            NoteSpawner.Instance.StopAndClear();ChartManager.Instance.SelectChart(original);NoteSpawner.Instance.LoadChart(original);AudioManager.Instance.PlaySong(original.audioClip,original.PlaybackLeadIn);GameManager.Instance.StartGame();InputManager.Instance.FlushHeld();
        }
        public void Cancel()
        {
            requested=false;if(!IsTraining)return;IsTraining=false;rescuing=false;if(GameManager.Instance!=null)GameManager.Instance.TrainingSafety=false;GameManager.Enable2FARescue=oldRescue;if(AudioManager.Instance!=null)AudioManager.Instance.StopSong();if(NoteSpawner.Instance!=null)NoteSpawner.Instance.StopAndClear();if(song!=null&&ChartManager.Instance!=null)ChartManager.Instance.SelectChart(song);if(panel!=null)panel.SetActive(false);if(curtain!=null)curtain.SetActive(false);
        }
        public void Refresh(double time) { } // Legacy preview callers do not start scripted training.
        void OnDestroy(){Cancel();if(GameManager.Instance!=null)GameManager.Instance.OnStateChanged-=State;if(NoteSpawner.Instance!=null)NoteSpawner.Instance.OnNoteResolved-=Resolved;if(panel!=null)Destroy(panel);if(curtain!=null)Destroy(curtain);if(pulse!=null)Destroy(pulse);if(practice!=null)Destroy(practice);foreach(var threat in threats)if(threat!=null)Destroy(threat);if(Instance==this)Instance=null;}
    }
}
