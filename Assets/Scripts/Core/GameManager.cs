using System;
using System.Collections;
using RhythmWare.Education;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmWare.Core
{
    public enum GameState
    {
        MainMenu,
        LevelSelect,
        Settings,
        ExtraContent,
        Learning,
        TutorialInstruction,
        Editor,
        Playing,
        Paused,
        RescueQuestion,
        Reviving,
        GameOver,
        Victory
    }

    public enum HitRating
    {
        Perfect,
        Good,
        Miss
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState CurrentState { get; private set; } = GameState.MainMenu;

        [Header("Gameplay Stats")]
        [SerializeField] private float maxHealth = 100f;
        public float CurrentHealth { get; private set; }
        public bool TrainingSafety { get; set; }
        public int CurrentScore { get; private set; }
        public int CurrentCombo { get; private set; }
        public int MaxCombo { get; private set; }

        public int PerfectHits { get; private set; }
        public int GoodHits { get; private set; }
        public int MissHits { get; private set; }

        // Events
        public event Action<GameState> OnStateChanged;
        public event Action<float, float> OnHealthChanged; // current, max
        public event Action<int, int> OnScoreComboChanged; // score, combo
        public event Action<HitRating, int> OnNoteHit;     // rating, laneIndex
        public event Action OnGameOver;
        public event Action OnVictory;
        public event Action On2FARescueTriggered;
        public event Action<int, bool, bool> OnOverclockChanged;
        public event Action<int> OnRescueCountdownChanged;
        public event Action OnRescueAnswered;

        // 2FA Emergency Rescue Protocol
        public bool HasUsed2FARescue { get; private set; } = false;
        public static bool Enable2FARescue { get; set; } = true;
        public const int OverclockMaximum = 20;
        public int OverclockCharge { get; private set; }
        public bool IsOverclockActive => OverclockCharge >= OverclockMaximum;
        public bool HasAchievedOverclock { get; private set; }
        public CyberQuestion RescueQuestion { get; private set; }
        public bool RescueAnswerLocked { get; private set; }
        public int RescueSelectedOption { get; private set; } = -1;
        public int RescueCountdown { get; private set; }
        Coroutine rescueRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SetState(GameState newState)
        {
            if(newState!=GameState.RescueQuestion && newState!=GameState.Reviving)CancelRescueTimer();
            CurrentState = newState;
            OnStateChanged?.Invoke(newState);

            if (newState == GameState.Paused || newState == GameState.RescueQuestion || newState == GameState.Reviving || newState==GameState.TutorialInstruction)
            {
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = 1f;
            }
        }

        public void ResetStats()
        {
            CancelRescueTimer();
            RhythmWare.Malware.MalwareEffectManager.Instance?.ResetInfections();
            CurrentHealth = maxHealth;
            CurrentScore = 0;
            CurrentCombo = 0;
            MaxCombo = 0;
            PerfectHits = 0;
            GoodHits = 0;
            MissHits = 0;
            HasUsed2FARescue = false;
            HasAchievedOverclock = false;OverclockCharge = 0;
            RescueQuestion = null;RescueAnswerLocked = false;RescueSelectedOption = -1;RescueCountdown = 0;
            OnOverclockChanged?.Invoke(0,false,false);

            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
            OnScoreComboChanged?.Invoke(CurrentScore, CurrentCombo);
        }

        public void StartGame()
        {
            AttemptRecorder.Instance?.Finish("abandoned");
            ResetStats();
            SetState(GameState.Playing);
            AttemptRecorder.Instance?.BeginCurrent();
        }

        public void RegisterHit(HitRating rating, int laneIndex, float damageMultiplier = 1.0f)
        {
            if (CurrentState != GameState.Playing) return;

            switch (rating)
            {
                case HitRating.Perfect:
                    OverclockCharge = Mathf.Min(OverclockMaximum,OverclockCharge+1);
                    if(IsOverclockActive)HasAchievedOverclock = true;
                    OnOverclockChanged?.Invoke(OverclockCharge,IsOverclockActive,false);
                    PerfectHits++;
                    CurrentCombo++;
                    CurrentScore += 1000 + (CurrentCombo * 10);
                    Heal(2f);
                    break;

                case HitRating.Good:
                    GoodHits++;
                    CurrentCombo++;
                    CurrentScore += 500 + (CurrentCombo * 5);
                    Heal(1f);
                    break;

                case HitRating.Miss:
                    bool discharged = IsOverclockActive;
                    OverclockCharge = 0;
                    OnOverclockChanged?.Invoke(0,false,discharged);
                    MissHits++;
                    CurrentCombo = 0;
                    float vulnerability=RhythmWare.Malware.MalwareEffectManager.Instance?.Infections.MissMultiplier??1f;
                    float extraDamage=Mathf.Max(0,Mathf.Min(15f*damageMultiplier*(vulnerability-1),CurrentHealth-15f*damageMultiplier));
                    RhythmWare.Malware.MalwareEffectManager.Instance?.InfectionVisuals?.RecordDamage(RhythmWare.Malware.InfectionKind.Trojan,extraDamage);
                    TakeDamage(15f * damageMultiplier * vulnerability);
                    break;
            }

            if (CurrentCombo > MaxCombo)
            {
                MaxCombo = CurrentCombo;
            }

            OnNoteHit?.Invoke(rating, laneIndex);
            OnScoreComboChanged?.Invoke(CurrentScore, CurrentCombo);
        }

        public void AddScore(int points)
        {
            if (CurrentState != GameState.Playing) return;
            CurrentScore += points;
            OnScoreComboChanged?.Invoke(CurrentScore, CurrentCombo);
        }

        public void TakeDamage(float amount)
        {
            if(CurrentState!=GameState.Playing)return;
            CurrentHealth = Mathf.Max(TrainingSafety?40f:0f, CurrentHealth - amount);
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

            if (CurrentHealth <= 0f && CurrentState == GameState.Playing)
            {
                if (Enable2FARescue && HasAchievedOverclock && !HasUsed2FARescue && On2FARescueTriggered != null)
                {
                    RescueQuestion = CyberQuestionBank.GetRandomQuestionForThreat(SocLearningProgress.LastThreat);
                    if(RescueQuestion==null){TriggerGameOver();return;}
                    HasUsed2FARescue = true;
                    RescueAnswerLocked = false;RescueSelectedOption = -1;
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PauseSong();
                    }
                    SetState(GameState.RescueQuestion);
                    On2FARescueTriggered.Invoke();
                    return;
                }

                TriggerGameOver();
            }
        }

        public bool AnswerRescue(int optionIndex)
        {
            if(CurrentState!=GameState.RescueQuestion || RescueAnswerLocked || RescueQuestion==null || optionIndex<0 || optionIndex>=RescueQuestion.options.Length)return false;
            RescueAnswerLocked = true;RescueSelectedOption = optionIndex;
            SocLearningProgress.Record(RescueQuestion,optionIndex,RhythmWare.UI.TutorialGuide.Instance?.IsTraining==true?"tutorial-rescue":"rescue");
            bool correct = optionIndex==RescueQuestion.correctIndex;
            if(correct)
            {
                CurrentHealth = maxHealth*.30f;
                OnHealthChanged?.Invoke(CurrentHealth,maxHealth);
                SetState(GameState.Reviving);
            }
            OnRescueAnswered?.Invoke();
            rescueRoutine = StartCoroutine(correct?ReviveCountdownRoutine():FailedRescueRoutine());
            return true;
        }

        IEnumerator ReviveCountdownRoutine()
        {
            for(int seconds=3;seconds>0;seconds--)
            {
                // Returning from another window always gives the player the
                // complete preparation time before a rescue resumes the song.
                if(!Application.isFocused&&!Application.isBatchMode)
                {seconds=4;yield return new WaitForSecondsRealtime(.1f);continue;}
                RescueCountdown = seconds;OnRescueCountdownChanged?.Invoke(seconds);
                yield return new WaitForSecondsRealtime(1);
                if(CurrentState!=GameState.Reviving)yield break;
                if(!Application.isFocused&&!Application.isBatchMode)seconds=4;
            }
            rescueRoutine = null;RescueCountdown = 0;
            AudioManager.Instance?.ResumeSong();
            SetState(GameState.Playing);
        }

        IEnumerator FailedRescueRoutine()
        {
            yield return new WaitForSecondsRealtime(2.5f);
            rescueRoutine = null;
            if(CurrentState==GameState.RescueQuestion)TriggerGameOver();
        }

        public void ForfeitRescue()
        {
            if(CurrentState!=GameState.RescueQuestion && CurrentState!=GameState.Reviving)return;
            RescueAnswerLocked = true;TriggerGameOver();
        }

        void CancelRescueTimer()
        {
            if(rescueRoutine!=null)StopCoroutine(rescueRoutine);
            rescueRoutine = null;RescueCountdown = 0;
        }

        public void Heal(float amount)
        {
            if(CurrentState!=GameState.Playing)return;
            amount*=RhythmWare.Malware.MalwareEffectManager.Instance?.Infections.HealingMultiplier??1f;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        public void TriggerGameOver()
        {
            SetState(GameState.GameOver);
            if (AudioManager.Instance != null)
                AudioManager.Instance.StopSong();

            OnGameOver?.Invoke();
        }

        public void TriggerVictory()
        {
            SetState(GameState.Victory);
            OnVictory?.Invoke();
        }

        void OnApplicationFocus(bool focused){if(!focused&&!Application.isBatchMode&&!DistributionSelfTest.IsRunning)PauseForFocusLoss();}
        void OnApplicationPause(bool paused){if(paused&&!Application.isBatchMode&&!DistributionSelfTest.IsRunning)PauseForFocusLoss();}
        public void PauseForFocusLoss()
        {
            if(CurrentState!=GameState.Playing)return;
            // Freeze the transport before creating a first-use pause panel.
            AudioManager.Instance?.PauseSong();
            var ui=UnityEngine.Object.FindAnyObjectByType<RhythmWare.UI.GameplayUI>();
            if(ui!=null)ui.TogglePause();else {AudioManager.Instance?.PauseSong();SetState(GameState.Paused);}
            InputManager.Instance?.FlushHeld();
        }
        public void LoadScene(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}
