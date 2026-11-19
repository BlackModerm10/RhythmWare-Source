#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using RhythmWare.Core;
using RhythmWare.Rhythm;
using RhythmWare.UI;

namespace RhythmWare.EditorTools
{
    public static class ChromaChartValidation
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static int step;static bool initialized,captured;static ChartData chart;static LevelItemInfo entry;static Keyboard keyboard;static double clock;
        static readonly FieldInfo active=typeof(NoteSpawner).GetField("activeNotes",Hidden),paused=typeof(AudioManager).GetField("isPaused",Hidden),origin=typeof(AudioManager).GetField("dspSongStart",Hidden);
        static readonly MethodInfo spawnUpdate=typeof(NoteSpawner).GetMethod("Update",Hidden),inputUpdate=typeof(InputManager).GetMethod("Update",Hidden),present=typeof(NotePresentation).GetMethod("LateUpdate",Hidden);
        public static void Restore()
        {
            if(!initialized)return;initialized=false;InputManager.Instance?.UseValidationStore(null);
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);
        }
        public static bool Run(Action<bool,string> check,Action<string> capture,Action<int,int> size)
        {
            var gm=GameManager.Instance;var editor=ChartEditorManager.Instance;
            switch(step)
            {
                case 0:
                    initialized=true;InputManager.Instance.UseValidationStore("RWChromaQA_");keyboard=InputSystem.AddDevice<Keyboard>();
                    chart=CampaignCatalog.CreateChart("Jack");Validate(check);MainMenuUI.Instance.OpenLevelSelect();step++;return false;
                case 1:
                    var selector=UnityEngine.Object.FindAnyObjectByType<LevelSelectUI>();var catalog=selector.RefreshCatalog();entry=catalog[1];
                    check(entry.songTitle=="[@__@]"&&entry.artist=="Chroma"&&entry.chartData.mapper=="Anwul_","The normal campaign slot displays Chroma and the original mapper");
                    typeof(LevelSelectUI).GetMethod("Select",Hidden).Invoke(selector,new object[]{1});step++;return false;
                case 2:
                    if(entry.audioClip==null)return false;chart.audioClip=entry.audioClip;
                    check(chart.audioClip.length>100.704f&&chart.audioClip.length<120,"The supplied full MP3 loads through the game's disk-audio path");
                    capture("chroma-normal-selector-1920.png");MainMenuUI.Instance.OpenEditor();step++;return false;
                case 3:
                    check(editor.OpenCatalogChart(chart),"The replacement map opens in the editor");
                    var round=ChartData.FromJson(editor.ActiveChart.ToJson());check(round.notes.Count==308&&round.notes.Count(n=>n.isHoldNote)==33&&round.notes.Any(n=>n.laneIndex==4&&n.isHoldNote),"Editor export preserves every note, all holds and center holds");
                    UnityEngine.Object.FindAnyObjectByType<EditorTimeline>().SetWindow(24,12);editor.Seek(26.819);step++;return false;
                case 4:
                    if(!UnityEngine.Object.FindAnyObjectByType<WaveformRenderer>().HasSamples)return false;
                    capture("chroma-normal-editor-1920.png");GameplayUI.IsTestPlayFromEditor=true;ChartManager.Instance.SelectChart(chart);NoteSpawner.Instance.LoadChart(chart);
                    AudioManager.Instance.PlaySong(chart.audioClip,chart.PlaybackLeadIn);gm.StartGame();AudioManager.Instance.PauseSong();clock=-chart.PlaybackLeadIn;step++;return false;
                case 5:
                    double until=clock+8;
                    while(gm.CurrentState==GameState.Playing&&(clock<until||((List<NoteController>)active.GetValue(NoteSpawner.Instance)).Any(n=>n!=null&&n.IsHolding)))
                    {
                        clock=Math.Round(clock+.005,4);Frame(clock);
                        if(!captured&&clock>=40){captured=true;foreach(var n in ((List<NoteController>)active.GetValue(NoteSpawner.Instance)).ToArray())if(n!=null&&!n.IsHit&&!n.IsMissed)present.Invoke(n.GetComponent<NotePresentation>(),null);UnityEngine.Object.FindAnyObjectByType<FluidBackdrop>().RenderAt(clock);capture("chroma-normal-playing-1920.png");}
                    }
                    AudioManager.Instance.PauseSong();check(gm.MissHits==0&&gm.GoodHits==0,"Exact chart input through "+clock.ToString("0.00")+"s: "+gm.PerfectHits+" Perfect, no Good/Miss");
                    if(gm.CurrentState==GameState.Playing&&clock<125)return false;
                    check(gm.CurrentState==GameState.Victory&&gm.PerfectHits==341,"All 308 original heads and 33 original hold tails reach victory, including the six doubles");
                    check(!TutorialGuide.Instance.IsTraining&&!RhythmWare.Boss.BonziStageController.Instance.IsEncounter,"The imported normal level does not activate tutorial or boss mechanics");
                    AudioManager.Instance.StopSong();gm.SetState(GameState.MainMenu);Restore();return true;
            }
            return false;
        }
        static void Frame(double time)
        {
            var audio=AudioManager.Instance;paused.SetValue(audio,false);origin.SetValue(audio,AudioSettings.dspTime-(time-GameSettings.AudioOffsetSec));
            var keys=chart.notes.Where(n=>time>=n.timestamp-.004&&time<=n.endTimestamp+.025).Select(n=>InputManager.Instance.GetKeyForLane(n.laneIndex)).Distinct().ToArray();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();keyboard.MakeCurrent();inputUpdate.Invoke(InputManager.Instance,null);spawnUpdate.Invoke(NoteSpawner.Instance,null);
            foreach(var n in ((List<NoteController>)active.GetValue(NoteSpawner.Instance)).ToArray())if(n!=null&&(n.IsHit||n.IsMissed))n.gameObject.SetActive(false);
        }
        static void Validate(Action<bool,string> check)
        {
            string root=Directory.GetParent(Application.dataPath).FullName;
            string source=File.ReadAllText(Path.Combine(root,"docs/chart-sources/Chroma-Anwul-e-y.osu"));
            var objects=source.Split(new[]{"[HitObjects]"},StringSplitOptions.None)[1].Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Select(l=>l.Split(',')).OrderBy(v=>int.Parse(v[2])).ThenBy(v=>int.Parse(v[0])).ToArray();
            var notes=chart.notes.OrderBy(n=>n.timestamp).ThenBy(n=>Array.IndexOf(LaneLayout.VisualOrder,n.laneIndex)).ToArray();int[] columns={0,1,4,2,3};
            check(objects.Length==308&&notes.Length==308,"No source objects are dropped or inserted");
            for(int i=0;i<objects.Length;i++)
            {
                var v=objects[i];double start=int.Parse(v[2])/1000.0,end=(int.Parse(v[3])&128)!=0?int.Parse(v[5].Split(':')[0])/1000.0:start;
                check(Math.Abs(notes[i].timestamp-start)<1e-9&&Math.Abs(notes[i].endTimestamp-end)<1e-9&&notes[i].laneIndex==columns[Math.Min(4,int.Parse(v[0])*5/512)],"Original object "+i+": exact head, tail and physical column");
            }
            check(chart.notes.Count(n=>n.malwareData!=null)==9&&chart.notes.Where(n=>n.malwareData!=null).All(n=>!n.isHoldNote),"Only nine existing single taps become viruses");
            check(chart.notes.Where(n=>n.malwareData!=null).GroupBy(n=>n.malwareData.GetType()).All(g=>g.Count()==3),"Spyware, Adware and Trojan appear three times each");
            check(chart.bpm==158&&chart.offsetMs==1000&&chart.playableEndSeconds==0&&chart.backgroundTheme=="data-rush","Original tempo and time origin use the normal level's complete-song environment");
            string expected=File.ReadAllText(Path.Combine(root,"docs/chroma-validation/source-audio-sha256.txt")).Trim();
            using(var sha=SHA256.Create()){string actual=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(GameContentPaths.ResolveAudio(chart.audioClipPath)))).Replace("-","").ToLowerInvariant();check(actual==expected,"Bundled Chroma audio is byte-identical to the supplied osu MP3");}
            check(CampaignCatalog.CreateChart("Sara").notes.Count==201&&CampaignCatalog.CreateChart("Dinner").notes.Count==898,"Sara and Bonzi retain their current authored maps");
        }
    }
}
#endif
