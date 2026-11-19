#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RhythmWare.Core;
using RhythmWare.Rhythm;
using RhythmWare.UI;

namespace RhythmWare.EditorTools
{
    public static class CampaignValidation
    {
        static int step;
        static ChartData tutorial;
        static double frozen;
        public static bool Run(Action<bool,string> check,Action<string> capture,Action<int,int> size)
        {
            var menu=MainMenuUI.Instance;var gm=GameManager.Instance;var audio=AudioManager.Instance;
            var selector=UnityEngine.Object.FindAnyObjectByType<LevelSelectUI>(FindObjectsInactive.Include);
            var ui=UnityEngine.Object.FindAnyObjectByType<GameplayUI>(FindObjectsInactive.Include);
            switch(step)
            {
                case 0:
                    var entries=selector.RefreshCatalog().Where(e=>string.IsNullOrEmpty(e.chartFilePath)).ToArray();
                    check(entries.Length==3,"Official campaign contains exactly three stages");
                    check(entries[0].chartData.tutorialId=="sara-basics"&&entries[1].songTitle=="Jack-the-Ripper"&&entries[2].audioClipPath.EndsWith("femtanyl - DINNER!.mp3"),"Campaign uses Sara tutorial, Jack intermediate and DINNER Bonzi");
                    foreach(var entry in entries)ValidateChart(entry.chartData,check);
                    check(!menu.GetComponentsInChildren<TextMeshProUGUI>(true).Any(t=>t.text=="RHYTHMWARE  /  CYBER CANDY POP"),"Main menu no longer contains the top palette label");
                    tutorial=entries[0].chartData;StartTutorial(0);step++;return false;
                case 1:
                    var guide=UnityEngine.Object.FindAnyObjectByType<TutorialGuide>();
                    check(GameObject.Find("SaraTutorialGuide")!=null&&guide.CurrentLesson==0,"Sara displays its live tutorial before the first note");capture("tutorial-intro-1920.png");
                    StartTutorial(66);step++;return false;
                case 2:
                    check(UnityEngine.Object.FindAnyObjectByType<TutorialGuide>().CurrentLesson==8,"Tutorial advances to hold instructions using the song clock");capture("tutorial-holds-1920.png");
                    ui.TogglePause();frozen=audio.SongTime;step++;return false;
                case 3:
                    check(gm.CurrentState==GameState.Paused&&audio.SongTime==frozen,"Candy pause retains frozen gameplay transport");
                    check(GameObject.Find("SaraTutorialGuide")==null,"Tutorial guide stays behind pause and rescue overlays");capture("pause-candy-1920.png");
                    ui.ResumeGame();gm.TriggerGameOver();step++;return false;
                case 4:
                    capture("gameover-candy-1920.png");ui.ExitToMainMenu();menu.OpenSettings();step++;return false;
                case 5:
                    check(UnityEngine.Object.FindAnyObjectByType<SettingsUI>().GetComponentsInChildren<Slider>().Length==4,"Candy settings retain master/music/SFX/dim sliders");capture("settings-candy-1920.png");size(1280,720);step++;return false;
                case 6:capture("settings-candy-1280.png");menu.OpenLevelSelect();step++;return false;
                case 7:
                    capture("campaign-select-1280.png");size(1920,1080);menu.BackToMainMenu();step++;return false;
                case 8:
                    menu.OpenSettings();
                    if(!VisualPreferences.ReducedEffects)
                    {
                        check(CandyMenuTransition.IsRunning,"Opening a menu starts the diagonal input-blocking transition");
                        var transition=UnityEngine.Object.FindAnyObjectByType<CandyMenuTransition>();
                        check(!transition.GetComponent<CanvasGroup>().interactable,"Menu transition blocks selectable mouse and keyboard submission");
                        typeof(CandyMenuTransition).GetField("age",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(transition,.24f);
                        transition.SendMessage("Draw");capture("menu-transition-midpoint.png");
                    }
                    step++;return false;
                case 9:
                    check(!CandyMenuTransition.IsRunning,"Menu transition completes and releases interaction");
                    check(menu.GetComponent<CanvasGroup>()==null||menu.GetComponent<CanvasGroup>().interactable,"Menu selectables regain interaction after transition");
                    GameplayUI.IsTestPlayFromEditor=false;menu.BackToMainMenu();return true;
            }
            return false;
        }
        static void ValidateChart(ChartData chart,Action<bool,string> check)
        {
            double duration=chart.tutorialId!=""?154.9845:chart.bossId!=""?136.5159:160.2351;
            check(chart.notes.Count>80&&chart.notes.Max(n=>n.endTimestamp)>duration*.95&&chart.notes.Max(n=>n.endTimestamp)<duration-1,"Full song coverage: "+chart.songTitle);
            check(chart.notes.All(n=>n.timestamp>=0&&n.laneIndex>=0&&n.laneIndex<7&&n.duration>=0)&&chart.notes.SequenceEqual(chart.notes.OrderBy(n=>n.timestamp)),"Valid sorted chart notes: "+chart.songTitle);
            foreach(var lane in chart.notes.GroupBy(n=>n.laneIndex))
            {
                var ordered=lane.OrderBy(n=>n.timestamp).ToArray();for(int i=1;i<ordered.Length;i++)if(ordered[i].timestamp<ordered[i-1].endTimestamp+.15)throw new Exception("Overlapping same-lane note/hold: "+chart.songTitle);
            }
            check(chart.notes.GroupBy(n=>Math.Round(n.timestamp,3)).All(g=>g.Count()<=2),"At most two simultaneous notes and no same-lane hold overlap: "+chart.songTitle);
            if(chart.tutorialId!="")check(chart.notes.Where(n=>n.timestamp<64).All(n=>n.duration==0&&n.malwareData==null)&&chart.notes.Count(n=>n.duration>.05)==3,"Sara introduces taps before holds and malware");
            if(chart.bossId!="")check(chart.notes.Where(n=>n.malwareData!=null).Select(n=>n.malwareData.GetType()).Distinct().Count()==3,"Bonzi uses its three principal malware families");
            var copy=ChartData.FromJson(chart.ToJson());check(copy.bossId==chart.bossId&&copy.tutorialId==chart.tutorialId&&copy.notes.Count==chart.notes.Count,"Campaign metadata survives JSON round trip: "+chart.songTitle);UnityEngine.Object.Destroy(copy);
        }
        static void StartTutorial(double at)
        {
            var copy=UnityEngine.Object.Instantiate(tutorial);copy.notes=tutorial.notes.Where(n=>n.timestamp>=at).ToList();
            GameplayUI.IsTestPlayFromEditor=true;ChartManager.Instance.SelectChart(copy);NoteSpawner.Instance.LoadChart(copy);
            AudioManager.Instance.PlaySong(tutorial.audioClip,1,at);GameManager.Instance.StartGame();
        }
    }
}
#endif
