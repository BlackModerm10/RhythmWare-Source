#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using RhythmWare.Core;
using RhythmWare.Rhythm;
using RhythmWare.Malware;
using RhythmWare.UI;

namespace RhythmWare.EditorTools
{
    public static class RhythmComfortValidation
    {
        static UnityWebRequest request;
        static AudioClip imported,disk;
        static string output;
        static double deadline;
        static int step;
        static float before;
        static bool reduced,initialized;
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        public static void Restore(){if(initialized)VisualPreferences.ReducedEffects=reduced;}
        public static bool Run(Action<bool,string> check,Action<string> capture,Action<int,int> size)
        {
            var gm=GameManager.Instance;var fx=MalwareEffectManager.Instance;
            switch(step)
            {
                case 0:
                    initialized=true;reduced=VisualPreferences.ReducedEffects;VisualPreferences.ReducedEffects=false;
                    var chart=CampaignCatalog.CreateChart("Dinner");
                    check(chart.notes.Count==286&&chart.notes.Count(n=>n.isHoldNote)==4&&chart.notes.Count(n=>n.laneIndex==4)==20,"Comfort map reduces 536 notes to 286, with four isolated holds and twenty SPACE accents");
                    check(chart.notes.Zip(chart.notes.Skip(1),(a,b)=>b.timestamp-a.timestamp>=.284&&b.timestamp-a.endTimestamp>=.249).All(v=>v),"Every input has breathing room and every hold is isolated");
                    var sides=chart.notes.Where(n=>n.laneIndex<4).ToArray();
                    check(sides.Zip(sides.Skip(1),(a,b)=>a.laneIndex/2!=b.laneIndex/2).All(v=>v),"All side inputs alternate hands rather than forcing same-hand jumps");
                    var timing=TimingWindow.ForChart(chart);var normal=TimingWindow.ForChart(CampaignCatalog.CreateChart("Jack"));
                    check(timing.Evaluate(.110)==HitRating.Good&&normal.Evaluate(.110)==HitRating.Miss,"Only the accessible chart grants a wider Good window");
                    check(Math.Abs(ChartData.FromJson(chart.ToJson()).timingWindowScale-1.35f)<.001,"Accessible timing survives editor export and reload");
                    Stage();var note=Make(null,0);double t=note.TargetTimestamp+.110;
                    typeof(AudioManager).GetField("isPaused",Hidden).SetValue(AudioManager.Instance,false);
                    typeof(AudioManager).GetField("dspSongStart",Hidden).SetValue(AudioManager.Instance,AudioSettings.dspTime-(t-GameSettings.AudioOffsetSec));
                    typeof(NoteSpawner).GetMethod("HandleLanePressed",Hidden).Invoke(NoteSpawner.Instance,new object[]{0});
                    check(note.IsHit&&gm.GoodHits==1&&gm.MissHits==0,"Actual input dispatch accepts a 110ms late hit instead of prematurely killing the note");
                    AudioManager.Instance.PauseSong();Stage();
                    var trojan=Make(ScriptableObject.CreateInstance<TrojanData>(),3);var plain=Make(null,0);
                    check(Math.Abs(trojan.TravelDuration-plain.TravelDuration)<.0001,"Trojan and normal data share a consistent travel time");
                    plain.UpdatePosition(plain.TargetTimestamp);typeof(NotePresentation).GetMethod("LateUpdate",Hidden).Invoke(plain.GetComponent<NotePresentation>(),null);
                    check(Vector3.Distance(plain.transform.position,LaneController.Instance.GetHitboxPosition(0))<.0001&&plain.transform.Find("SignatureOutline").GetComponent<SpriteRenderer>().color.a>.9f,"Bright arrival diamond coincides with the receptor at the actual judgment time");
                    Stage();Make(ScriptableObject.CreateInstance<SpywareData>(),0).OnMissed();before=gm.CurrentHealth;size(1920,1080);step++;return false;
                case 1:
                    check(gm.CurrentHealth<before-1&&fx.InfectionVisuals.DamageShown(InfectionKind.Spyware)>1,"Visible Spyware counter records real firewall loss");
                    fx.InfectionVisuals.Render(0,true);capture("spyware-steals-firewall-1920.png");
                    var hud=UnityEngine.Object.FindAnyObjectByType<GameplayHUD>();
                    var label=(TMPro.TextMeshProUGUI)typeof(GameplayHUD).GetField("integrity",Hidden).GetValue(hud);
                    check(label.text.StartsWith("FUGA"),"The main firewall plate identifies the active leak rather than leaving a generic label");
                    VisualPreferences.ReducedEffects=true;size(1280,720);step++;return false;
                case 2:
                    fx.TriggerSpywareGlitch(0);fx.InfectionVisuals.Render(0,true);capture("spyware-static-warning-1280.png");
                    check(fx.Infections.LeakPerSecond==3&&fx.InfectionVisuals.DamageShown(InfectionKind.Spyware)>0,"Reduced effects preserves both the effect's meaning and its damage counter");
                    VisualPreferences.ReducedEffects=false;Stage();Make(ScriptableObject.CreateInstance<TrojanData>(),3).OnMissed();before=gm.CurrentHealth;gm.RegisterHit(HitRating.Miss,0);
                    check(Math.Abs(before-gm.CurrentHealth-21)<.01&&Math.Abs(fx.InfectionVisuals.DamageShown(InfectionKind.Trojan)-6)<.01,"Trojan displays the exact six extra firewall points caused by an ordinary miss");
                    size(1920,1080);step++;return false;
                case 3:
                    gm.RegisterHit(HitRating.Miss,0);fx.InfectionVisuals.Render(0,true);capture("trojan-amplifies-miss-1920.png");
                    hud=UnityEngine.Object.FindAnyObjectByType<GameplayHUD>();label=(TMPro.TextMeshProUGUI)typeof(GameplayHUD).GetField("integrity",Hidden).GetValue(hud);
                    check(label.text=="VULNERABLE","The firewall plate identifies the Trojan backdoor during play");
                    var judgment=(TMPro.TextMeshProUGUI)typeof(GameplayHUD).GetField("judgement",Hidden).GetValue(hud);
                    check(judgment.rectTransform.anchorMin.y>.7f,"MISS feedback is relocated above the lanes instead of covering the infection explanation");
                    fx.TriggerNeutralized(ScriptableObject.CreateInstance<TrojanData>(),3);
                    check(!fx.Infections.Active(InfectionKind.Trojan)&&fx.Infections.MissMultiplier==1,"Interception visibly restores ordinary miss damage");
                    gm.SetState(GameState.MainMenu);AudioManager.Instance.StopSong();NoteSpawner.Instance.StopAndClear();Restore();return true;
            }
            return false;
        }
        static void Stage()
        {
            var am=AudioManager.Instance;NoteSpawner.Instance.StopAndClear();
            foreach(var n in UnityEngine.Object.FindObjectsByType<NoteController>())UnityEngine.Object.Destroy(n.gameObject);
            var chart=CampaignCatalog.CreateChart("Dinner");chart.audioClip=ChartEditorManager.Instance.ActiveChart.audioClip;
            chart.notes=Enumerable.Range(0,30).Select(i=>new NoteData{timestamp=1000+i,laneIndex=i%5}).ToList();
            typeof(GameplayUI).GetMethod("HideAllOverlays",Hidden).Invoke(UnityEngine.Object.FindAnyObjectByType<GameplayUI>(FindObjectsInactive.Include),null);
            GameplayUI.IsTestPlayFromEditor=true;ChartManager.Instance.SelectChart(chart);NoteSpawner.Instance.LoadChart(chart);
            am.PlaySong(chart.audioClip,1,20);am.PauseSong();NoteSpawner.Instance.StopAndClear();GameManager.Instance.StartGame();
        }
        static NoteController Make(MalwareData malware,int lane)
        {
            var go=new GameObject("ComfortEffectFixture");go.AddComponent<SpriteRenderer>();var note=go.AddComponent<NoteController>();
            note.Initialize(new NoteData{timestamp=AudioManager.Instance.SongTime+1,laneIndex=lane,malwareData=malware},2.4,TimingWindow.ForChart(ChartManager.Instance.SelectedChart).missWindowSec);
            note.UpdatePosition(note.TargetTimestamp-.6);
            // Real input dispatch uses the spawner's active-note collection.
            ((List<NoteController>)typeof(NoteSpawner).GetField("activeNotes",Hidden).GetValue(NoteSpawner.Instance)).Add(note);
            return note;
        }
        public static void RunAudioProbe()
        {
            output=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"docs/comfort-validation");Directory.CreateDirectory(output);
            string path=Path.Combine(Application.dataPath,"Audio/Music/Levels/DINNER-Hyper-Digital.mp3");
            imported=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Levels/DINNER-Hyper-Digital.mp3");imported.LoadAudioData();
            request=UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri,AudioType.MPEG);request.SendWebRequest();
            deadline=EditorApplication.timeSinceStartup+120;EditorApplication.update+=Probe;
        }
        static void Probe()
        {
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Audio probe timeout");
                if(!request.isDone||imported.loadState!=AudioDataLoadState.Loaded)return;
                if(request.result!=UnityWebRequest.Result.Success)throw new Exception(request.error);
                disk=DownloadHandlerAudioClip.GetContent(request);
                AudioSettings.GetDSPBufferSize(out int size,out int buffers);
                var lines=new List<string>{"offsetSeconds="+GameSettings.AudioOffsetSec,"outputRate="+AudioSettings.outputSampleRate,"bufferSize="+size,"buffers="+buffers};
                foreach(var pair in new[]{("imported",imported),("disk",disk)})
                {
                    var clip=pair.Item2;var pcm=new float[clip.samples*clip.channels];if(!clip.GetData(pcm,0))throw new Exception("PCM unavailable: "+pair.Item1);
                    using(var writer=new BinaryWriter(File.Create(Path.Combine(output,pair.Item1+".f32"))))
                        for(int i=0;i<clip.samples;i+=8){float sum=0;int count=0;for(int f=i;f<Math.Min(i+8,clip.samples);f++)for(int c=0;c<clip.channels;c++){sum+=pcm[f*clip.channels+c];count++;}writer.Write(sum/count);}
                    lines.Add(pair.Item1+"="+clip.name+","+clip.frequency+","+clip.channels+","+clip.samples+","+clip.length);
                }
                File.WriteAllLines(Path.Combine(output,"audio-probe.txt"),lines);EditorApplication.update-=Probe;request.Dispose();EditorApplication.Exit(0);
            }
            catch(Exception ex){File.WriteAllText(Path.Combine(output,"probe-error.txt"),ex.ToString());EditorApplication.update-=Probe;EditorApplication.Exit(1);}
        }
    }
}
#endif
