using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using RhythmWare.Core;

namespace RhythmWare.UI
{
    public sealed class AudioCalibrationUI : MonoBehaviour
    {
        public AudioCalibrationSession Session { get; }=new AudioCalibrationSession();
        public bool Running { get; private set; }
        public float SuggestedOffset=>Session.SuggestedOffset;
        AudioSource source;AudioClip clicks;InputAction tap;Action back;
        TextMeshProUGUI status,result,detail;Button start,apply,test,reset;
        readonly Image[] progress=new Image[AudioCalibrationSession.TargetSamples];
        bool verify,menuSuspended,inputLocked,oldCapture;double verifyOffset;float unlockAt;
        bool welcome;Button leave;
        public void Build(Action close,bool firstRun=false)
        {
            back=close;welcome=firstRun;CandyUI.Backdrop(transform);
            CandyUI.Title(transform,welcome?"BIENVENIDO / JUGUEMOS A TU RITMO":"CALIBRACIÓN / ESCUCHA Y PULSA",new Vector2(.05f,.86f),new Vector2(.80f,.97f),38);
            leave=CyberUI.Button(transform,welcome?"CONFIGURAR DESPUÉS":"VOLVER / ESC",new Vector2(.81f,.88f),new Vector2(.96f,.96f),Close);
            var instructions=CyberUI.Label(transform,welcome?"Antes de comenzar, queremos ayudarte a ajustar el audio. Escucha cuatro golpes y después pulsa ESPACIO o A / CRUZ al ritmo.":"Escucha cuatro golpes. Después pulsa ESPACIO o A / CRUZ siguiendo el ritmo, sin esperar a reaccionar a cada sonido.",new Vector2(.10f,.74f),new Vector2(.90f,.84f),25);instructions.alignment=TextAlignmentOptions.Center;
            var card=CombatPanelGraphic.Create(transform,"AudioCalibrationCard",new Vector2(.14f,.31f),new Vector2(.86f,.71f),CandyMenuGraphic.Mint);
            status=CyberUI.Label(card.transform,welcome?"TU PRIMER PASO ANTES DE DEFENDER LA RED":"ESCUCHA EL MISMO EQUIPO CON EL QUE JUGARÁS",new Vector2(.12f,.75f),new Vector2(.88f,.93f),23,CyberUI.Cyan);
            result=CyberUI.Label(card.transform,"OFFSET ACTUAL: "+Milliseconds(GameSettings.AudioOffsetSec),new Vector2(.12f,.43f),new Vector2(.88f,.75f),42);
            detail=CyberUI.Label(card.transform,"La prueba compara tus pulsaciones con los sonidos. Necesita al menos doce muestras y busca dieciséis.",new Vector2(.12f,.18f),new Vector2(.88f,.43f),23,CyberUI.Muted);
            foreach(var text in new[]{status,result,detail}){text.alignment=TextAlignmentOptions.Center;text.enableAutoSizing=true;text.fontSizeMin=16;text.fontSizeMax=text.fontSize;}
            for(int i=0;i<progress.Length;i++){float x=.12f+i*.0475f;progress[i]=CyberUI.Box(card.transform,"Sample"+i,new Vector2(x,.09f),new Vector2(x+.04f,.13f),CyberUI.Raised);}
            start=CyberUI.Button(transform,"INICIAR / REPETIR",new Vector2(.10f,.18f),new Vector2(.29f,.27f),()=>StartCalibration(false));
            apply=CyberUI.Button(transform,"APLICAR OFFSET",new Vector2(.305f,.18f),new Vector2(.495f,.27f),Apply);
            test=CyberUI.Button(transform,"PROBAR MI AJUSTE",new Vector2(.51f,.18f),new Vector2(.70f,.27f),()=>StartCalibration(true));
            reset=CyberUI.Button(transform,"RESTABLECER 0 ms",new Vector2(.715f,.18f),new Vector2(.905f,.27f),()=>{GameSettings.AudioOffsetSec=0;result.text="OFFSET ACTUAL: 0 ms";apply.interactable=false;});
            var note=CyberUI.Label(transform,welcome?"Puedes hacerlo después desde Ajustes → Calibrar audio. Usa los mismos auriculares y control con los que jugarás. No cambia la velocidad de la canción.":"Usa los mismos auriculares, altavoces y dispositivo de entrada. La estimación incluye la latencia del equipo y tu pulsación. ESC / START cancela.",new Vector2(.10f,.055f),new Vector2(.90f,.15f),20,CyberUI.Muted);note.alignment=TextAlignmentOptions.Center;
            source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.ignoreListenerPause=true;source.priority=0;
            tap=new InputAction("CalibrationTap",InputActionType.Button);tap.AddBinding("<Keyboard>/space");tap.AddBinding("<Gamepad>/buttonSouth");tap.performed+=OnTap;tap.Enable();
            apply.interactable=false;AudioSettings.OnAudioConfigurationChanged+=AudioChanged;
        }
        static string Milliseconds(double value)=>(value*1000).ToString("+0;-0;0")+" ms";
        AudioClip Metronome()
        {
            if(clicks!=null)return clicks;int hz=AudioSettings.outputSampleRate>0?AudioSettings.outputSampleRate:48000;
            int beats=AudioCalibrationSession.ListenBeats+AudioCalibrationSession.MeasureBeats;var pcm=new float[(int)(hz*(beats*AudioCalibrationSession.Period+.1))];
            for(int beat=0;beat<beats;beat++){int at=(int)Math.Round(beat*AudioCalibrationSession.Period*hz);for(int i=0;i<hz*.045;i++)pcm[at+i]=Mathf.Sin(2*Mathf.PI*(beat%4==0?1400:1000)*i/hz)*Mathf.Exp(-i/(hz*.007f))*.55f;}
            clicks=AudioClip.Create("Calibration100BPM",pcm.Length,1,hz,false);clicks.SetData(pcm,0);return clicks;
        }
        public void StartCalibration(bool verification=false)
        {
            StopRun();if(GameSettings.MasterVolume*GameSettings.SFXVolume<.001f){status.text="ACTIVA EL VOLUMEN MAESTRO Y LOS EFECTOS";return;}
            verify=verification;verifyOffset=GameSettings.AudioOffsetSec;
            if(AudioManager.Instance!=null)menuSuspended|=AudioManager.Instance.SuspendMenuBGM();
            source.clip=Metronome();source.volume=GameSettings.MasterVolume*GameSettings.SFXVolume;source.pitch=1;
            Session.Begin(AudioSettings.dspTime+.75);source.PlayScheduled(Session.StartDsp);Running=true;
            if(!inputLocked){oldCapture=InputManager.Instance.CapturingBinding;inputLocked=true;}InputManager.Instance.CapturingBinding=true;unlockAt=float.PositiveInfinity;
            EventSystem.current?.SetSelectedGameObject(null);Buttons(false);status.text="ESCUCHA CUATRO GOLPES";result.text="0 / 16 PULSACIONES";detail.text=verify?"Se comprobará tu offset actual: "+Milliseconds(verifyOffset):"Después pulsa al ritmo del sonido. El offset no se modificará hasta que elijas APLICAR.";RefreshProgress();
        }
        void OnTap(InputAction.CallbackContext context)
        {if(Running)RecordTapDsp(AudioSettings.dspTime+(context.time-Time.realtimeSinceStartupAsDouble));}
        public void RecordTapDsp(double dsp)
        {
            if(!Running||!Session.Record(dsp+(verify?verifyOffset:0)))return;
            result.text=Session.Count+" / 16 PULSACIONES";RefreshProgress();if(Session.Count>=AudioCalibrationSession.TargetSamples)Finish();
        }
        void RefreshProgress(){for(int i=0;i<progress.Length;i++)progress[i].color=i<Session.Count?CandyMenuGraphic.Mint:CyberUI.Raised;}
        void Buttons(bool enabled){start.interactable=test.interactable=reset.interactable=enabled;apply.interactable=false;}
        void Finish()
        {
            StopRun();Buttons(true);
            if(!Session.HasEstimate){status.text="NECESITAMOS UN PULSO MÁS ESTABLE";result.text=Session.Count+" MUESTRAS";detail.text="Repite y sigue el ritmo. Se necesitan doce pulsaciones válidas y consistentes.";return;}
            status.text=verify?"DESFASE CON TU AJUSTE":"OFFSET PROPUESTO";
            result.text=Milliseconds(verify?Session.MedianError:Session.SuggestedOffset);
            detail.text=Session.UsableSamples+" muestras · variación "+(Session.Variation*1000).ToString("0")+" ms. "+(verify?"Cerca de 0 ms indica coincidencia. Puedes repetir la calibración si lo necesitas.":"APLICAR guarda este valor para las notas y sus juicios. Negativo retrasa la llegada; positivo la adelanta.");
            apply.interactable=!verify;
        }
        public void Apply(){if(Running||verify||!Session.HasEstimate)return;GameSettings.AudioOffsetSec=Session.SuggestedOffset;result.text="GUARDADO: "+Milliseconds(GameSettings.AudioOffsetSec);status.text="OFFSET APLICADO AL JUEGO";apply.interactable=false;if(welcome){GameSettings.HasSeenAudioSetup=true;leave.GetComponentInChildren<TextMeshProUGUI>().text="CONTINUAR AL MENÚ";}}
        void StopRun(){if(source!=null)source.Stop();Running=false;if(inputLocked)unlockAt=Time.unscaledTime+.25f;}
        void ReleaseInput(){if(inputLocked&&InputManager.Instance!=null)InputManager.Instance.CapturingBinding=oldCapture;inputLocked=false;}
        void Close(){StopRun();ReleaseInput();if(welcome)GameSettings.HasSeenAudioSetup=true;back?.Invoke();}
        void Update()
        {
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true||Gamepad.current?.startButton.wasPressedThisFrame==true){Close();return;}
            if(Running){if(AudioSettings.dspTime>=Session.StartDsp+AudioCalibrationSession.ListenBeats*AudioCalibrationSession.Period)status.text=verify?"PULSA PARA COMPROBAR TU AJUSTE":"PULSA ESPACIO O A / CRUZ AL RITMO";if(AudioSettings.dspTime>Session.EndDsp+.5)Finish();}
            else if(inputLocked&&Time.unscaledTime>=unlockAt&&Keyboard.current?.spaceKey.isPressed!=true&&Gamepad.current?.buttonSouth.isPressed!=true)ReleaseInput();
        }
        void AudioChanged(bool changed){if(!Running)return;StopRun();Buttons(true);status.text="CAMBIÓ EL DISPOSITIVO DE AUDIO";detail.text="Repite la prueba con la salida actual.";if(clicks!=null){Destroy(clicks);clicks=null;}}
        void OnApplicationFocus(bool focus){if(!focus&&!Application.isBatchMode&&Running){StopRun();Buttons(true);status.text="PRUEBA INTERRUMPIDA / REPITE";}}
        void OnEnable(){tap?.Enable();}
        void OnDisable(){StopRun();ReleaseInput();tap?.Disable();if(menuSuspended&&AudioManager.Instance!=null&&(GameManager.Instance.CurrentState==GameState.Settings||GameManager.Instance.CurrentState==GameState.MainMenu))AudioManager.Instance.ResumeMenuBGM();menuSuspended=false;}
        void OnDestroy(){AudioSettings.OnAudioConfigurationChanged-=AudioChanged;tap?.Dispose();if(clicks!=null)Destroy(clicks);}
    }
}
