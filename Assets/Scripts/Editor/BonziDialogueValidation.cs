#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using RhythmWare.Boss;
using RhythmWare.Core;
using RhythmWare.Rhythm;
using RhythmWare.UI;

namespace RhythmWare.EditorTools
{
    public static class BonziDialogueValidation
    {
        static int step;
        static ChartData boss;
        static string frozenText;
        static int frozenCharacters;
        static double frozenTime;
        static bool reduced;
        static TextMeshProUGUI Body()=>GameObject.Find("BonziSpeechBubble").GetComponentsInChildren<TextMeshProUGUI>().Last();
        public static bool Run(Action<bool,string> check,Action<string> capture,Action<int,int> size)
        {
            var gm=GameManager.Instance;var audio=AudioManager.Instance;var actor=BonziStageController.Instance;
            switch(step)
            {
                case 0:
                    ValidatePolicy(check);reduced=VisualPreferences.ReducedEffects;
                    MainMenuUI.Instance.OpenEditor();boss=BonziEncounter.CreateChart();ChartEditorManager.Instance.OpenCatalogChart(boss);step++;return false;
                case 1:
                    if(ChartEditorManager.Instance.ActiveChart.audioClip==null)return false;
                    boss.audioClip=ChartEditorManager.Instance.ActiveChart.audioClip;
                    StartBoss(0);step++;return false;
                case 2:
                    check(actor.IsSpeaking&&actor.DialogueText.Contains("permiso"),"Bonzi greets the player in a live encounter");
                    var body=Body();body.ForceMeshUpdate();check(!body.isTextTruncated,"Speech fits at 1920x1080");
                    check(!body.raycastTarget&&!body.GetComponentInParent<CanvasGroup>().blocksRaycasts,"Speech cannot intercept gameplay input");
                    capture("bonzi-dialogue-intro-1920.png");
                    audio.PauseSong();frozenTime=audio.SongTime;actor.RefreshPresentation(frozenTime);
                    frozenText=body.text;frozenCharacters=body.maxVisibleCharacters;gm.SetState(GameState.Paused);step++;return false;
                case 3:
                    check(!actor.IsSpeaking&&audio.SongTime==frozenTime,"Pause hides speech and freezes the song clock");
                    gm.SetState(GameState.Playing);check(actor.IsSpeaking&&Body().text==frozenText&&Body().maxVisibleCharacters==frozenCharacters,"Resume retains the same line and typewriter position");
                    gm.SetState(GameState.RescueQuestion);check(!actor.IsSpeaking,"SOC question hides Bonzi speech");
                    gm.SetState(GameState.Reviving);check(!actor.IsSpeaking,"Revive countdown hides Bonzi speech");
                    gm.SetState(GameState.Playing);audio.ResumeSong();
                    size(1280,720);step++;return false;
                case 4:
                    check(actor.IsSpeaking,"Dialogue survives pause and rescue lifecycle");Body().ForceMeshUpdate();check(!Body().isTextTruncated,"Speech fits at 1280x720");
                    capture("bonzi-dialogue-1280.png");size(1920,1080);StartBoss(boss.audioClip.length*.4);step++;return false;
                case 5:
                    check(actor.DialogueText.Contains("anuncios"),"Mid-song starts announce Bonzi's Adware phase");capture("bonzi-dialogue-adware-1920.png");
                    StartBoss(boss.audioClip.length*.72);step++;return false;
                case 6:
                    check(actor.DialogueText.Contains("troyanos"),"Final phase announces Trojan escalation");capture("bonzi-dialogue-trojan-1920.png");
                    VisualPreferences.ReducedEffects=true;actor.RefreshPresentation(audio.SongTime);
                    check(Body().maxVisibleCharacters==int.MaxValue&&Body().GetComponentInParent<CanvasGroup>().alpha==1,"Reduced effects displays complete steady dialogue");
                    StartBoss(0);for(int i=0;i<20;i++)gm.RegisterHit(HitRating.Perfect,i%7);
                    // Empty fixture chart has a one-hit core, so containment must win over all queued chatter.
                    actor.RefreshPresentation(audio.SongTime);check(actor.DialogueText.Contains("condiciones"),"Containment dialogue preempts phase and reaction chatter");
                    capture("bonzi-dialogue-contained-1920.png");gm.SetState(GameState.GameOver);check(!actor.IsSpeaking,"Results hide speech");
                    StartBoss(0);check(actor.DialogueText.Contains("permiso"),"Retry resets dialogue and greets again");
                    var ordinary=CampaignCatalog.CreateChart("Sara");NoteSpawner.Instance.LoadChart(ordinary);
                    check(!actor.IsEncounter&&!actor.IsSpeaking,"Normal songs clear encounter speech");
                    VisualPreferences.ReducedEffects=reduced;UnityEngine.Object.FindAnyObjectByType<GameplayUI>().ExitToMainMenu();GameplayUI.IsTestPlayFromEditor=false;
                    return true;
            }
            return false;
        }
        static void StartBoss(double at)
        {
            var chart=UnityEngine.Object.Instantiate(boss);chart.notes=new System.Collections.Generic.List<NoteData>();
            GameplayUI.IsTestPlayFromEditor=true;ChartManager.Instance.SelectChart(chart);NoteSpawner.Instance.LoadChart(chart);
            AudioManager.Instance.PlaySong(boss.audioClip,1,at);GameManager.Instance.StartGame();
        }
        static void ValidatePolicy(Action<bool,string> check)
        {
            var d=new BonziDialogue();d.Refresh(0,1,1);
            check(d.VisibleAt(0)&&d.Text.Contains("permiso"),"Dialogue policy opens with false-assistant greeting");
            check(!d.Attack("Spyware",1)&&!d.Miss(1),"Routine events cannot interrupt an important line");
            check(!d.VisibleAt(5.5)&&!d.Attack("Spyware",6),"Speech expires but cooldown prevents immediate chatter");
            check(d.Attack("Spyware",10)&&d.Text.Contains("espionaje"),"Spyware launch receives contextual dialogue");
            check(!d.Attack("Adware",11)&&!d.Miss(11),"Repeated notes and misses do not spam speech");
            d.Refresh(11,2,1);check(d.Text.Contains("anuncios"),"Phase changes preempt routine attack speech");
            d.Refresh(12,3,1);check(d.Text.Contains("troyanos"),"Seeking to a new phase replaces an earlier phase line");
            d.Overclock();d.Refresh(13,3,1);check(d.Text.Contains("troyanos"),"Overclock reaction waits until the phase announcement finishes");
            d.Refresh(18,3,1);check(d.Text.Contains("OVERCLOCK"),"Queued Overclock reaction appears when readable");
            double start=d.StartedAt;d.Overclock();d.Refresh(24,3,1);check(d.StartedAt==start,"Overclock comment plays once per attempt");
            d.Refresh(25,3,.6f);check(d.Text.Contains("núcleo"),"Boss reacts when core integrity crosses two thirds");
            d.Refresh(31,3,.3f);check(d.Text.Contains("desinstales"),"Boss bargains when its core is critical");
            d.Refresh(31.1,3,0);check(d.Text.Contains("condiciones")&&!d.Attack("Trojan",50)&&!d.Miss(50),"Defeat preempts all lines and silences further taunts");
            d.Reset();d.Refresh(0,1,1);check(d.Text.Contains("permiso"),"Policy reset clears timers and once-only flags");
            d.Revived();d.Refresh(6,1,1);check(d.Text.Contains("respuesta"),"Revival queues a contextual SOC reaction");
            d.Reset();d.Refresh(0,1,1);check(d.Attack("Adware",10)&&d.Text.Contains("ventanas"),"Adware has its own launch dialogue");
            check(d.Attack("Trojan",20)&&d.Text.Contains("sorpresa"),"Trojan launch rotates contextual variants");
            check(!d.Attack("Unknown",30)&&d.Miss(30),"Unknown malware is silent while a spaced miss can trigger a taunt");
        }
    }
}
#endif
