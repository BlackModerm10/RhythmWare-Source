#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using TMPro;
using RhythmWare.Core;
using RhythmWare.Rhythm;
using RhythmWare.UI;

namespace RhythmWare.EditorTools
{
    public static class BetaPolishValidation
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static int step;static bool initialized;static Keyboard keyboard;static Gamepad pad;static AudioClip clip;static AudioCalibrationUI calibration;
        static readonly FieldInfo origin=typeof(AudioManager).GetField("dspSongStart",Hidden);
        static List<NoteController> Active=>(List<NoteController>)typeof(NoteSpawner).GetField("activeNotes",Hidden).GetValue(NoteSpawner.Instance);
        public static void Restore()
        {
            if(!initialized)return;initialized=false;
            if(calibration!=null)calibration.gameObject.SetActive(false);
            GameSettings.UseRhythmValidationStore(null);InputManager.Instance.UseValidationStore(null);
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);
            if(clip!=null)UnityEngine.Object.Destroy(clip);
        }
        public static bool Run(Action<bool,string> check,Action<string> capture,Action<int,int> size)
        {
            var gm=GameManager.Instance;
            switch(step)
            {
                case 0:
                    initialized=true;GameSettings.UseRhythmValidationStore("RWBetaQA_");InputManager.Instance.UseValidationStore("RWBetaKeysQA_");
                    check(!RhythmWare.Education.SocLearningProgress.Pathname.StartsWith(Application.persistentDataPath,StringComparison.OrdinalIgnoreCase),"SOC validation always uses an isolated file, including between legacy fixtures");
                    keyboard=InputSystem.AddDevice<Keyboard>();pad=InputSystem.AddDevice<Gamepad>();clip=AudioClip.Create("BetaTimingFixture",44100*20,1,44100,false);
                    GameSettings.AudioOffsetSec=0;GameSettings.NoteSpeed=1;
                    check(!GameSettings.HasSeenAudioSetup,"A clean preference profile offers audio setup");
                    MainMenuUI.Instance.OpenSettings();UnityEngine.Object.FindAnyObjectByType<SettingsUI>().OpenFirstRunCalibration();step++;return false;
                case 1:
                    calibration=UnityEngine.Object.FindAnyObjectByType<AudioCalibrationUI>();
                    check(calibration.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text.Contains("Antes de comenzar")),"First-run calibration explains its purpose before asking for taps");
                    capture("beta-first-run-audio.png");
                    typeof(AudioCalibrationUI).GetMethod("Close",Hidden).Invoke(calibration,null);
                    check(GameSettings.HasSeenAudioSetup&&gm.CurrentState==GameState.MainMenu,"Explicitly configuring later dismisses the offer and returns to the menu");
                    GameSettings.HasSeenAudioSetup=false;MainMenuUI.Instance.OpenSettings();UnityEngine.Object.FindAnyObjectByType<SettingsUI>().OpenFirstRunCalibration();step++;return false;
                case 2:
                    calibration=UnityEngine.Object.FindAnyObjectByType<AudioCalibrationUI>();calibration.StartCalibration();
                    for(int i=4;i<20;i++)calibration.RecordTapDsp(calibration.Session.StartDsp+i*AudioCalibrationSession.Period+.100);
                    calibration.Apply();check(GameSettings.HasSeenAudioSetup&&Math.Abs(GameSettings.AudioOffsetSec+.100)<.006,"Applying the first-run estimate persists both offset and onboarding completion");
                    typeof(AudioCalibrationUI).GetMethod("Close",Hidden).Invoke(calibration,null);GameSettings.AudioOffsetSec=0;
                    Timing(check);MainMenuUI.Instance.BackToMainMenu();MainMenuUI.Instance.OpenExtraContent();step++;return false;
                case 3:
                    var academy=UnityEngine.Object.FindAnyObjectByType<ExtraContentUI>();academy.SwitchTab(2);Canvas.ForceUpdateCanvases();
                    check(academy.GetComponentsInChildren<Image>().Any(i=>i.name=="BossImage"&&i.sprite!=null),"Bonzi dossier displays the actual boss artwork");
                    var words=string.Join(" ",academy.GetComponentsInChildren<TextMeshProUGUI>().Select(t=>t.text));
                    check(words.Contains("2004")&&words.Contains("Trojan")&&words.Contains("ficticia")&&!words.Contains("DINNER!")&&!words.Contains("HYPER"),"Dossier teaches history and fictional attack context without map details");capture("beta-bonzi-dossier.png");
                    academy.SwitchTab(3);step++;return false;
                case 4:
                    words=string.Join(" ",UnityEngine.Object.FindAnyObjectByType<ExtraContentUI>().GetComponentsInChildren<TextMeshProUGUI>().Select(t=>t.text));
                    check(words.Contains("BETA")&&!words.Contains("Unity 6")&&!words.Contains("Inspiraciones"),"About identifies the beta with concise player-facing information");capture("beta-about.png");
                    MainMenuUI.Instance.OpenLevelSelect();step++;return false;
                case 5:
                    var selector=UnityEngine.Object.FindAnyObjectByType<LevelSelectUI>();var entries=selector.RefreshCatalog();
                    check(entries.Count(e=>string.IsNullOrEmpty(e.chartFilePath))==3,"Exactly three official campaign levels ship in the beta");
                    foreach(int i in new[]{0,1,2})
                    {
                        typeof(LevelSelectUI).GetMethod("Select",Hidden).Invoke(selector,new object[]{i});
                        var info=(TextMeshProUGUI)typeof(LevelSelectUI).GetField("info",Hidden).GetValue(selector);
                        check(!info.text.Contains("MAPPER")&&!info.text.Contains("HYPER")&&!info.text.Contains("Anwul_")&&!info.text.Contains("LeiN-"),"Campaign detail card hides imported metadata for level "+i);
                    }
                    CustomMetadata(selector,check);
                    step=50;return false;
                case 50:
                    if(UnityEngine.Object.FindAnyObjectByType<LevelSelectUI>().RefreshCatalog()[2].audioClip==null)return false;
                    capture("beta-campaign-select.png");size(1024,768);Prepare(new NoteData{timestamp=3,laneIndex=4});gm.SetState(GameState.Paused);step=6;return false;
                case 6:
                    LaneController.Instance.RefreshKeyLabels();step++;return false;
                case 7:
                    foreach(var label in LaneController.Instance.GetComponentsInChildren<TextMesh>())
                    {
                        int lane=Array.FindIndex(Enumerable.Range(0,5).Select(InputManager.Instance.LabelForLane).ToArray(),v=>v==label.text);if(lane<0)continue;
                        var rect=EpicStageLayout.WorldRect(Camera.main);var sizePx=EpicStageLayout.ReceptorSize(lane);
                        check(label.GetComponent<MeshRenderer>().bounds.size.x<=sizePx.x*rect.height/1080*.78f,"Receptor label fits at 4:3: "+label.text);
                    }
                    capture("beta-gameplay-1024x768.png");InputManager.Instance.RemapKey(4,Key.Backspace,false);step=8;return false;
                case 8:
                    var longLabel=LaneController.Instance.GetComponentsInChildren<TextMesh>().First(t=>t.text==InputManager.Instance.LabelForLane(4));
                    check(longLabel.GetComponent<MeshRenderer>().bounds.size.x<EpicStageLayout.ReceptorSize(4).x*EpicStageLayout.WorldRect(Camera.main).height/1080*.78f,"Long rebound key labels also fit the central receptor at 4:3");capture("beta-rebound-label-1024x768.png");size(1920,1080);Stop();MainMenuUI.Instance.BackToMainMenu();Restore();return true;
            }
            return false;
        }
        static void Timing(Action<bool,string> check)
        {
            foreach(bool controller in new[]{false,true})
            {
                Prepare(new NoteData{timestamp=3,laneIndex=0});Clock(3.075);
                double received=double.NaN;Action<int,double> probe=(lane,dsp)=>received=dsp;InputManager.Instance.OnLanePressedDsp+=probe;
                Send(true,controller,.045);InputManager.Instance.OnLanePressedDsp-=probe;
                check(GameManager.Instance.PerfectHits==1&&GameManager.Instance.GoodHits==0,"Timestamp preserves a +30ms Perfect when processed at +75ms: "+(controller?"gamepad":"keyboard")+" (Perfect="+GameManager.Instance.PerfectHits+", Good="+GameManager.Instance.GoodHits+", Miss="+GameManager.Instance.MissHits+", event="+received+", capture="+InputManager.Instance.CapturingBinding+", active="+Active.Count+")");Send(false,controller);
            }
            Prepare(new NoteData{timestamp=3,laneIndex=0},new NoteData{timestamp=3.09,laneIndex=0});Clock(3.06);Send(true);
            check(Active.First(n=>n.TargetTimestamp==3).IsHit&&!Active.First(n=>n.TargetTimestamp==3.09).IsHit,"Overlapping valid notes resolve in chronological order");Send(false);Clock(3.09);Send(true);
            check(GameManager.Instance.PerfectHits==2,"The second nearby note remains independently hittable");Send(false);
            Prepare(new NoteData{timestamp=3,laneIndex=0},new NoteData{timestamp=3.18,laneIndex=0});Clock(3.145);Send(true);
            check(!Active.First(n=>n.TargetTimestamp==3).IsHit&&Active.First(n=>n.TargetTimestamp==3.18).IsHit,"A skipped note in the Miss band does not steal the next valid hit");Send(false);
            Prepare(new NoteData{timestamp=3,laneIndex=0},new NoteData{timestamp=3.25,laneIndex=0});Clock(3.25);Send(true);
            check(GameManager.Instance.MissHits==1&&GameManager.Instance.PerfectHits==1,"An expired earlier note resolves without consuming the next press");Send(false);
            Prepare(new NoteData{timestamp=3,laneIndex=0},new NoteData{timestamp=3.08,laneIndex=0});Clock(3.08);
            // Multiple transitions in one input update must survive a render hitch.
            double now=Time.realtimeSinceStartupAsDouble;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D),now-.08);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(),now-.04);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D),now);
            InputSystem.Update();check(GameManager.Instance.PerfectHits==2,"Two presses queued in one frame resolve both notes in event order");Send(false);
            Prepare(new NoteData{timestamp=3,laneIndex=0,duration=1});Clock(3);Send(true);Clock(4.10);Send(false,false,.20);
            check(GameManager.Instance.GoodHits==1&&GameManager.Instance.PerfectHits==1,"A release at 3.90s is judged before a delayed frame at 4.10s, rather than receiving a false full-tail Perfect");
            Prepare(new NoteData{timestamp=3,laneIndex=0,duration=1});Clock(3);Send(true);Clock(4.10);Send(false,false,.05);
            check(GameManager.Instance.PerfectHits==2,"Releasing after the actual tail earns Perfect even when its frame is late");
            Prepare(new NoteData{timestamp=3,laneIndex=0});Clock(2.8);double before=AudioManager.Instance.SongTime;
            GameManager.Instance.PauseForFocusLoss();check(GameManager.Instance.CurrentState==GameState.Paused&&!AudioManager.Instance.IsPlaying,"Losing focus freezes gameplay and audio in the pause menu");
            typeof(GameManager).GetMethod("OnApplicationFocus",Hidden).Invoke(GameManager.Instance,new object[]{true});
            check(GameManager.Instance.CurrentState==GameState.Paused&&Math.Abs(before-AudioManager.Instance.SongTime)<.02,"Regaining focus does not automatically resume the song");Stop();
        }
        static void CustomMetadata(LevelSelectUI selector,Action<bool,string> check)
        {
            var list=(List<LevelItemInfo>)typeof(LevelSelectUI).GetField("levels",Hidden).GetValue(selector);
            var custom=ScriptableObject.CreateInstance<ChartData>();custom.songTitle="Personal map";custom.difficultyId="QA_CUSTOM";custom.mapper="QA Mapper";
            list.Add(new LevelItemInfo{chartData=custom,songTitle=custom.songTitle,description="Custom fixture",chartFilePath="in-memory-qa",audioClip=clip,audioClipPath="in-memory-qa"});
            typeof(LevelSelectUI).GetField("selectedIndex",Hidden).SetValue(selector,list.Count-1);typeof(LevelSelectUI).GetMethod("ShowInfo",Hidden).Invoke(selector,null);
            var info=(TextMeshProUGUI)typeof(LevelSelectUI).GetField("info",Hidden).GetValue(selector);
            check(info.text.Contains("MAPPER: QA Mapper")&&info.text.Contains("QA_CUSTOM"),"Personal maps retain mapper attribution and their own difficulty labels");
            list.RemoveAt(list.Count-1);typeof(LevelSelectUI).GetField("selectedIndex",Hidden).SetValue(selector,2);typeof(LevelSelectUI).GetMethod("ShowInfo",Hidden).Invoke(selector,null);UnityEngine.Object.Destroy(custom);
        }
        static void Prepare(params NoteData[] notes)
        {
            Stop();
            // A fresh virtual device has no later state timestamp which would
            // correctly cause Input System to discard our intentionally old event.
            InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(pad);
            keyboard=InputSystem.AddDevice<Keyboard>();pad=InputSystem.AddDevice<Gamepad>();
            var chart=ScriptableObject.CreateInstance<ChartData>();chart.laneLayout=LaneLayout.Id;chart.notes.AddRange(notes);chart.audioClip=clip;
            GameplayUI.IsTestPlayFromEditor=true;ChartManager.Instance.SelectChart(chart);NoteSpawner.Instance.LoadChart(chart);AudioManager.Instance.PlaySong(clip,.05);GameManager.Instance.StartGame();InputManager.Instance.FlushHeld();
        }
        static void Clock(double t)
        {
            origin.SetValue(AudioManager.Instance,AudioSettings.dspTime-t);
            // Advancing the virtual transport also moves its start epoch back.
            typeof(AudioManager).GetField("inputEpochDsp",Hidden).SetValue(AudioManager.Instance,AudioSettings.dspTime-10);
        }
        static void Send(bool down,bool controller=false,double age=0)
        {
            // Queue a real, monotonically ordered event and then delay its
            // processing. Backdating a release before its press is invalid.
            if(age>0)origin.SetValue(AudioManager.Instance,(double)origin.GetValue(AudioManager.Instance)+age);
            double t=Time.realtimeSinceStartupAsDouble;
            if(controller)InputSystem.QueueStateEvent(pad,down?new GamepadState(GamepadButton.LeftShoulder):new GamepadState(),t);
            else InputSystem.QueueStateEvent(keyboard,down?new KeyboardState(Key.D):new KeyboardState(),t);
            if(age>0)System.Threading.Thread.Sleep((int)Math.Round(age*1000));
            InputSystem.Update();
        }
        static void Stop(){AudioManager.Instance.StopSong();NoteSpawner.Instance.StopAndClear();GameManager.Instance.SetState(GameState.MainMenu);if(keyboard!=null&&keyboard.added){InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();}if(pad!=null&&pad.added){InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.Update();}}
    }
}
#endif
