#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using RhythmWare.Core;
using RhythmWare.Rhythm;
using RhythmWare.UI;
using RhythmWare.Boss;

namespace RhythmWare.EditorTools
{
    // A deterministic autoplay fixture drives the actual spawner, mapped hold keys,
    // hit/health logic and renderer. It is never attached to production gameplay.
    public static class OsuBossChartValidation
    {
        static int step,frames;
        static ChartData chart;
        static double time,duration;
        static bool initialized,reduced;
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly FieldInfo origin=typeof(AudioManager).GetField("dspSongStart",Private),paused=typeof(AudioManager).GetField("isPaused",Private),active=typeof(NoteSpawner).GetField("activeNotes",Private);
        static readonly MethodInfo update=typeof(NoteSpawner).GetMethod("Update",Private),late=typeof(NotePresentation).GetMethod("LateUpdate",Private);
        static string Root=>Directory.GetParent(Application.dataPath).FullName;
        public static void Restore()
        {
            if(!initialized)return;VisualPreferences.ReducedEffects=reduced;
            if(Keyboard.current!=null){InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());InputSystem.Update();}
        }
        public static bool Run(Action<bool,string> check,Action<string> capture,Action<int,int> size)
        {
            var gm=GameManager.Instance;var am=AudioManager.Instance;var editor=ChartEditorManager.Instance;
            switch(step)
            {
                case 0:
                    initialized=true;reduced=VisualPreferences.ReducedEffects;VisualPreferences.ReducedEffects=false;
                    chart=CampaignCatalog.CreateChart("Dinner");Validate(check);
                    MainMenuUI.Instance.OpenLevelSelect();
                    typeof(LevelSelectUI).GetMethod("Select",Private).Invoke(UnityEngine.Object.FindAnyObjectByType<LevelSelectUI>(),new object[]{2});
                    size(1920,1080);step++;return false;
                case 1:
                    capture("osu-bonzi-selector-1920.png");
                    MainMenuUI.Instance.OpenEditor();check(editor.OpenCatalogChart(chart),"Adapted boss opens as an editable official-chart copy");step++;return false;
                case 2:
                    if(editor.ActiveChart.audioClip==null)return false;
                    chart.audioClip=editor.ActiveChart.audioClip;duration=chart.audioClip.length;
                    check(duration>135.3&&duration<138&&chart.notes.All(n=>n.endTimestamp<duration),"Every retained head and hold tail fits the exact osu audio asset");
                    check(editor.ActiveChart.bossPhaseStarts.SequenceEqual(chart.bossPhaseStarts)&&editor.ActiveChart.notes.Count==286,"Editor preserves musical boss phases and every adapted note");
                    capture("osu-bonzi-editor-1920.png");
                    check(editor.TestPlayCurrentChart(),"Editor starts the adapted chart through the existing test-play path");
                    check(am.SongTime< -chart.approachSeconds,"Audio pre-roll leaves the first time-zero note its full approach");
                    am.PauseSong();double frozen=am.SongTime;am.ResumeSong();
                    check(Math.Abs(am.SongTime-frozen)<.04&&(double)typeof(AudioManager).GetField("scheduledAudioStart",Private).GetValue(am)>AudioSettings.dspTime+chart.approachSeconds-.1,"Pausing during the opening preserves the remaining scheduled lead-in and note clock");
                    am.PlaySong(chart.audioClip,chart.PlaybackLeadIn,20);am.PauseSong();am.ResumeSong();
                    var music=(AudioSource)typeof(AudioManager).GetField("musicSource",Private).GetValue(am);
                    check(Math.Abs(music.time-20)<.05,"Pausing an editor seek before its scheduled cue preserves the original audio position");
                    ChartManager.Instance.SelectChart(chart);GameplayUI.IsTestPlayFromEditor=false;
                    UnityEngine.Object.FindAnyObjectByType<GameplayUI>().RestartGame();
                    check(am.SongTime< -chart.approachSeconds&&gm.CurrentState==GameState.Playing,"Campaign retry also restores the full approach before the time-zero opening note");
                    GameplayUI.IsTestPlayFromEditor=true;
                    am.PauseSong();NoteSpawner.Instance.StopAndClear();
                    ChartManager.Instance.SelectChart(chart);NoteSpawner.Instance.LoadChart(chart);gm.StartGame();
                    am.PlaySong(chart.audioClip,chart.PlaybackLeadIn);am.PauseSong();time=-chart.PlaybackLeadIn;
                    check(BonziEncounter.PhaseAt(41.633,duration)==1&&BonziEncounter.PhaseAt(41.634,duration)==2&&BonziEncounter.PhaseAt(96.296,duration)==2&&BonziEncounter.PhaseAt(96.297,duration)==3,"Boss phases follow chart phrases and the end of its musical break");
                    size(640,360);step++;return false;
                case 3:
                    double until=Math.Min(duration+.1,time+8);
                    while(time<until&&gm.CurrentState==GameState.Playing)
                    {
                        time=Math.Round(time+.02,3);Frame(time);
                        // Eight seconds of the real source-derived opening phrase.
                        if(frames<80&&time>=12+frames*.1-.001)
                        {
                            Present(time);capture("boss-chart-"+frames.ToString("D3")+".png");frames++;
                        }
                    }
                    am.PauseSong();InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());InputSystem.Update();
                    check(gm.MissHits==0,"Autoplay has no timing, hold or lane collision misses through "+time.ToString("0.0")+"s");
                    if(gm.CurrentState!=GameState.Victory&&time<duration+.1)return false;
                    check(gm.CurrentState==GameState.Victory&&gm.PerfectHits==290&&gm.GoodHits==0,"The complete adapted chart reaches victory with exactly 290 judgments");
                    check(BonziStageController.Instance.IsContained&&gm.CurrentHealth==100,"The dynamic boss target is reachable and full-chart interception is survivable");
                    check(frames==80,"Native preview contains eighty source-timed gameplay frames");
                    size(1920,1080);step++;return false;
                case 4:
                    Fixture(102.5);Present(102.5);capture("osu-bonzi-finale-1920.png");
                    check(BonziStageController.Instance.Phase==3,"Final chorus fixture shows the Trojan phase on its actual chart");
                    check(!((GameObject)typeof(GameplayUI).GetField("victoryPanel",Private).GetValue(UnityEngine.Object.FindAnyObjectByType<GameplayUI>())).activeSelf,"Finale preview contains gameplay instead of a stale victory overlay");
                    NoteSpawner.Instance.StopAndClear();am.StopSong();gm.SetState(GameState.MainMenu);Restore();return true;
            }
            return false;
        }
        static void Frame(double songTime)
        {
            var am=AudioManager.Instance;
            paused.SetValue(am,false);origin.SetValue(am,AudioSettings.dspTime-(songTime-GameSettings.AudioOffsetSec));
            var keys=chart.notes.Where(n=>n.isHoldNote&&songTime>=n.timestamp&&songTime<=n.endTimestamp+.04).Select(n=>InputManager.Instance.GetKeyForLane(n.laneIndex)).Distinct().ToArray();
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(keys));InputSystem.Update();
            update.Invoke(NoteSpawner.Instance,null);
            foreach(var n in ((List<NoteController>)active.GetValue(NoteSpawner.Instance)).ToArray())
            {
                if(n==null)continue;
                if(!n.IsHit&&!n.IsMissed&&!n.IsHolding&&n.TargetTimestamp<=songTime+.001)n.OnHit(HitRating.Perfect);
                if(n.IsHit||n.IsMissed)n.gameObject.SetActive(false);
            }
        }
        static void Present(double t)
        {
            foreach(var n in ((List<NoteController>)active.GetValue(NoteSpawner.Instance)).ToArray())
            {if(n==null||n.IsHit||n.IsMissed)continue;var view=n.GetComponent<NotePresentation>();if(view!=null)late.Invoke(view,null);}
            BonziStageController.Instance.RefreshPresentation(t);
            UnityEngine.Object.FindAnyObjectByType<FluidBackdrop>().RenderAt(t);
        }
        static void Fixture(double at)
        {
            typeof(GameplayUI).GetMethod("HideAllOverlays",Private).Invoke(UnityEngine.Object.FindAnyObjectByType<GameplayUI>(),null);
            ChartManager.Instance.SelectChart(chart);NoteSpawner.Instance.LoadChart(chart);GameManager.Instance.StartGame();
            AudioManager.Instance.PlaySong(chart.audioClip,1,at);AudioManager.Instance.PauseSong();NoteSpawner.Instance.StopAndClear();
            var spawn=typeof(NoteSpawner).GetMethod("SpawnNote",Private);
            foreach(var n in chart.notes.Where(n=>n.timestamp>=at&&n.timestamp<=at+chart.approachSeconds))spawn.Invoke(NoteSpawner.Instance,new object[]{n});
            foreach(var n in (List<NoteController>)active.GetValue(NoteSpawner.Instance))n.UpdatePosition(at);
        }
        static void Validate(Action<bool,string> check)
        {
            string source=File.ReadAllText(Path.Combine(Root,"docs/chart-sources/DINNER-Blau-4-Hyper-Digital.osu"));
            var rows=source.Split(new[]{"[HitObjects]"},StringSplitOptions.None)[1].Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Where(l=>!l.StartsWith("//")).Select(l=>l.Split(',')).ToArray();
            var times=new HashSet<long>(rows.Select(v=>long.Parse(v[2])));
            check(rows.Length==1190&&rows.Count(v=>(int.Parse(v[3])&128)!=0)==173,"Original Blau-4 chart is identified as 1190 objects and 173 holds");
            check(chart.notes.Count==286&&chart.notes.Count(n=>n.isHoldNote)==4&&chart.notes.Count(n=>n.laneIndex==4)==20,"Balanced arrangement has 286 notes, four holds and twenty central accents");
            check(chart.notes.All(n=>times.Contains((long)Math.Round(n.timestamp*1000)-96)),"Every adapted hit occurs at an source-chart timestamp plus the measured 96ms alignment");
            check(chart.notes.Where(n=>n.isHoldNote).All(n=>rows.Any(v=>long.Parse(v[2])==Math.Round(n.timestamp*1000)-96&&(int.Parse(v[3])&128)!=0&&long.Parse(v[5].Split(':')[0])==Math.Round(n.endTimestamp*1000)-96)),"All retained hold tails preserve source endpoints with the same 96ms alignment");
            check(!chart.notes.Any(n=>n.timestamp>=86.834&&n.timestamp<96.297),"The nine-second source break remains free of invented notes");
            check(chart.notes.All(n=>chart.notes.Count(m=>m.timestamp<=n.timestamp&&m.endTimestamp>=n.timestamp)<=1),"No adapted moment requires more than one key including active holds");
            check(chart.notes.Max(n=>chart.notes.Count(m=>m.timestamp>=n.timestamp&&m.timestamp<n.timestamp+1))==4,"Peak density is capped at four notes per second");
            check(chart.offsetMs==96&&chart.bpm==156&&Math.Abs(chart.notes[0].timestamp-.096)<.0001,"Measured alignment and 156 BPM are retained");
            check(chart.StableId=="rw-campaign-dinner-5keys-comfort-v2","Adaptation has an independent leaderboard identity");
            var round=ChartData.FromJson(chart.ToJson());
            check(round.bossPhaseStarts.SequenceEqual(chart.bossPhaseStarts)&&round.notes.Count==286,"JSON/editor export preserves musical phase metadata and chart notes");
            bool invalid=false;try{ChartData.FromJson(chart.ToJson().Replace("41.634","-41.634"));}catch(InvalidDataException){invalid=true;}
            check(invalid,"Malformed phase data is rejected on import");
            var legacy=CampaignCatalog.CreateChart("Sara");ChartManager.Instance.SelectChart(legacy);
            check(BonziEncounter.PhaseAt(40,100)==2&&BonziEncounter.PhaseAt(80,100)==3,"Charts without musical phase metadata retain the existing fractional phase fallback");
            using(var hash=SHA256.Create())
            {
                string actual=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,"Audio/Music/Levels/DINNER-Hyper-Digital.mp3")))).Replace("-","").ToLowerInvariant();
                string expected=File.ReadAllText(Path.Combine(Root,"docs/osu-bonzi-validation/source-audio-sha256.txt")).Trim();
                check(actual==expected,"Imported audio is byte-identical to the file referenced by the supplied osu chart");
            }
        }
    }
}
#endif
