using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using RhythmWare.Core;

namespace RhythmWare.UI
{
    public sealed class SettingsUI : MonoBehaviour
    {
        TextMeshProUGUI offsetValue,taps,noteSpeed,resolution;
        Button fullscreen,effects;
        bool built;
        GameObject mainPage,controlPage,calibrationPage,profilePage;
        int controlsClosedFrame=-1;
        readonly Dictionary<string,Slider> sliders=new Dictionary<string,Slider>();
        readonly Dictionary<string,TextMeshProUGUI> sliderValues=new Dictionary<string,TextMeshProUGUI>();
        public void Build()
        {
            if(built)return;built=true;
            mainPage=CyberUI.Rect(transform,"SettingsMain",Vector2.zero,Vector2.one).gameObject;var view=mainPage.transform;CandyUI.Backdrop(view);
            CandyUI.Title(view,"SETTINGS / A TU RITMO",new Vector2(.04f,.86f),new Vector2(.78f,.96f),40);
            CyberUI.Label(view,"Ajusta la mezcla, la imagen y tu sincronización.",new Vector2(.04f,.80f),new Vector2(.85f,.86f),22,CyberUI.Muted);
            CyberUI.Button(view,"VOLVER / ESC",new Vector2(.80f,.88f),new Vector2(.97f,.96f),()=>MainMenuUI.Instance.BackToMainMenu());
            var audio=CyberUI.Box(view,"AudioSettings",new Vector2(.04f,.37f),new Vector2(.48f,.78f),CyberUI.Panel);
            CandyUI.Title(audio.transform,"01 / AUDIO",new Vector2(.05f,.84f),new Vector2(.95f,.98f),30);
            Volume(audio.transform,"VOLUMEN MAESTRO",.66f,GameSettings.MasterVolume,v=>GameSettings.MasterVolume=v);
            Volume(audio.transform,"MÚSICA",.43f,GameSettings.MusicVolume,v=>GameSettings.MusicVolume=v);
            Volume(audio.transform,"EFECTOS",.20f,GameSettings.SFXVolume,v=>GameSettings.SFXVolume=v);
            var video=CyberUI.Box(view,"VideoSettings",new Vector2(.51f,.48f),new Vector2(.96f,.78f),CyberUI.Panel);
            CandyUI.Title(video.transform,"02 / IMAGEN",new Vector2(.05f,.82f),new Vector2(.95f,.98f),28);
            CyberUI.Button(video.transform,"‹",new Vector2(.04f,.59f),new Vector2(.16f,.79f),()=>{DisplaySettings.CycleResolution(-1);RefreshButtons();});
            resolution=CyberUI.Label(video.transform,"",new Vector2(.18f,.59f),new Vector2(.82f,.79f),23,CyberUI.Cyan);resolution.alignment=TextAlignmentOptions.Center;
            CyberUI.Button(video.transform,"›",new Vector2(.84f,.59f),new Vector2(.96f,.79f),()=>{DisplaySettings.CycleResolution(1);RefreshButtons();});
            fullscreen=CyberUI.Button(video.transform,"",new Vector2(.04f,.35f),new Vector2(.49f,.55f),()=>{GameSettings.IsFullscreen=!GameSettings.IsFullscreen;RefreshButtons();});
            effects=CyberUI.Button(video.transform,"",new Vector2(.51f,.35f),new Vector2(.96f,.55f),()=>{VisualPreferences.ReducedEffects=!VisualPreferences.ReducedEffects;RefreshButtons();});
            SliderRow(video.transform,"ATENUACIÓN DEL ESCENARIO",.11f,GameSettings.BackgroundDim,v=>GameSettings.BackgroundDim=v);
            var sync=CyberUI.Box(view,"SyncSettings",new Vector2(.51f,.17f),new Vector2(.96f,.45f),CyberUI.Panel);
            CandyUI.Title(sync.transform,"03 / SINCRONIZACIÓN",new Vector2(.05f,.75f),new Vector2(.95f,.98f),27);
            offsetValue=CyberUI.Label(sync.transform,"",new Vector2(.34f,.47f),new Vector2(.67f,.74f),30,CyberUI.Cyan);offsetValue.alignment=TextAlignmentOptions.Center;
            CyberUI.Button(sync.transform,"MÁS TARDE −5 ms",new Vector2(.05f,.47f),new Vector2(.32f,.72f),()=>ChangeOffset(-.005f));
            CyberUI.Button(sync.transform,"MÁS TEMPRANO +5 ms",new Vector2(.68f,.47f),new Vector2(.95f,.72f),()=>ChangeOffset(.005f));
            CyberUI.Button(sync.transform,"CALIBRAR AUDIO / ESCUCHAR Y PULSAR",new Vector2(.05f,.20f),new Vector2(.95f,.43f),OpenCalibration);
            taps=CyberUI.Label(sync.transform,"El offset ajusta la llegada y los juicios. Si llegan antes del sonido: MÁS TARDE; si llegan después: MÁS TEMPRANO.",new Vector2(.05f,.025f),new Vector2(.95f,.19f),15,CyberUI.Muted);
            var speedPanel=CyberUI.Box(view,"NoteSpeedSettings",new Vector2(.04f,.17f),new Vector2(.48f,.34f),CyberUI.Panel);
            CandyUI.Title(speedPanel.transform,"VELOCIDAD DE NOTAS / 0,5–8×",new Vector2(.05f,.61f),new Vector2(.95f,.96f),24);
            CyberUI.Button(speedPanel.transform,"− / F3",new Vector2(.04f,.12f),new Vector2(.25f,.55f),()=>GameSettings.NoteSpeed-=.1f);
            noteSpeed=CyberUI.Label(speedPanel.transform,"",new Vector2(.27f,.12f),new Vector2(.73f,.55f),28,CyberUI.Cyan);noteSpeed.alignment=TextAlignmentOptions.Center;
            CyberUI.Button(speedPanel.transform,"+ / F4",new Vector2(.75f,.12f),new Vector2(.96f,.55f),()=>GameSettings.NoteSpeed+=.1f);
            GameSettings.OnNoteSpeedChanged+=RefreshNoteSpeed;RefreshNoteSpeed(GameSettings.NoteSpeed);
            CyberUI.Label(view,"En partida: RUEDA ajusta el volumen general; SHIFT + RUEDA ajusta solo efectos. Los cambios se guardan automáticamente.",new Vector2(.04f,.07f),new Vector2(.96f,.14f),20,CyberUI.Muted);
            CyberUI.Button(view,"CONFIGURAR TECLADO Y MANDO",new Vector2(.04f,.015f),new Vector2(.48f,.065f),OpenControls);
            CyberUI.Button(view,"PERFIL / CAMBIAR PARTICIPANTE",new Vector2(.51f,.015f),new Vector2(.96f,.065f),()=>OpenProfiles(false));
            RefreshButtons();ChangeOffset(0,false);
        }
        public void OpenProfiles(bool welcome=false)
        {
            if(profilePage!=null){profilePage.SetActive(false);Destroy(profilePage);}
            profilePage=CyberUI.Rect(transform,"ParticipantProfiles",Vector2.zero,Vector2.one).gameObject;
            profilePage.AddComponent<ParticipantProfileUI>().Build(()=>{
                controlsClosedFrame=Time.frameCount;profilePage.SetActive(false);mainPage.SetActive(true);ChangeOffset(0,false);RefreshNoteSpeed(GameSettings.NoteSpeed);
                RefreshVolume("VOLUMEN MAESTRO",GameSettings.MasterVolume);RefreshVolume("MÚSICA",GameSettings.MusicVolume);RefreshVolume("EFECTOS",GameSettings.SFXVolume);
                if(welcome){if(!GameSettings.HasSeenAudioSetup)OpenFirstRunCalibration();else MainMenuUI.Instance.BackToMainMenu();}
            },welcome);
            mainPage.SetActive(false);
        }
        public void OpenControls()
        {
            if(controlPage==null){controlPage=CyberUI.Rect(transform,"ControlsPage",Vector2.zero,Vector2.one).gameObject;controlPage.AddComponent<ControlBindingsUI>().Build(()=>{controlsClosedFrame=Time.frameCount;controlPage.SetActive(false);mainPage.SetActive(true);});}
            mainPage.SetActive(false);controlPage.SetActive(true);controlPage.GetComponent<ControlBindingsUI>().Refresh();controlPage.GetComponent<ControlBindingsUI>().FocusFirst();
        }
        public void OpenCalibration()=>OpenCalibrationPage(false);
        public void OpenFirstRunCalibration()=>OpenCalibrationPage(true);
        void OpenCalibrationPage(bool welcome)
        {
            if(calibrationPage!=null){calibrationPage.SetActive(false);Destroy(calibrationPage);}
            calibrationPage=CyberUI.Rect(transform,"CalibrationPage",Vector2.zero,Vector2.one).gameObject;
            calibrationPage.AddComponent<AudioCalibrationUI>().Build(()=>{controlsClosedFrame=Time.frameCount;calibrationPage.SetActive(false);mainPage.SetActive(true);ChangeOffset(0,false);if(welcome)MainMenuUI.Instance.BackToMainMenu();},welcome);
            mainPage.SetActive(false);calibrationPage.SetActive(true);
        }
        void RefreshNoteSpeed(float value){if(noteSpeed!=null)noteSpeed.text=value.ToString("0.0")+"× · "+GameSettings.NoteTravelSeconds.ToString("0.00")+" s";}
        void OnDestroy(){GameSettings.OnNoteSpeedChanged-=RefreshNoteSpeed;}
        void Volume(Transform parent,string label,float y,float value,Action<float> apply)
        { SliderRow(parent,label,y,value,v=>{apply(v);AudioManager.Instance?.UpdateVolumes();}); }
        void SliderRow(Transform parent,string label,float y,float value,Action<float> apply)
        {
            var row=CyberUI.Rect(parent,label,new Vector2(.05f,y-.10f),new Vector2(.95f,y+.12f));
            CyberUI.Label(row,label,new Vector2(0,.48f),new Vector2(.80f,1),19);
            var text=CyberUI.Label(row,"",new Vector2(.81f,.48f),Vector2.one,19,CyberUI.Cyan);text.alignment=TextAlignmentOptions.MidlineRight;
            var rect=CyberUI.Rect(row,"Slider",new Vector2(.02f,.05f),new Vector2(.98f,.43f));
            var slider=rect.gameObject.AddComponent<Slider>();slider.minValue=0;slider.maxValue=1;
            var track=CyberUI.Box(rect,"Track",new Vector2(0,.30f),new Vector2(1,.70f),CyberUI.Raised);track.raycastTarget=true;
            var fill=CyberUI.Box(rect,"Fill",new Vector2(0,.30f),new Vector2(1,.70f),CyberUI.Magenta);
            var handleArea=CyberUI.Rect(rect,"HandleArea",Vector2.zero,Vector2.one);
            var handle=CyberUI.Box(handleArea,"Handle",Vector2.zero,new Vector2(0,1),CyberUI.Cyan);handle.rectTransform.sizeDelta=new Vector2(14,0);handle.raycastTarget=true;
            slider.fillRect=fill.rectTransform;slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;slider.SetValueWithoutNotify(value);
            text.text=$"{value*100:0}%";slider.onValueChanged.AddListener(v=>{text.text=$"{v*100:0}%";apply(v);});
            sliders[label]=slider;sliderValues[label]=text;
        }
        void RefreshButtons()
        {
            fullscreen.GetComponentInChildren<TextMeshProUGUI>().text=GameSettings.IsFullscreen?"PANTALLA COMPLETA":"MODO VENTANA";
            resolution.text=DisplaySettings.ResolutionLabel;
            effects.GetComponentInChildren<TextMeshProUGUI>().text="EFECTOS REDUCIDOS: "+(VisualPreferences.ReducedEffects?"ON":"OFF");
        }
        void ChangeOffset(float amount,bool save=true) { if(save)GameSettings.AudioOffsetSec+=amount;offsetValue.text=$"{GameSettings.AudioOffsetSec*1000:0} ms"; }
        void OnEnable() { if(built){RefreshButtons();ChangeOffset(0,false);RefreshNoteSpeed(GameSettings.NoteSpeed);RefreshVolume("VOLUMEN MAESTRO",GameSettings.MasterVolume);RefreshVolume("MÚSICA",GameSettings.MusicVolume);RefreshVolume("EFECTOS",GameSettings.SFXVolume);} }
        void RefreshVolume(string label,float value){if(sliders.TryGetValue(label,out var slider)){slider.SetValueWithoutNotify(value);sliderValues[label].text=(value*100).ToString("0")+"%";}}
        void Update() { if((profilePage==null||!profilePage.activeSelf)&&controlsClosedFrame!=Time.frameCount&&(controlPage==null||!controlPage.activeSelf)&&(calibrationPage==null||!calibrationPage.activeSelf)&&!CandyMenuTransition.IsRunning&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)MainMenuUI.Instance.BackToMainMenu(); }
    }
}
