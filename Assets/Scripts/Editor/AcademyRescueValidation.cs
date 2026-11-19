#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using RhythmWare.Core;
using RhythmWare.Education;
using RhythmWare.Rhythm;
using RhythmWare.UI;

namespace RhythmWare.EditorTools
{
    // Real scene checks, including asynchronous countdown and paused audio.
    public static class AcademyRescueValidation
    {
        static int step;
        static double frozenTime,reviveStarted,resumedTime;
        static Vector3 notePosition;
        static NoteController frozenNote;
        static ChartData fixture;
        static Action<int> countdownListener;
        static Action<GameState> stateListener;
        static readonly List<int> secondsSeen=new List<int>();
        static bool offsetSaved,hadOffset;
        static float previousOffset;
        public static double PollDelay => step==6?.15:step==9?3.4:2;
        public static void Restore()
        {
            if(offsetSaved){GameSettings.AudioOffsetSec=previousOffset;if(!hadOffset){PlayerPrefs.DeleteKey("AudioOffsetSec");PlayerPrefs.Save();}offsetSaved=false;}
            var gm=GameManager.Instance;
            if(gm!=null){gm.OnRescueCountdownChanged-=countdownListener;gm.OnStateChanged-=stateListener;}
            if(fixture!=null)UnityEngine.Object.Destroy(fixture);
        }
        public static void Academy(Action<bool,string> check)
        {
            var hub=UnityEngine.Object.FindAnyObjectByType<ExtraContentUI>();var academy=hub.Academy;
            check(hub.CurrentTab==1 && academy.VisibleCount==18,"Academy opens practice with all 18 existing cases");
            var q=academy.CurrentQuestion;academy.QuestionCard.Options[(q.correctIndex+1)%q.options.Length].onClick.Invoke();
            check(academy.AnsweredCount==1 && academy.CorrectCount==0,"Practice records incorrect answer and explanation");
            academy.QuestionCard.Options[q.correctIndex].onClick.Invoke();
            check(academy.CorrectCount==0,"Practice locks duplicate answers");
            academy.NextQuestion();academy.PrevQuestion();
            check(academy.AnsweredCount==1 && !academy.QuestionCard.Options[0].interactable,"Returning to a case preserves its answer");
            academy.RestartQuiz();academy.QuestionCard.Options[academy.CurrentQuestion.correctIndex].onClick.Invoke();
            check(academy.CorrectCount==1,"Practice restart and correct button response");
            academy.SelectModule("Phishing");check(academy.VisibleCount==3 && academy.CorrectCount==1,"Module filter retains global progress");
            academy.SelectModule("Todos");
            for(int i=0;i<18;i++)
            {
                academy.Answer(academy.CurrentQuestion.correctIndex);Canvas.ForceUpdateCanvases();
                foreach(var text in academy.GetComponentsInChildren<TMPro.TextMeshProUGUI>())text.ForceMeshUpdate();
                academy.NextQuestion();
            }
            check(academy.CorrectCount==18 && academy.AnsweredCount==18,"Every existing case can be completed once");
            academy.RestartQuiz();
            check(hub.MalwareCount==9,"All nine malware dossiers retained");
        }
        static void EarnOverclock()
        { for(int i=0;i<GameManager.OverclockMaximum;i++)GameManager.Instance.RegisterHit(HitRating.Perfect,0); }
        static void FreshAttempt()
        {
            var ui=UnityEngine.Object.FindAnyObjectByType<GameplayUI>();ui.RestartGame();NoteSpawner.Instance.StopAndClear();
        }
        public static bool Rescue(Action<bool,string> check,Action<string> capture,Action<int,int> resize)
        {
            var gm=GameManager.Instance;var audio=AudioManager.Instance;var ui=UnityEngine.Object.FindAnyObjectByType<GameplayUI>();
            switch(step)
            {
                case 0:
                    hadOffset=PlayerPrefs.HasKey("AudioOffsetSec");previousOffset=GameSettings.AudioOffsetSec;offsetSaved=true;GameSettings.AudioOffsetSec=.125f;
                    gm.StartGame();gm.TakeDamage(100);
                    check(gm.CurrentState==GameState.GameOver && !gm.HasUsed2FARescue,"Death without OVERCLOCK offers no rescue");
                    FreshAttempt();step++;return false;
                case 1:
                    if(audio.SongTime<.5)return false; // Wait for the scheduled song, not just wall time.
                    for(int i=0;i<19;i++)gm.RegisterHit(HitRating.Perfect,0);
                    check(!gm.HasAchievedOverclock && gm.OverclockCharge==19,"Nineteen Perfect hits do not unlock rescue");
                    gm.RegisterHit(HitRating.Good,0);check(gm.OverclockCharge==19,"Good judgement preserves existing charge rule");
                    gm.RegisterHit(HitRating.Perfect,0);check(gm.HasAchievedOverclock && gm.IsOverclockActive,"Twentieth Perfect unlocks OVERCLOCK and rescue");
                    gm.RegisterHit(HitRating.Miss,0);check(gm.HasAchievedOverclock && gm.OverclockCharge==0,"Rescue eligibility survives OVERCLOCK discharge");
                    fixture=ScriptableObject.CreateInstance<ChartData>();fixture.notes.Add(new NoteData{timestamp=audio.SongTime+1,laneIndex=3,speedMultiplier=1});fixture.notes.Add(new NoteData{timestamp=audio.SongTime+12,laneIndex=5,speedMultiplier=1});
                    NoteSpawner.Instance.LoadChart(fixture);
                    // Drive the real spawner within this editor callback so a
                    // slow batch frame cannot expire the note before pausing.
                    NoteSpawner.Instance.SendMessage("Update",SendMessageOptions.RequireReceiver);
                    var notes=(List<NoteController>)typeof(NoteSpawner).GetField("activeNotes",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(NoteSpawner.Instance);
                    check(notes.Count>0,"Real spawned note available for freeze/resume test");frozenNote=notes[0];notePosition=frozenNote.transform.position;
                    gm.TakeDamage(100);frozenTime=audio.SongTime;
                    step++;return false;
                case 2:
                    check(gm.CurrentState==GameState.RescueQuestion && gm.HasUsed2FARescue && Time.timeScale==0 && !audio.IsPlaying,"Eligible death pauses audio and gameplay for one SOC question");
                    check(!gm.AnswerRescue(-1) && !gm.RescueAnswerLocked,"Invalid rescue answer is rejected without consuming choice");
                    ui.ResumeGame();ui.TogglePause();check(gm.CurrentState==GameState.RescueQuestion,"Pause controls cannot bypass rescue question");
                    capture("rescue-question-1920.png");resize(1280,720);step++;return false;
                case 3:capture("rescue-question-1280.png");resize(1366,768);step++;return false;
                case 4:capture("rescue-question-1366.png");resize(1600,900);step++;return false;
                case 5:
                    capture("rescue-question-1600.png");
                    check(Math.Abs(audio.SongTime-frozenTime)<.001 && frozenNote!=null && !frozenNote.IsMissed && Vector3.Distance(notePosition,frozenNote.transform.position)<.001,"Song clock and spawned note remain frozen throughout question");
                    resize(1920,1080);reviveStarted=Time.realtimeSinceStartupAsDouble;
                    countdownListener=seconds=>{secondsSeen.Add(seconds);capture("rescue-countdown-"+seconds+".png");};gm.OnRescueCountdownChanged+=countdownListener;
                    stateListener=state=>{if(state==GameState.Playing){resumedTime=audio.SongTime;check(Math.Abs(resumedTime-frozenTime)<.03,"Resume DSP retains paused position with nonzero latency offset");}};gm.OnStateChanged+=stateListener;
                    var rescue=UnityEngine.Object.FindAnyObjectByType<SocRescueUI>();rescue.QuestionCard.Options[gm.RescueQuestion.correctIndex].onClick.Invoke();
                    check(gm.CurrentState==GameState.Reviving && Mathf.Abs(gm.CurrentHealth-30)<.001f && gm.RescueCountdown==3,"Correct response restores exactly 30 percent and starts 3-second countdown");
                    check(!gm.AnswerRescue(gm.RescueQuestion.correctIndex),"Repeated rescue answer cannot heal or restart countdown");
                    ui.ResumeGame();ui.TogglePause();check(gm.CurrentState==GameState.Reviving && !audio.IsPlaying,"Pause controls cannot skip countdown");
                    step++;return false;
                case 6:
                    if(gm.CurrentState==GameState.Reviving){check(!audio.IsPlaying && Math.Abs(audio.SongTime-frozenTime)<.001,"Audio remains paused during countdown");return false;}
                    check(gm.CurrentState==GameState.Playing && audio.IsPlaying && Mathf.Abs(gm.CurrentHealth-30)<.001f,"Countdown resumes original song and gameplay");
                    check(Time.realtimeSinceStartupAsDouble-reviveStarted>=3 && secondsSeen.SequenceEqual(new[]{3,2,1}),"Countdown displays 3, 2, 1 over at least three real seconds");
                    gm.OnRescueCountdownChanged-=countdownListener;gm.OnStateChanged-=stateListener;
                    gm.TakeDamage(100);check(gm.CurrentState==GameState.GameOver,"Second death cannot reuse the rescue");
                    FreshAttempt();check(!gm.HasAchievedOverclock && !gm.HasUsed2FARescue && gm.OverclockCharge==0,"Retry resets eligibility, charge and rescue usage");step++;return false;
                case 7:
                    EarnOverclock();gm.TakeDamage(100);
                    var prompt=UnityEngine.Object.FindAnyObjectByType<SocRescueUI>();prompt.QuestionCard.Options[(gm.RescueQuestion.correctIndex+1)%3].onClick.Invoke();
                    check(gm.RescueAnswerLocked && gm.CurrentHealth==0 && !gm.AnswerRescue(gm.RescueQuestion.correctIndex),"Incorrect answer is final and cannot be replaced by keyboard or click");
                    capture("rescue-incorrect-1920.png");step++;return false;
                case 8:
                    if(gm.CurrentState==GameState.RescueQuestion)return false;
                    check(gm.CurrentState==GameState.GameOver,"Incorrect answer leads to defeat after explanation");
                    FreshAttempt();EarnOverclock();gm.TakeDamage(100);gm.ForfeitRescue();check(gm.CurrentState==GameState.GameOver,"Forfeit finishes rescue without reviving");
                    FreshAttempt();EarnOverclock();gm.TakeDamage(100);gm.AnswerRescue(gm.RescueQuestion.correctIndex);FreshAttempt();step++;return false;
                case 9:
                    check(gm.CurrentState==GameState.Playing && gm.CurrentHealth==100 && !gm.HasUsed2FARescue && gm.RescueCountdown==0,"Retry cancels stale revive countdown");
                    Restore();return true;
            }
            return false;
        }
    }
}
#endif
