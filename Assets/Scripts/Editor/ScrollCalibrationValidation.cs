#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using RhythmWare.Core;
using RhythmWare.Rhythm;
using RhythmWare.UI;

namespace RhythmWare.EditorTools
{
    public static class ScrollCalibrationValidation
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static int step;static bool initialized;static Keyboard keyboard;static AudioClip silence;static AudioCalibrationUI calibration;
        static readonly FieldInfo active=typeof(NoteSpawner).GetField("activeNotes",Hidden),paused=typeof(AudioManager).GetField("isPaused",Hidden),origin=typeof(AudioManager).GetField("dspSongStart",Hidden);
        public static void Restore()
        {
            if(!initialized)return;initialized=false;if(calibration!=null)calibration.gameObject.SetActive(false);
            GameSettings.UseRhythmValidationStore(null);InputManager.Instance.UseValidationStore(null);
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);if(silence!=null)UnityEngine.Object.Destroy(silence);
        }
        public static bool Run(Action<bool,string> check,Action<string> capture,Action<int,int> size)
        {
            var gm=GameManager.Instance;var audio=AudioManager.Instance;var spawner=NoteSpawner.Instance;
            switch(step)
            {
                case 0:
                    initialized=true;GameSettings.UseRhythmValidationStore("RWScrollQA_");InputManager.Instance.UseValidationStore("RWScrollKeysQA_");keyboard=InputSystem.AddDevice<Keyboard>();
                    Model(check);Motion(check);ArrivalMotion(check);UniformSpacing(check);PrepareLiveSpeed();step++;return false;
                case 1:
                    LiveSpeed(check);RecoverySpeed(check);MainMenuUI.Instance.OpenSettings();step++;return false;
                case 2:
                    capture("settings-scroll-calibration-1920.png");UnityEngine.Object.FindAnyObjectByType<SettingsUI>().OpenCalibration();step++;return false;
                case 3:
                    calibration=UnityEngine.Object.FindAnyObjectByType<AudioCalibrationUI>();calibration.StartCalibration();
                    check(calibration.Running&&InputManager.Instance.CapturingBinding,"Calibration schedules its own audible sequence and captures rhythm input");step++;return false;
                case 4:
                    var output=(AudioSource)typeof(AudioCalibrationUI).GetField("source",Hidden).GetValue(calibration);
                    check(output.isPlaying&&output.pitch==1&&output.volume>0,"Calibration audio really plays at normal pitch and nonzero volume (playing="+output.isPlaying+", running="+calibration.Running+", pitch="+output.pitch+", gain="+output.volume+", status="+((TMPro.TextMeshProUGUI)typeof(AudioCalibrationUI).GetField("status",Hidden).GetValue(calibration)).text+")");
                    var pcm=new float[output.clip.samples];output.clip.GetData(pcm,0);check(pcm.Any(v=>Math.Abs(v)>.1),"Metronome PCM contains audible clicks");
                    check(!AudioManager.Instance.IsMenuMusicPlaying,"Menu music is suspended during calibration");capture("audio-calibration-listening-1920.png");
                    calibration.Session.Begin(AudioSettings.dspTime-4*AudioCalibrationSession.Period-.120);
                    SendKeys(Key.Space);SendKeys();check(calibration.Session.Count==1&&Math.Abs(calibration.Session.MedianError-.120)<.035,"A real Input System action supplies its event timestamp to the calibration");
                    for(int i=5;i<20;i++)calibration.RecordTapDsp(calibration.Session.StartDsp+i*AudioCalibrationSession.Period+.120+(i%3-1)*.003);
                    check(!calibration.Running&&calibration.Session.HasEstimate&&Math.Abs(calibration.SuggestedOffset+.120)<.006,"Sixteen measured late taps propose a negative correction");step++;return false;
                case 5:
                    capture("audio-calibration-result-1920.png");calibration.Apply();check(Math.Abs(GameSettings.AudioOffsetSec+.120)<.006,"APPLY persists the measured offset in an isolated preference store");
                    check(Math.Abs(PlayerPrefs.GetFloat("RWScrollQA_AudioOffsetSec")+.120)<.006,"Offset is saved rather than only displayed");
                    calibration.StartCalibration(true);for(int i=4;i<20;i++)calibration.RecordTapDsp(calibration.Session.StartDsp+i*AudioCalibrationSession.Period+.120);
                    check(!calibration.Running&&Math.Abs(calibration.Session.MedianError)<.006,"Testing the applied correction reports near-zero residual error");step++;return false;
                case 6:
                    capture("audio-calibration-verified-1920.png");calibration.gameObject.SetActive(false);ClockOffset(check);AudioManager.Instance.StopSong();NoteSpawner.Instance.StopAndClear();gm.SetState(GameState.MainMenu);Restore();return true;
            }
            return false;
        }
        static void Model(Action<bool,string> check)
        {
            foreach(double delay in new[]{-.160,.120,.245})
            {
                var session=new AudioCalibrationSession();session.Begin(100);
                check(!session.Record(100+.120),"Warmup taps are ignored");
                for(int i=4;i<20;i++){session.Record(100+i*.6+delay+(i%3-1)*.003);check(!session.Record(100+i*.6+delay+.010),"Duplicate beat "+i+" cannot bias calibration");}
                check(session.HasEstimate&&Math.Abs(session.SuggestedOffset+delay)<.006,"Calibration sign and median are correct at "+delay+" seconds");
            }
            var irregular=new AudioCalibrationSession();irregular.Begin(100);for(int i=4;i<20;i++)irregular.Record(100+i*.6+(i%2==0?.20:-.20));check(!irregular.HasEstimate,"Highly irregular taps do not produce a misleading applicable offset");
            var sparse=new AudioCalibrationSession();sparse.Begin(100);for(int i=4;i<10;i++)sparse.Record(100+i*.6+.1);check(!sparse.HasEstimate,"Insufficient samples do not enable an offset proposal");
        }
        static void Motion(Action<bool,string> check)
        {
            foreach(float speed in new[]{.5f,1,1.7f,3})
            {
                GameSettings.NoteSpeed=speed;
                foreach(int lane in LaneLayout.VisualOrder)
                {
                    var go=new GameObject("ConstantMotionFixture");go.AddComponent<SpriteRenderer>();var note=go.AddComponent<NoteController>();note.Initialize(new NoteData{timestamp=10,laneIndex=lane},GameSettings.NoteTravelSeconds);
                    var positions=new List<Vector3>();for(int i=1;i<=4;i++){note.UpdatePosition(10-note.TravelDuration+i*.2*note.TravelDuration);positions.Add(note.transform.position);}
                    check(Vector3.Distance(positions[1]-positions[0],positions[2]-positions[1])<.0001f&&Vector3.Distance(positions[2]-positions[1],positions[3]-positions[2])<.0001f,"Constant movement in equal time slices: "+lane+" at "+speed+"x");UnityEngine.Object.Destroy(go);
                }
            }
            GameSettings.NoteSpeed=1;
        }
        static void SendKeys(params Key[] keys)
        {InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();keyboard.MakeCurrent();typeof(InputManager).GetMethod("Update",Hidden).Invoke(InputManager.Instance,null);}
        static void ArrivalMotion(Action<bool,string> check)
        {
            var camera=Camera.main;var palette=ChartData.GetDefaultMalwareCatalog();
            foreach(float speed in new[]{.5f,1,1.7f,3})
            foreach(int lane in LaneLayout.VisualOrder)
            foreach(string kind in new[]{"Normal","Spyware","Adware","Trojan"})
            {
                GameSettings.NoteSpeed=speed;
                var go=new GameObject("ArrivalMotion_"+kind);go.AddComponent<SpriteRenderer>();var note=go.AddComponent<NoteController>();
                note.Initialize(new NoteData{timestamp=10,laneIndex=lane,malwareData=kind=="Normal"?null:palette[kind]},GameSettings.NoteTravelSeconds);
                Vector3 previous=Vector3.zero,velocity=Vector3.zero;bool constant=true,crossed=false;
                // Real frame positions throughout the final 120 ms of approach
                // AND the first 120 ms of the late-hit window.
                for(int i=0;i<=8;i++)
                {
                    double time=10-.12+i*.03;note.UpdatePosition(time);
                    Vector3 screen=camera.WorldToScreenPoint(note.transform.position);
                    if(i==1)velocity=screen-previous;
                    else if(i>1)constant&=Vector3.Distance(screen-previous,velocity)<.015f;
                    if(i==8)crossed=screen.y<camera.WorldToScreenPoint(LaneController.Instance.GetHitboxPosition(lane)).y-1;
                    previous=screen;
                }
                check(constant&&velocity.sqrMagnitude>1&&crossed&&!note.IsMissed,"Constant screen velocity through the receptor and late-hit window: "+kind+" lane "+lane+" at "+speed+"x");
                UnityEngine.Object.Destroy(go);
            }
            // Regression witness: the previous clamp pins every sample after
            // the target to the same position, regardless of elapsed time.
            check(Mathf.Clamp01(1.01f)==Mathf.Clamp01(1.05f)&&EpicStageLayout.RoutePixel(0,1.05f)!=EpicStageLayout.RoutePixel(0,1.01f),"Old endpoint clamp reproduced; new route continues past the hit line");
            var holdObject=new GameObject("LateHoldRibbon");holdObject.AddComponent<SpriteRenderer>();var hold=holdObject.AddComponent<NoteController>();
            hold.Initialize(new NoteData{timestamp=10,laneIndex=4,duration=.8},2.4);hold.UpdatePosition(10.08);
            var ribbon=holdObject.GetComponent<LineRenderer>();
            check(Vector3.Distance(ribbon.GetPosition(ribbon.positionCount-1),hold.transform.position)<.0001f,"Unpressed hold ribbon follows its moving head beyond the receptor");
            UnityEngine.Object.Destroy(holdObject);GameSettings.NoteSpeed=1;
        }
        static void UniformSpacing(Action<bool,string> check)
        {
            foreach(float speed in new[]{.5f,1,1.7f,3})
            foreach(int lane in LaneLayout.VisualOrder)
            {
                GameSettings.NoteSpeed=speed;double travel=GameSettings.NoteTravelSeconds;
                var headObject=new GameObject("UniformHead");headObject.AddComponent<SpriteRenderer>();var head=headObject.AddComponent<NoteController>();
                var nextObject=new GameObject("UniformNext");nextObject.AddComponent<SpriteRenderer>();var next=nextObject.AddComponent<NoteController>();
                head.Initialize(new NoteData{timestamp=10,laneIndex=lane,duration=.12},travel);next.Initialize(new NoteData{timestamp=10.12,laneIndex=lane,speedMultiplier=1.8f},travel);
                var line=headObject.GetComponent<LineRenderer>();float initialGap=-1,initialLength=-1,initialWidth=-1;Vector3 initialSize=Vector3.zero;bool uniform=true;
                foreach(float p in new[]{.20f,.40f,.60f,.80f,.98f})
                {
                    double time=10-travel*(1-p);head.UpdatePosition(time);next.UpdatePosition(time);
                    float gap=Vector3.Distance(head.transform.position,next.transform.position),length=Vector3.Distance(line.GetPosition(0),line.GetPosition(line.positionCount-1));
                    if(initialGap<0){initialGap=gap;initialLength=length;initialWidth=line.startWidth;initialSize=head.transform.localScale;}
                    uniform&=Mathf.Abs(gap-initialGap)<.0001f&&Mathf.Abs(length-initialLength)<.0001f&&line.startWidth==initialWidth&&head.transform.localScale==initialSize;
                }
                check(uniform&&initialGap>.01&&Math.Abs(initialGap-initialLength)<.0001,"Equal time intervals retain visible spacing, hold length, width and head size through the entire approach: lane "+lane+" at "+speed+"x");
                Vector2 start=EpicStageLayout.RoutePixel(lane,0),end=EpicStageLayout.RoutePixel(lane,1);
                check(start.x==end.x&&EpicStageLayout.RoadHalfWidth(start.y)==EpicStageLayout.RoadHalfWidth(end.y),"Column "+lane+" is parallel and has no depth compression");
                UnityEngine.Object.Destroy(headObject);UnityEngine.Object.Destroy(nextObject);
            }
            GameSettings.NoteSpeed=1;
        }
        static void Frame(double time)
        {paused.SetValue(AudioManager.Instance,false);origin.SetValue(AudioManager.Instance,AudioSettings.dspTime-(time-GameSettings.AudioOffsetSec));typeof(NoteSpawner).GetMethod("Update",Hidden).Invoke(NoteSpawner.Instance,null);}
        static void PrepareLiveSpeed()
        {
            silence=AudioClip.Create("ScrollValidationSilence",48000*20,1,48000,false);var chart=ScriptableObject.CreateInstance<ChartData>();chart.audioClip=silence;chart.notes.Add(new NoteData{timestamp=6,laneIndex=0,duration=1});chart.notes.Add(new NoteData{timestamp=10,laneIndex=4,duration=.8});
            GameplayUI.IsTestPlayFromEditor=true;ChartManager.Instance.SelectChart(chart);NoteSpawner.Instance.LoadChart(chart);AudioManager.Instance.PlaySong(silence,10);GameManager.Instance.StartGame();
        }
        static void LiveSpeed(Action<bool,string> check)
        {
            Frame(3);
            check(((List<NoteController>)active.GetValue(NoteSpawner.Instance)).Count==0,"Notes spawn according to the chosen reading window");GameSettings.NoteSpeed=.5f;Frame(3);var list=(List<NoteController>)active.GetValue(NoteSpawner.Instance);check(list.Count==1&&list[0].gameObject.activeSelf,"Slower speed reveals notes earlier");
            var note=list[0];double timestamp=note.TargetTimestamp;GameSettings.NoteSpeed=3;check(!note.gameObject.activeSelf,"Faster speed hides distant notes instead of leaving them frozen at the horizon");
            Frame(5.5);check(note.gameObject.activeSelf&&note.TargetTimestamp==timestamp,"A hidden note returns at its original timestamp");Frame(6);SendKeys(InputManager.Instance.GetKeyForLane(0));AudioManager.Instance.PauseSong();GameManager.Instance.SetState(GameState.Paused);SendKeys();
            SendKeys(Key.F3);SendKeys();check(Math.Abs(GameSettings.NoteSpeed-2.9f)<.001,"F3 reduces scroll speed during pause");check(note.IsHolding&&!note.IsMissed,"Changing scroll while paused does not drop an active hold");SendKeys(Key.F4);SendKeys();check(GameSettings.NoteSpeed==3,"F4 increases scroll speed without altering audio pitch");
            check(!InputManager.ValidKey(Key.F3)&&!InputManager.ValidKey(Key.F4),"Speed shortcuts are reserved from gameplay remapping");GameSettings.NoteSpeed=1;NoteSpawner.Instance.StopAndClear();AudioManager.Instance.StopSong();GameManager.Instance.SetState(GameState.MainMenu);
        }
        static void ClockOffset(Action<bool,string> check)
        {
            var am=AudioManager.Instance;GameSettings.AudioOffsetSec=0;am.PlaySong(silence,1,5);am.PauseSong();double frozen=am.SongTime;
            GameSettings.AudioOffsetSec=-.125f;check(Math.Abs(am.SongTime-(frozen-.125))<.0001,"Offset really shifts the paused note/judgment clock by 125 ms");
            var music=(AudioSource)typeof(AudioManager).GetField("musicSource",Hidden).GetValue(am);float before=music.time;am.ResumeSong();check(Math.Abs(music.time-before)<.025&&music.pitch==1,"Applying offset and resuming does not seek or change the song's pitch");
        }
        static void RecoverySpeed(Action<bool,string> check)
        {
            var guide=TutorialGuide.Instance;
            typeof(TutorialGuide).GetField("<CurrentLesson>k__BackingField",Hidden).SetValue(guide,5);
            GameSettings.NoteSpeed=.5f;
            typeof(TutorialGuide).GetMethod("StartRecovery",Hidden).Invoke(guide,null);
            var pending=(List<NoteData>)typeof(NoteSpawner).GetField("pendingNotes",Hidden).GetValue(NoteSpawner.Instance);
            var next=pending.Single();
            check(next.timestamp-AudioManager.Instance.SongTime>=GameSettings.NoteTravelSeconds,"Tutorial recovery respects the slowest full reading window");
            check(RhythmWare.Malware.MalwareEffectManager.Instance.Infections.Remaining(RhythmWare.Malware.InfectionKind.Spyware)>next.timestamp-AudioManager.Instance.SongTime,"Tutorial infection remains active until its cleanup example arrives");
            ((GameObject)typeof(TutorialGuide).GetField("panel",Hidden).GetValue(guide)).SetActive(false);
            NoteSpawner.Instance.StopAndClear();RhythmWare.Malware.MalwareEffectManager.Instance.ResetInfections();GameManager.Instance.TrainingSafety=false;GameSettings.NoteSpeed=1;GameManager.Instance.SetState(GameState.MainMenu);
        }
    }
}
#endif
