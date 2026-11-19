#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using RhythmWare.Core;
using RhythmWare.Rhythm;
using RhythmWare.UI;
namespace RhythmWare.EditorTools
{
    public static class SpeedWheelValidation
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static int step;static bool initialized;static Keyboard keyboard;static Mouse mouse;static AudioClip silence;
        static readonly FieldInfo paused=typeof(AudioManager).GetField("isPaused",Hidden),origin=typeof(AudioManager).GetField("dspSongStart",Hidden),active=typeof(NoteSpawner).GetField("activeNotes",Hidden);
        public static void Restore()
        {
            if(!initialized)return;initialized=false;InputManager.Instance.CapturingBinding=false;CyberUI.ModalOpen=false;
            GameSettings.UseRhythmValidationStore(null);InputManager.Instance.UseValidationStore(null);
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);
            if(silence!=null)UnityEngine.Object.Destroy(silence);
        }
        public static bool Run(Action<bool,string> check,Action<string> capture,Action<int,int> size)
        {
            var gm=GameManager.Instance;var am=AudioManager.Instance;var input=InputManager.Instance;
            switch(step)
            {
                case 0:
                    initialized=true;GameSettings.UseRhythmValidationStore("RWSpeedWheelQA_");input.UseValidationStore("RWSpeedWheelKeysQA_");keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
                    GameSettings.NoteSpeed=9;check(GameSettings.NoteSpeed==8&&PlayerPrefs.GetFloat("RWSpeedWheelQA_NoteScrollSpeed")==8,"8x is the persisted maximum reading speed");
                    check(Math.Abs(GameSettings.NoteTravelSeconds-.3)<1e-8,"8x gives a 300 ms full travel window");
                    GameSettings.NoteSpeed=0;check(GameSettings.NoteSpeed==.5f,"The existing 0.5x minimum remains available");
                    GameSettings.NoteSpeed=8;GameSettings.MasterVolume=GameSettings.SFXVolume=.5f;GameSettings.MusicVolume=.8f;
                    MainMenuUI.Instance.OpenSettings();step++;return false;
                case 1:
                    capture("settings-8x-wheel.png");
                    var chart=ScriptableObject.CreateInstance<ChartData>();silence=AudioClip.Create("WheelControlsSilence",48000*8,1,48000,false);chart.audioClip=silence;
                    chart.notes.Add(new NoteData{timestamp=3,laneIndex=0});chart.notes.Add(new NoteData{timestamp=4,laneIndex=2});chart.notes.Add(new NoteData{timestamp=5,laneIndex=4,duration=.45});
                    GameplayUI.IsTestPlayFromEditor=true;ChartManager.Instance.SelectChart(chart);NoteSpawner.Instance.LoadChart(chart);am.PlaySong(silence,10);gm.StartGame();step++;return false;
                case 2:
                    Frame(2.65);check(Notes.Count==0,"At 8x a note is hidden before its 300 ms reading window");
                    Frame(2.75);check(Notes.Count==1&&Notes[0].gameObject.activeSelf,"At 8x the first note appears when it enters the reading window");
                    var note=Notes[0];note.RefreshVisuals(2.80);Vector3 a=note.transform.position;note.RefreshVisuals(2.85);Vector3 b=note.transform.position;note.RefreshVisuals(2.90);Vector3 c=note.transform.position;
                    check(Vector3.Distance(b-a,c-b)<.0001f,"8x movement has equal distance in equal time intervals");
                    Clock(3);Send(0,Key.D);Frame(3);Send();Frame(3.8);Clock(4);Send(0,Key.J);Frame(4);Send();Frame(4.8);Clock(5);Send(0,Key.Space);Frame(5);
                    check(gm.PerfectHits==3&&Notes.Any(n=>n!=null&&n.IsHolding),"Two taps and a hold head are judged normally at 8x");
                    WheelChecks(check);Frame(5.2);check(Notes.Any(n=>n!=null&&n.IsHolding),"Adjusting volume does not drop the active hold");
                    Frame(5.46);check(gm.PerfectHits==4&&gm.MissHits==0,"The hold tail completes with no misses after volume changes");Send();
                    am.PauseSong();gm.SetState(GameState.Paused);Send(0,Key.F4);Send();check(GameSettings.NoteSpeed==8,"F4 respects the new upper limit");
                    Send(0,Key.F3);Send();check(Math.Abs(GameSettings.NoteSpeed-7.9f)<.001,"F3 can lower the new maximum");Send(0,Key.F4);Send();check(GameSettings.NoteSpeed==8,"F4 can return to 8x while paused");
                    Send(-1,Key.LeftShift);Send();check(Math.Abs(GameSettings.SFXVolume-.40f)<.001,"Shift plus wheel adjusts effects while paused");
                    step++;return false;
                case 3:
                    capture("gameplay-effects-wheel.png");am.StopSong();NoteSpawner.Instance.StopAndClear();gm.SetState(GameState.MainMenu);MainMenuUI.Instance.OpenSettings();step++;return false;
                case 4:
                    var settings=UnityEngine.Object.FindAnyObjectByType<SettingsUI>();var sliders=(Dictionary<string,Slider>)typeof(SettingsUI).GetField("sliders",Hidden).GetValue(settings);
                    check(Math.Abs(sliders["VOLUMEN MAESTRO"].value-GameSettings.MasterVolume)<.001&&Math.Abs(sliders["EFECTOS"].value-GameSettings.SFXVolume)<.001,"Reopened settings sliders reflect wheel changes without stale values");
                    capture("settings-saved-volume.png");float saved=GameSettings.MasterVolume;Send(-1);Send();check(GameSettings.MasterVolume==saved,"Settings wheel scrolling does not change gameplay volume");
                    GameManager.Instance.SetState(GameState.MainMenu);Restore();return true;
            }
            return false;
        }
        static List<NoteController> Notes=>(List<NoteController>)active.GetValue(NoteSpawner.Instance);
        static void WheelChecks(Action<bool,string> check)
        {
            var input=InputManager.Instance;double beforeTime=AudioManager.Instance.SongTime;
            Send(1,Key.Space);Send(0,Key.Space);check(Math.Abs(GameSettings.MasterVolume-.55f)<.001&&GameSettings.SFXVolume==.5f,"One wheel notch changes master by five percent and preserves the effect mix");
            Send(-1,Key.LeftShift,Key.Space);Send(0,Key.Space);check(Math.Abs(GameSettings.SFXVolume-.45f)<.001&&Math.Abs(GameSettings.MasterVolume-.55f)<.001,"Shift plus wheel changes only effects by five percent");
            var audio=AudioManager.Instance;var music=(AudioSource)typeof(AudioManager).GetField("musicSource",Hidden).GetValue(audio);var fx=(AudioSource)typeof(AudioManager).GetField("sfxSource",Hidden).GetValue(audio);
            check(Math.Abs(music.volume-.55f*.8f)<.001&&Math.Abs(fx.volume-.55f*.45f)<.001,"The actual music and SFX sources immediately receive their new gains");
            check(music.pitch==1&&audio.SongTime-beforeTime<.5&&ChartManager.Instance.SelectedChart.notes[2].timestamp==5,"Wheel mixing leaves audio pitch and chart times unchanged");
            check(Math.Abs(PlayerPrefs.GetFloat("RWSpeedWheelQA_SFXVolume")-.45f)<.001,"Wheel effect volume persists in PlayerPrefs");
            input.CapturingBinding=true;Send(-1,Key.Space);Send(0,Key.Space);input.CapturingBinding=false;check(Math.Abs(GameSettings.MasterVolume-.55f)<.001,"Binding capture blocks the volume shortcut");
            CyberUI.ModalOpen=true;Send(-1,Key.Space);Send(0,Key.Space);CyberUI.ModalOpen=false;check(Math.Abs(GameSettings.MasterVolume-.55f)<.001,"Modal lessons block the volume shortcut");
            Send(.5f,Key.Space);Send(0,Key.Space);check(Math.Abs(GameSettings.MasterVolume-.55f)<.001,"A partial high-resolution wheel tick does not over-adjust volume");
            Send(.5f,Key.Space);Send(0,Key.Space);check(Math.Abs(GameSettings.MasterVolume-.60f)<.001,"High-resolution wheel ticks accumulate into a single five percent change");
            GameSettings.MasterVolume=.02f;Send(-1,Key.Space);Send(0,Key.Space);check(GameSettings.MasterVolume==0,"Wheel can mute master without producing negative gains");
            GameSettings.MasterVolume=.98f;Send(1,Key.Space);Send(0,Key.Space);check(GameSettings.MasterVolume==1,"Wheel clamps master at one hundred percent");
            GameSettings.MasterVolume=.55f;audio.UpdateVolumes();
        }
        static void Send(float notches=0,params Key[] keys)
        {
            float units=InputSystem.settings.scrollDeltaBehavior==InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange?120:1;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.QueueStateEvent(mouse,new MouseState{scroll=new Vector2(0,notches*units)});
            InputSystem.Update();keyboard.MakeCurrent();mouse.MakeCurrent();typeof(InputManager).GetMethod("Update",Hidden).Invoke(InputManager.Instance,null);
        }
        static void Clock(double t){paused.SetValue(AudioManager.Instance,false);origin.SetValue(AudioManager.Instance,AudioSettings.dspTime-(t-GameSettings.AudioOffsetSec));}
        static void Frame(double t){Clock(t);typeof(NoteSpawner).GetMethod("Update",Hidden).Invoke(NoteSpawner.Instance,null);}
    }
}
#endif
