#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using RhythmWare.Core;
using RhythmWare.Rhythm;
using RhythmWare.UI;
using RhythmWare.Boss;
using RhythmWare.Malware;
using RhythmWare.Education;

namespace RhythmWare.EditorTools
{
    public static class BossLearningValidation
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static int step;static bool initialized;static Keyboard keyboard;static ChartData boss;static double clock;
        static readonly FieldInfo active=typeof(NoteSpawner).GetField("activeNotes",Hidden),paused=typeof(AudioManager).GetField("isPaused",Hidden),origin=typeof(AudioManager).GetField("dspSongStart",Hidden);
        public static void Restore()
        {
            if(!initialized)return;initialized=false;
            SocLearningFlow.Instance?.Close();TutorialGuide.Instance?.Cancel();GameSettings.UseRhythmValidationStore(null);InputManager.Instance?.UseValidationStore(null);SocLearningProgress.SetValidationPath(null);
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);
        }
        public static bool Run(Action<bool,string> check,Action<string> capture,Action<int,int> size)
        {
            var gm=GameManager.Instance;var am=AudioManager.Instance;var guide=TutorialGuide.Instance;var flow=SocLearningFlow.Instance;
            switch(step)
            {
                case 0:
                    initialized=true;GameSettings.UseRhythmValidationStore("RWBossLearningQA_");InputManager.Instance.UseValidationStore("RWBossLearningKeysQA_");
                    SocLearningProgress.SetValidationPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"docs/boss-learning-validation/soc-test-progress.json"));
                    keyboard=InputSystem.AddDevice<Keyboard>();boss=CampaignCatalog.CreateChart("Dinner");ValidateSource(check);Timing(check);
                    MainMenuUI.Instance.OpenLevelSelect();step++;return false;
                case 1:
                    var selector=UnityEngine.Object.FindAnyObjectByType<LevelSelectUI>();var entries=selector.RefreshCatalog();
                    typeof(LevelSelectUI).GetMethod("Select",Hidden).Invoke(selector,new object[]{2});step++;return false;
                case 2:
                    var level=UnityEngine.Object.FindAnyObjectByType<LevelSelectUI>().RefreshCatalog()[2];
                    if(level.audioClip==null)return false;boss.audioClip=level.audioClip;
                    check(boss.audioClip.length>119.422&&boss.audioClip.length<140,"New original boss MP3 loads through the game audio path");capture("dragon-boss-selector.png");
                    Prepare(boss);clock=-boss.PlaybackLeadIn;step++;return false;
                case 3:
                    double until=clock+8;while(gm.CurrentState==GameState.Playing&&clock<until)
                    {
                        clock=Math.Round(clock+.005,4);Frame(clock,boss);
                    }
                    BonziStageController.Instance.RefreshPresentation(clock);am.PauseSong();
                    check(gm.GoodHits==0&&gm.MissHits==0,"Exact Dragon input through "+clock.ToString("0.00")+": "+gm.PerfectHits+" Perfect, "+gm.GoodHits+" Good, "+gm.MissHits+" Miss; state="+gm.CurrentState);
                    if(clock>94&&clock<110){check(!BonziStageController.Instance.IsContained&&BonziStageController.Instance.Phase==3,"Bonzi survives into the final Trojan phase");capture("dragon-boss-final-phase.png");}
                    if(gm.CurrentState==GameState.Playing&&clock<150)return false;
                    check(gm.CurrentState==GameState.Victory&&gm.PerfectHits==898,"All 898 original Dragon heads reach boss victory without dropped chords");
                    check(BonziStageController.Instance.IsContained,"Bonzi is contained at the end of the complete attack");
                    GameplayUI.IsTestPlayFromEditor=false;SocLearningProgress.LastThreat="Adware";gm.StartGame();gm.TriggerGameOver();step++;return false;
                case 4:
                    check(flow.IsDefeatReview&&gm.CurrentState==GameState.Learning,"Real defeat opens a contextual SOC learning case");
                    check(flow.CurrentQuestion.relatedThreat=="Adware"&&!flow.CanRetry,"Defeat question matches the last threat and retry starts locked");
                    UnityEngine.Object.FindAnyObjectByType<GameplayUI>(FindObjectsInactive.Include).RestartGame();flow.Finish();Keys(Key.Space);Keys();
                    check(flow.IsDefeatReview&&gm.CurrentState==GameState.Learning,"Space, direct restart and premature continue cannot bypass the lesson");
                    capture("defeat-case-question.png");flow.Answer((flow.CurrentQuestion.correctIndex+1)%3);
                    check(!flow.CanRetry&&SocLearningProgress.Data.attempts.Last().source=="defeat"&&!SocLearningProgress.Data.attempts.Last().correct,"Wrong defeat answer records learning and reveals explanatory feedback");
                    step++;return false;
                case 5:
                    check(!flow.CanRetry,"Feedback cannot be skipped during its reading interval");capture("defeat-case-explanation.png");
                    typeof(SocLearningFlow).GetField("feedbackAt",Hidden).SetValue(flow,Time.unscaledTime-3.1f);
                    check(flow.CanRetry,"Retry unlocks after an answer and three seconds of feedback");flow.Finish();
                    check(gm.CurrentState==GameState.Playing&&!flow.IsOpen&&gm.PerfectHits==0,"Reviewed defeat restarts the current boss with fresh stats");
                    Stop();var sara=CampaignCatalog.CreateChart("Sara");sara.audioClip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Levels/Sara.mp3");
                    GameplayUI.IsTestPlayFromEditor=false;ChartManager.Instance.SelectChart(sara);NoteSpawner.Instance.LoadChart(sara);am.PlaySong(sara.audioClip,sara.PlaybackLeadIn);guide.Request(sara);gm.StartGame();step++;return false;
                case 6:
                    check(guide.IsTraining&&guide.CardOpen&&guide.CurrentLesson==0,"Single guided tutorial still starts at its first lesson");
                    for(int i=0;i<5;i++)guide.SkipLesson();step=61;return false;
                case 61:
                    var title=(TMPro.TextMeshProUGUI)typeof(TutorialGuide).GetField("heading",Hidden).GetValue(guide);
                    check(title.text=="SPYWARE","Malware tutorial title is SPYWARE without OBSERVA");capture("tutorial-spyware-title.png");
                    for(int i=0;i<5;i++)guide.SkipLesson();step=7;return false;
                case 7:
                    check(guide.IsTraining&&guide.CurrentLesson==10&&guide.CardOpen,"Guided tutorial includes the eleventh speed lesson");
                    capture("tutorial-speed-explanation.png");guide.StartTrial();step++;return false;
                case 8:
                    check(ChartManager.Instance.SelectedChart.notes.Count==6&&gm.TrainingSafety,"Speed example uses six playable protected notes");
                    double[] times=ChartManager.Instance.SelectedChart.notes.Select(n=>n.timestamp).ToArray();
                    GameSettings.NoteSpeed=1;Keys(Key.F4);Keys();check(Math.Abs(GameSettings.NoteSpeed-1.1f)<.001,"F4 in the tutorial visibly increases reading speed");
                    Keys(Key.F3);Keys();check(GameSettings.NoteSpeed==1&&times.SequenceEqual(ChartManager.Instance.SelectedChart.notes.Select(n=>n.timestamp)),"F3 reverses the example without moving any musical timestamp");
                    Frame(2.5,ChartManager.Instance.SelectedChart,false);am.PauseSong();capture("tutorial-speed-practice.png");
                    guide.SkipLesson();check(!guide.IsTraining&&gm.CurrentState==GameState.Playing&&ChartManager.Instance.SelectedChart.notes.Count==201,"Finishing or skipping the final lesson starts full Sara once");
                    Adware(check);gm.SetState(GameState.Paused);MalwareEffectManager.Instance.InfectionVisuals.Render(.9f,true);step++;return false;
                case 9:
                    capture("adware-central-gameplay-1920.png");size(1280,720);step++;return false;
                case 10:
                    MalwareEffectManager.Instance.InfectionVisuals.Render(0,true);capture("adware-central-gameplay-1280.png");size(1920,1080);gm.SetState(GameState.Playing);
                    var data=ChartData.GetDefaultMalwareCatalog()["Adware"];var go=new GameObject("AdwareCleanupFixture");go.AddComponent<SpriteRenderer>();var note=go.AddComponent<NoteController>();note.Initialize(new NoteData{timestamp=am.SongTime,laneIndex=0,malwareData=data},GameSettings.NoteTravelSeconds);note.OnHit(HitRating.Perfect);
                    check(!MalwareEffectManager.Instance.Infections.Active(InfectionKind.Adware),"Hitting a second Adware clears the intrusive ads and healing block");
                    gm.ResetStats();MalwareEffectManager.Instance.TriggerAdwarePopups();for(int i=0;i<20;i++)gm.RegisterHit(HitRating.Perfect,0);
                    check(!MalwareEffectManager.Instance.Infections.Active(InfectionKind.Adware),"OVERCLOCK also purges intrusive Adware");
                    Stop();Restore();return true;
            }
            return false;
        }
        static void Timing(Action<bool,string> check)
        {
            var window=TimingWindow.ForChart(null);
            check(window.Evaluate(.060)==HitRating.Perfect&&window.Evaluate(-.060)==HitRating.Perfect,"60 ms early or late now earns Perfect");
            check(window.Evaluate(.100)==HitRating.Good&&window.Evaluate(.130)==HitRating.Miss,"Good and Miss boundaries stay ordered");
            check(window.CanHit(.159)&&!window.CanHit(.161),"Late miss uses the same relaxed 160 ms base window");
            check(Math.Abs(TimingWindow.ForChart(CampaignCatalog.CreateChart("Sara")).perfectWindowSec-.08775)<.0001,"Sara retains its extra tutorial timing allowance");
        }
        static void ValidateSource(Action<bool,string> check)
        {
            string root=Directory.GetParent(Application.dataPath).FullName;
            var objects=File.ReadAllText(Path.Combine(root,"docs/chart-sources/Dragon-LeiN-5K-HYPER.osu")).Split(new[]{"[HitObjects]"},StringSplitOptions.None)[1].Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Select(l=>l.Split(',')).OrderBy(v=>int.Parse(v[2])).ThenBy(v=>int.Parse(v[0])).ToArray();
            int[] map={0,1,4,2,3};check(objects.Length==898&&boss.notes.Count==898,"Boss imports every original 5K HYPER object");
            var ordered=boss.notes.OrderBy(n=>n.timestamp).ThenBy(n=>Array.IndexOf(LaneLayout.VisualOrder,n.laneIndex)).ToArray();
            for(int i=0;i<objects.Length;i++)check(Math.Abs(ordered[i].timestamp-int.Parse(objects[i][2])/1000.0)<1e-9&&ordered[i].laneIndex==map[Math.Min(4,int.Parse(objects[i][0])*5/512)]&&!ordered[i].isHoldNote,"Dragon original object "+i+": exact timestamp and column");
            check(boss.notes.Count(n=>n.malwareData!=null)==24&&boss.notes.Where(n=>n.malwareData!=null).GroupBy(n=>n.malwareData.GetType()).All(g=>g.Count()==8),"Three boss phases contain eight Spyware, eight Adware and eight Trojans");
            check(boss.notes.GroupBy(n=>n.timestamp).Max(g=>g.Count())==3&&boss.bpm==165,"Original chords up to three keys and 165 BPM are preserved");
        }
        static void Adware(Action<bool,string> check)
        {
            var gm=GameManager.Instance;gm.ResetStats();gm.TakeDamage(20);MalwareEffectManager.Instance.TriggerAdwarePopups();float hp=gm.CurrentHealth;gm.Heal(10);
            check(gm.CurrentHealth==hp,"Adware still blocks real firewall repair");
            check(MalwareInfectionPresentation.AdwareAreas.Length==3&&MalwareInfectionPresentation.AdwareAreas.All(r=>r.yMax<835&&r.x<1540&&r.xMax>380),"Three intrusive ad windows intersect the approach but spare the judgment row");
        }
        static void Keys(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();keyboard.MakeCurrent();typeof(InputManager).GetMethod("Update",Hidden).Invoke(InputManager.Instance,null);}
        static void Frame(double time,ChartData chart,bool press=true)
        {
            paused.SetValue(AudioManager.Instance,false);origin.SetValue(AudioManager.Instance,AudioSettings.dspTime-(time-GameSettings.AudioOffsetSec));
            int priorMiss=GameManager.Instance.MissHits;
            Keys(press?chart.notes.Where(n=>time>=n.timestamp-.004&&time<=n.endTimestamp+.025).Select(n=>InputManager.Instance.GetKeyForLane(n.laneIndex)).Distinct().ToArray():new Key[0]);
            typeof(NoteSpawner).GetMethod("Update",Hidden).Invoke(NoteSpawner.Instance,null);
            if(GameManager.Instance.MissHits>priorMiss)Debug.Log("[Boss timing diagnostic] t="+time+" capture="+InputManager.Instance.CapturingBinding+" held="+string.Join(",",Enumerable.Range(0,5).Select(i=>i+":"+InputManager.Instance.IsLaneHeld(i)))+" pending="+string.Join(",",((List<NoteController>)active.GetValue(NoteSpawner.Instance)).Where(n=>n!=null&&!n.IsHit).Select(n=>n.LaneIndex+"@"+n.TargetTimestamp+" miss="+n.IsMissed)));
            foreach(var n in ((List<NoteController>)active.GetValue(NoteSpawner.Instance)).ToArray())if(n!=null&&(n.IsHit||n.IsMissed))n.gameObject.SetActive(false);
        }
        static void Prepare(ChartData chart){GameplayUI.IsTestPlayFromEditor=true;ChartManager.Instance.SelectChart(chart);NoteSpawner.Instance.LoadChart(chart);AudioManager.Instance.PlaySong(chart.audioClip,chart.PlaybackLeadIn);GameManager.Instance.StartGame();AudioManager.Instance.PauseSong();}
        static void Stop(){AudioManager.Instance.StopSong();NoteSpawner.Instance.StopAndClear();GameManager.Instance.SetState(GameState.MainMenu);}
    }
}
#endif
