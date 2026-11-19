using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using RhythmWare.Core;
using RhythmWare.Rhythm;
using RhythmWare.Education;

namespace RhythmWare.UI
{
    public class GameplayUI : MonoBehaviour
    {
        [Header("HUD Elements")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text comboText;
        [SerializeField] private Text accuracyText;
        [SerializeField] private Text hitFeedbackText;

        [Header("Overclock / Firewall Fever (Muse Dash style)")]
        [SerializeField] private Slider overclockSlider;
        [SerializeField] private Text overclockText;
        private int overclockCount => GameManager.Instance?.OverclockCharge??0;
        private const int OVERCLOCK_MAX = GameManager.OverclockMaximum;
        private bool isOverclockActive => GameManager.Instance!=null && GameManager.Instance.IsOverclockActive;

        [Header("Pause Overlay")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button exitToMenuButton;

        [Header("End Game Screens")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private TMP_Text victoryStatsText;
        [SerializeField] private TMP_Text victoryRankText;

        private GameplayHUD modernHUD;
        public void Configure(GameplayHUD hud) { modernHUD=hud; }

        public static bool IsTestPlayFromEditor { get; set; } = false;

        private TMP_Text gameOverStatsText;
        private Button gameOverRetryButton;
        private Button gameOverExitButton;
        private TMP_Text gameOverForensicText;
        private TMP_Text victoryForensicText;
        private Button victoryRetryButton;
        private Button victoryExitButton;

        private SocRescueUI rescueUI;

        private Coroutine feedbackCoroutine;

        private void OnEnable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnHealthChanged += UpdateHealthUI;
                GameManager.Instance.OnScoreComboChanged += UpdateScoreComboUI;
                GameManager.Instance.OnNoteHit += DisplayHitFeedback;
                GameManager.Instance.OnGameOver += ShowGameOverScreen;
                GameManager.Instance.OnVictory += ShowVictoryScreen;
                GameManager.Instance.On2FARescueTriggered += Handle2FARescueTriggered;
                GameManager.Instance.OnOverclockChanged += OverclockChanged;
            }

            UpdateOverclockUI();

            HideAllOverlays();
            if(modernHUD!=null && GameManager.Instance!=null) { modernHUD.Health(GameManager.Instance.CurrentHealth,100);modernHUD.Stats(GameManager.Instance.CurrentScore,GameManager.Instance.CurrentCombo); }
        }

        private void OnDisable()
        {
            scrambleStatsCoroutine=null;feedbackCoroutine=null;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnHealthChanged -= UpdateHealthUI;
                GameManager.Instance.OnScoreComboChanged -= UpdateScoreComboUI;
                GameManager.Instance.OnNoteHit -= DisplayHitFeedback;
                GameManager.Instance.OnGameOver -= ShowGameOverScreen;
                GameManager.Instance.OnVictory -= ShowVictoryScreen;
                GameManager.Instance.On2FARescueTriggered -= Handle2FARescueTriggered;
                GameManager.Instance.OnOverclockChanged -= OverclockChanged;
            }
        }

        private void Start()
        {
            if (resumeButton != null) resumeButton.onClick.AddListener(ResumeGame);
            if (restartButton != null) restartButton.onClick.AddListener(RestartGame);
            if (exitToMenuButton != null) exitToMenuButton.onClick.AddListener(ExitToMainMenu);
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnHealthChanged -= UpdateHealthUI;
                GameManager.Instance.OnScoreComboChanged -= UpdateScoreComboUI;
                GameManager.Instance.OnNoteHit -= DisplayHitFeedback;
                GameManager.Instance.OnGameOver -= ShowGameOverScreen;
                GameManager.Instance.OnVictory -= ShowVictoryScreen;
                GameManager.Instance.On2FARescueTriggered -= Handle2FARescueTriggered;
            }
        }

        private void Update()
        {
            if (SocLearningFlow.Instance?.IsOpen==true || GameManager.Instance.CurrentState==GameState.TutorialInstruction) return;

            if(GameManager.Instance.CurrentState==GameState.RescueQuestion || GameManager.Instance.CurrentState==GameState.Reviving)return;

            if (Keyboard.current?.escapeKey.wasPressedThisFrame==true || Gamepad.current?.selectButton.wasPressedThisFrame==true)
            {
                if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
                {
                    TogglePause();
                }
                else if (GameManager.Instance != null && (GameManager.Instance.CurrentState == GameState.Paused || GameManager.Instance.CurrentState == GameState.GameOver || GameManager.Instance.CurrentState == GameState.Victory))
                {
                    // If paused or game over, pressing ESC exits directly to Editor or Menu!
                    ExitToMainMenu();
                }
            }
            else if (Keyboard.current?.pKey.wasPressedThisFrame==true || Gamepad.current?.startButton.wasPressedThisFrame==true)
            {
                TogglePause();
            }
            else if ((Keyboard.current?.spaceKey.wasPressedThisFrame==true || Gamepad.current?.buttonSouth.wasPressedThisFrame==true) && GameManager.Instance != null)
            {
                if (GameManager.Instance.CurrentState == GameState.Paused)
                {
                    ResumeGame();
                }
                else if (GameManager.Instance.CurrentState == GameState.GameOver || GameManager.Instance.CurrentState == GameState.Victory)
                {
                    RestartGame();
                }
            }
        }

        private void UpdateHealthUI(float currentHealth, float maxHealth)
        {
            if(modernHUD!=null) { modernHUD.Health(currentHealth,maxHealth);return; }
            if (healthSlider == null)
            {
                EnsureHealthSliderExists();
            }

            if (healthSlider != null)
            {
                float ratio = Mathf.Clamp01(currentHealth / maxHealth);
                healthSlider.value = ratio;

                if (healthSlider.fillRect != null)
                {
                    Image fillImg = healthSlider.fillRect.GetComponent<Image>();
                    if (fillImg != null)
                    {
                        if (ratio > 0.5f) fillImg.color = new Color(0.0f, 1.0f, 0.6f, 1.0f); // Neon Green
                        else if (ratio > 0.25f) fillImg.color = new Color(1.0f, 0.8f, 0.0f, 1.0f); // Cyber Yellow
                        else fillImg.color = new Color(1.0f, 0.2f, 0.25f, 1.0f); // Critical Red
                    }
                }
            }
        }

        private void UpdateScoreComboUI(int score, int combo)
        {
            if (scrambleStatsCoroutine != null) return; // Don't overwrite if scrambled by Phishing

            if(modernHUD!=null)modernHUD.Stats(score,combo);
            if (scoreText != null) scoreText.text = $"PUNTOS: {score:N0}";
            if (comboText != null) comboText.text = combo > 0 ? $"COMBO x{combo}" : "";

            UpdateAccuracyUI();
        }

        private Coroutine scrambleStatsCoroutine;

        public void ScrambleHUDStats(float duration)
        {
            if (!gameObject.activeInHierarchy) return;
            if (scrambleStatsCoroutine != null) StopCoroutine(scrambleStatsCoroutine);
            scrambleStatsCoroutine = StartCoroutine(ScrambleStatsRoutine(duration));
        }

        private IEnumerator ScrambleStatsRoutine(float duration)
        {
            float elapsed = 0f;
            string[] hexChars = new string[] { "0xDEAD", "0xBEEF", "???", "ERROR", "0x0000", "CORRUPT", "$#%@" };

            while (elapsed < duration)
            {
                elapsed += 0.12f;
                modernHUD?.Corrupt(hexChars[Random.Range(0,hexChars.Length)]);
                if (scoreText != null) scoreText.text = $"PUNTOS: {hexChars[Random.Range(0, hexChars.Length)]}";
                if (comboText != null) comboText.text = $"COMBO: {hexChars[Random.Range(0, hexChars.Length)]}";
                if (accuracyText != null) accuracyText.text = "PRECISIÓN: ???%";
                yield return new WaitForSeconds(0.12f);
            }

            scrambleStatsCoroutine = null;
            if (GameManager.Instance != null)
            {
                UpdateScoreComboUI(GameManager.Instance.CurrentScore, GameManager.Instance.CurrentCombo);
            }
        }

        private void UpdateAccuracyUI()
        {
            if (accuracyText == null || GameManager.Instance == null) return;

            float acc = CalculateCurrentAccuracy();
            accuracyText.text = $"PRECISIÓN: {acc:F1}%";
        }

        private float CalculateCurrentAccuracy()
        {
            if (GameManager.Instance == null) return 100f;
            var gm = GameManager.Instance;
            int total = gm.PerfectHits + gm.GoodHits + gm.MissHits;
            if (total == 0) return 100f;

            float points = (gm.PerfectHits * 100f) + (gm.GoodHits * 50f);
            return points / (total * 100f) * 100f;
        }

        private void DisplayHitFeedback(HitRating rating, int laneIndex)
        {
            modernHUD?.Judge(rating);
            if(hitFeedbackText!=null)
            {
                hitFeedbackText.text=rating.ToString().ToUpperInvariant();
                if(feedbackCoroutine!=null)StopCoroutine(feedbackCoroutine);
                feedbackCoroutine=StartCoroutine(HitFeedbackRoutine());
            }
        }

        private void OverclockChanged(int charge,bool active,bool discharged)
        { UpdateOverclockUI();modernHUD?.Overclock(charge,active,discharged); }

        private void UpdateOverclockUI()
        {
            modernHUD?.Overclock(overclockCount,isOverclockActive);
            if (overclockSlider != null)
            {
                overclockSlider.value = (float)overclockCount / OVERCLOCK_MAX;
            }
            if (overclockText != null)
            {
                overclockText.text = isOverclockActive ? "<color=#00FFFF>★ FIREWALL OVERCLOCK ACTIVO ★</color>" : $"FIREWALL: {overclockCount}/{OVERCLOCK_MAX}";
            }
        }

        private IEnumerator HitFeedbackRoutine()
        {
            hitFeedbackText.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.45f);
            hitFeedbackText.gameObject.SetActive(false);
        }

        public void TogglePause()
        {
            if(GameManager.Instance.CurrentState==GameState.RescueQuestion || GameManager.Instance.CurrentState==GameState.Reviving)return;
            if (GameManager.Instance == null) return;
            EnsurePausePanelExists();

            if (GameManager.Instance.CurrentState == GameState.Playing)
            {
                GameManager.Instance.SetState(GameState.Paused);
                if (AudioManager.Instance != null) AudioManager.Instance.PauseSong();

                if (exitToMenuButton != null)
                {
                    TMP_Text exitBtnText = exitToMenuButton.GetComponentInChildren<TMP_Text>();
                    if (exitBtnText != null)
                    {
                        exitBtnText.text = IsTestPlayFromEditor ? "VOLVER AL EDITOR [Esc]" : "VOLVER AL MENÚ [Esc]";
                    }
                }

                if (pausePanel != null){pausePanel.SetActive(true);pausePanel.transform.SetAsLastSibling();}
            }
            else if (GameManager.Instance.CurrentState == GameState.Paused)
            {
                ResumeGame();
            }
        }

        public void ResumeGame()
        {
            if(GameManager.Instance.CurrentState!=GameState.Paused)return;
            if (pausePanel != null) pausePanel.SetActive(false);
            if (AudioManager.Instance != null) AudioManager.Instance.ResumeSong();
            if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.Playing);
        }

        public void RestartGame()
        {
            if(SocLearningFlow.Instance?.IsDefeatReview==true)return;
            AttemptRecorder.Instance?.Finish("abandoned");
            HideAllOverlays();
            if(TutorialGuide.Instance?.IsTraining==true){TutorialGuide.Instance.RestartCurrent();return;}
            if(IsTestPlayFromEditor) { ChartEditorManager.Instance.TestPlay(false);return; }
            var chart=ChartManager.Instance?.SelectedChart;if(chart?.audioClip==null)return;
            TutorialGuide.Instance?.Request(chart);
            NoteSpawner.Instance.LoadChart(chart);AudioManager.Instance.PlaySong(chart.audioClip,chart.PlaybackLeadIn);GameManager.Instance.StartGame();
        }

        public void ExitToMainMenu()
        {
            AttemptRecorder.Instance?.Finish("abandoned");
            if(TutorialGuide.Instance?.IsTraining==true){TutorialGuide.Instance.Cancel();CompleteExitToMainMenu();return;}
            if(!IsTestPlayFromEditor&&SocLearningFlow.Instance!=null&&!SocLearningFlow.Instance.IsOpen){
                AudioManager.Instance?.StopSong();NoteSpawner.Instance?.StopAndClear();
                SocLearningFlow.Instance.Debrief(ChartManager.Instance?.SelectedChart,CompleteExitToMainMenu);return;
            }
            CompleteExitToMainMenu();
        }
        void CompleteExitToMainMenu()
        {
            AttemptRecorder.Instance?.Finish("abandoned");
            HideAllOverlays();
            if (AudioManager.Instance != null) AudioManager.Instance.StopSong();
            if (NoteSpawner.Instance != null) NoteSpawner.Instance.StopAndClear();
            if (GameManager.Instance != null) GameManager.Instance.ResetStats();

            if (IsTestPlayFromEditor)
            {
                IsTestPlayFromEditor = false;
                ReturnToEditor();
            }
            else
            {
                if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.MainMenu);
                MainMenuUI mainMenu = FindAnyObjectByType<MainMenuUI>(FindObjectsInactive.Include);
                if (mainMenu != null)
                {
                    mainMenu.BackToMainMenu();
                }
            }
        }

        public void ReturnToEditor()
        {
            IsTestPlayFromEditor=false;
            ChartEditorManager.Instance?.RestoreAfterTest();
            HideAllOverlays();
            if (AudioManager.Instance != null) AudioManager.Instance.StopSong();
            if (NoteSpawner.Instance != null) NoteSpawner.Instance.StopAndClear();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ResetStats();
                GameManager.Instance.SetState(GameState.Editor);
            }

            MainMenuUI mainMenu = FindAnyObjectByType<MainMenuUI>(FindObjectsInactive.Include);
            if (mainMenu != null)
            {
                mainMenu.OpenEditor();
            }
        }

        private void EnsurePausePanelExists()
        {
            if(pausePanel!=null)return;
            pausePanel=CreateCandyOverlay("PauseOverlayPanel","EN PAUSA",CyberUI.Cyan);
            CyberUI.Label(pausePanel.transform,"SPYWARE: ROBA FIREWALL · TROYANO: AGRAVA LOS MISS.\nADWARE: BLOQUEA LA CURACIÓN (EFECTOS AL FALLAR).\nACIERTA EL MISMO TIPO O CARGA OVERCLOCK PARA LIMPIAR.",new Vector2(.10f,.57f),new Vector2(.90f,.76f),21,CyberUI.Muted);
            resumeButton=CyberUI.Button(pausePanel.transform,"REANUDAR / ESPACIO",new Vector2(.12f,.40f),new Vector2(.88f,.52f),ResumeGame);
            restartButton=CyberUI.Button(pausePanel.transform,"REINICIAR CANCIÓN",new Vector2(.12f,.25f),new Vector2(.49f,.37f),RestartGame);
            exitToMenuButton=CyberUI.Button(pausePanel.transform,"VOLVER / ESC",new Vector2(.51f,.25f),new Vector2(.88f,.37f),ExitToMainMenu);
            pausePanel.SetActive(false);
        }
        GameObject CreateCandyOverlay(string name,string title,Color color)
        {
            var root=CyberUI.Rect(transform,name,Vector2.zero,Vector2.one).gameObject;
            CandyUI.Backdrop(root.transform);
            var blocker=root.AddComponent<Image>();blocker.color=Color.clear;blocker.raycastTarget=true;
            CandyUI.Title(root.transform,title,new Vector2(.09f,.77f),new Vector2(.91f,.92f),64).color=color;
            CandyUI.Entrance(root);return root;
        }

        private void HandleStateChanged(GameState newState)
        {
            bool isPlayingOrPaused = (newState == GameState.TutorialInstruction || newState == GameState.Playing || newState == GameState.Paused || newState == GameState.RescueQuestion || newState == GameState.Reviving || newState == GameState.GameOver || newState == GameState.Victory);
            gameObject.SetActive(isPlayingOrPaused);
        }

        private void Handle2FARescueTriggered()
        {
            UpdateOverclockUI();
            if(IsTestPlayFromEditor){GameManager.Instance.TriggerGameOver();return;}
            if(rescueUI==null)
            {
                var root=CyberUI.Rect(transform,"SocRescueOverlay",Vector2.zero,Vector2.one);
                rescueUI=root.gameObject.AddComponent<SocRescueUI>();rescueUI.Build();
            }
            rescueUI.Show();
        }

        #region Game Over & Forensic Debrief

        private void ShowGameOverScreen()
        {
            if(!IsTestPlayFromEditor&&TutorialGuide.Instance?.IsTraining!=true&&SocLearningFlow.Instance!=null)
            {
                HideAllOverlays();
                SocLearningFlow.Instance.Defeat(ChartManager.Instance?.SelectedChart,RestartGame,CompleteExitToMainMenu);
                return;
            }
            EnsureGameOverPanelExists();
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);gameOverPanel.transform.SetAsLastSibling();

                if (GameManager.Instance != null && gameOverStatsText != null)
                {
                    var gm = GameManager.Instance;
                    float acc = CalculateCurrentAccuracy();
                    gameOverStatsText.text = $"PUNTUACIÓN: {gm.CurrentScore:N0}   |   PRECISIÓN: {acc:F1}%\n" +
                                             $"MÁXIMO COMBO: {gm.MaxCombo}   |   <color=#FF3333>FALLOS / INTRUSIONES: {gm.MissHits}</color>";
                }

                if (gameOverForensicText != null)
                {
                    CyberQuestion question = CyberQuestionBank.GetRandomQuestion();
                    string incidentLesson = question != null ? question.explanation : "Implementa políticas de privilegios mínimos y copias de seguridad inmutables.";
                    string threatCategory = question != null ? question.category.ToUpper() : "AMENAZA AVANZADA PERSISTENTE (APT)";

                    gameOverForensicText.text = $"<color=#00FFFF>INFORME FORENSE SOC (ROOT CAUSE ANALYSIS)</color>\n" +
                                               $"<color=#FF9999>Vulnerabilidad explotada:</color> Sobrecarga perimetral por falta de contención rápida ({threatCategory}).\n" +
                                               $"<color=#00FF66>Mitigación recomendada:</color> {incidentLesson}";
                }

                if (gameOverExitButton != null)
                {
                    TMP_Text exitBtnText = gameOverExitButton.GetComponentInChildren<TMP_Text>();
                    if (exitBtnText != null)
                    {
                        exitBtnText.text = IsTestPlayFromEditor ? "VOLVER AL EDITOR [Esc]" : "VOLVER AL MENÚ [Esc]";
                    }
                }
            }
        }

        private void EnsureGameOverPanelExists()
        {
            if(gameOverPanel!=null)return;
            gameOverPanel=CreateCandyOverlay("GameOverOverlayPanel","FIREWALL CAÍDO",CyberUI.Magenta);
            gameOverStatsText=CyberUI.Label(gameOverPanel.transform,"",new Vector2(.10f,.55f),new Vector2(.90f,.73f),28);
            var report=CyberUI.Box(gameOverPanel.transform,"ForensicReport",new Vector2(.10f,.25f),new Vector2(.90f,.52f),CyberUI.Panel);
            gameOverForensicText=CyberUI.Label(report.transform,"",new Vector2(.035f,.05f),new Vector2(.965f,.95f),24);gameOverForensicText.enableAutoSizing=true;gameOverForensicText.fontSizeMin=18;
            gameOverRetryButton=CyberUI.Button(gameOverPanel.transform,"REINTENTAR / ESPACIO",new Vector2(.10f,.10f),new Vector2(.49f,.21f),RestartGame);
            gameOverExitButton=CyberUI.Button(gameOverPanel.transform,"VOLVER / ESC",new Vector2(.51f,.10f),new Vector2(.90f,.21f),ExitToMainMenu);
            gameOverPanel.SetActive(false);
        }

        #endregion

        #region Victory & Forensic Debrief

        private void EnsureVictoryPanelExists()
        {
            if(victoryPanel!=null)return;
            victoryPanel=CreateCandyOverlay("VictoryOverlayPanel","RED PROTEGIDA",CyberUI.Cyan);
            victoryRankText=CyberUI.Label(victoryPanel.transform,"",new Vector2(.10f,.64f),new Vector2(.90f,.77f),48);
            victoryStatsText=CyberUI.Label(victoryPanel.transform,"",new Vector2(.10f,.44f),new Vector2(.90f,.63f),26);
            var report=CyberUI.Box(victoryPanel.transform,"Debriefing",new Vector2(.10f,.24f),new Vector2(.90f,.43f),CyberUI.Panel);
            victoryForensicText=CyberUI.Label(report.transform,"",new Vector2(.035f,.04f),new Vector2(.965f,.96f),22);victoryForensicText.enableAutoSizing=true;victoryForensicText.fontSizeMin=17;
            victoryRetryButton=CyberUI.Button(victoryPanel.transform,"REINTENTAR / ESPACIO",new Vector2(.10f,.09f),new Vector2(.49f,.20f),RestartGame);
            victoryExitButton=CyberUI.Button(victoryPanel.transform,"VOLVER / ESC",new Vector2(.51f,.09f),new Vector2(.90f,.20f),ExitToMainMenu);
            victoryPanel.SetActive(false);
        }

        private void ShowVictoryScreen()
        {
            EnsureVictoryPanelExists();
            if (victoryPanel != null){victoryPanel.SetActive(true);victoryPanel.transform.SetAsLastSibling();}

            if (GameManager.Instance != null)
            {
                var gm = GameManager.Instance;
                float acc = CalculateCurrentAccuracy();
                string rank = EvaluateRank(acc, gm.MissHits);

                if (victoryRankText != null)
                {
                    victoryRankText.text = rank;
                }

                if (victoryStatsText != null)
                {
                    victoryStatsText.text = $"PUNTUACIÓN: {gm.CurrentScore:N0}   |   PRECISIÓN: {acc:F1}%\n" +
                                           $"MÁXIMO COMBO: {gm.MaxCombo}\n" +
                                           $"<color=#00FFFF>PERFECT: {gm.PerfectHits}</color>  |  <color=#FFCC00>GOOD: {gm.GoodHits}</color>  |  <color=#FF3333>MISS: {gm.MissHits}</color>";
                }

                // Save High Score, Max Combo and Rank to PlayerPrefs
                var activeChart = ChartManager.Instance != null ? ChartManager.Instance.SelectedChart : null;
                if (activeChart != null && !IsTestPlayFromEditor && TutorialGuide.Instance?.IsTraining!=true)
                {
                    try { Leaderboards.Provider.Submit(new LeaderboardScore { playerName=PlayerProfile.DisplayName, chartId=activeChart.StableId,difficultyId=activeChart.DifficultyId,score=gm.CurrentScore,accuracy=acc,maxCombo=gm.MaxCombo,rank=GetSimpleRank(acc,gm.MissHits),timestamp=System.DateTime.UtcNow.ToString("o") }); }
                    catch(System.Exception ex) { Debug.LogWarning("Leaderboard: "+ex.Message); }
                    string songKey = activeChart.StableId+"_"+activeChart.laneLayout;
                    string diffKey = activeChart.DifficultyId;
                    int prevScore = PlayerPrefs.GetInt($"HighScore_{songKey}_{diffKey}", 0);
                    if (gm.CurrentScore > prevScore)
                    {
                        PlayerPrefs.SetInt($"HighScore_{songKey}_{diffKey}", gm.CurrentScore);
                        PlayerPrefs.SetString($"HighRank_{songKey}_{diffKey}", GetSimpleRank(acc, gm.MissHits));
                        PlayerPrefs.SetInt($"MaxCombo_{songKey}_{diffKey}", gm.MaxCombo);
                        PlayerPrefs.Save();
                    }
                }

                if (victoryForensicText != null)
                {
                    CyberQuestion question = CyberQuestionBank.GetRandomQuestion();
                    string tip = question != null ? question.explanation : "Aplica siempre el principio de privilegios mínimos en tus cuentas y servicios.";
                    victoryForensicText.text = $"<color=#00FF66>SESIÓN COMPLETADA / REPASO SOC DISPONIBLE</color>\n" +
                                              $"• Estatus defensivo: La sesión terminó con el firewall operativo. Revisa tus aciertos y fallos para seguir mejorando.\n" +
                                              $"<color=#00FFFF>• Recomendación de seguridad:</color> {tip}";
                }

                if (victoryExitButton != null)
                {
                    TMP_Text exitBtnText = victoryExitButton.GetComponentInChildren<TMP_Text>();
                    if (exitBtnText != null)
                    {
                        exitBtnText.text = IsTestPlayFromEditor ? "VOLVER AL EDITOR [Esc]" : "VOLVER AL MENÚ [Esc]";
                    }
                }
            }
        }

        #endregion

        private void EnsureHealthSliderExists()
        {
            if (healthSlider != null) return;

            healthSlider = GetComponentInChildren<Slider>(true);
            if (healthSlider != null) return;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            GameObject hpObj = new GameObject("HealthSlider");
            hpObj.transform.SetParent(transform, false);
            RectTransform hpRt = hpObj.AddComponent<RectTransform>();
            hpRt.anchorMin = new Vector2(0.5f, 1f);
            hpRt.anchorMax = new Vector2(0.5f, 1f);
            hpRt.anchoredPosition = new Vector2(0f, -38f);
            hpRt.sizeDelta = new Vector2(360f, 22f);

            healthSlider = hpObj.AddComponent<Slider>();
            healthSlider.interactable = false;

            GameObject hpBg = new GameObject("Background");
            hpBg.transform.SetParent(hpObj.transform, false);
            RectTransform hpBgRt = hpBg.AddComponent<RectTransform>();
            hpBgRt.anchorMin = Vector2.zero;
            hpBgRt.anchorMax = Vector2.one;
            hpBgRt.sizeDelta = Vector2.zero;
            Image hpBgImg = hpBg.AddComponent<Image>();
            hpBgImg.sprite = CreateSolidSprite();
            hpBgImg.color = new Color(0.08f, 0.10f, 0.16f, 0.9f);

            GameObject hpFillArea = new GameObject("Fill Area");
            hpFillArea.transform.SetParent(hpObj.transform, false);
            RectTransform hpFaRt = hpFillArea.AddComponent<RectTransform>();
            hpFaRt.anchorMin = Vector2.zero;
            hpFaRt.anchorMax = Vector2.one;
            hpFaRt.sizeDelta = new Vector2(-6f, -6f);

            GameObject hpFill = new GameObject("Fill");
            hpFill.transform.SetParent(hpFillArea.transform, false);
            RectTransform hpFRt = hpFill.AddComponent<RectTransform>();
            hpFRt.anchorMin = Vector2.zero;
            hpFRt.anchorMax = Vector2.one;
            hpFRt.sizeDelta = Vector2.zero;
            Image hpFImg = hpFill.AddComponent<Image>();
            hpFImg.sprite = CreateSolidSprite();
            hpFImg.color = new Color(0.0f, 1.0f, 0.6f, 1.0f);

            healthSlider.targetGraphic = hpFImg;
            healthSlider.fillRect = hpFRt;
            healthSlider.minValue = 0f;
            healthSlider.maxValue = 1f;
            healthSlider.value = 1f;

            GameObject hpLbl = new GameObject("HealthLabel");
            hpLbl.transform.SetParent(hpObj.transform, false);
            RectTransform hpLRt = hpLbl.AddComponent<RectTransform>();
            hpLRt.anchorMin = Vector2.zero;
            hpLRt.anchorMax = Vector2.one;
            hpLRt.sizeDelta = Vector2.zero;
            Text hpLText = hpLbl.AddComponent<Text>();
            hpLText.font = font;
            hpLText.text = "INTEGRIDAD FIREWALL";
            hpLText.fontSize = 13;
            hpLText.fontStyle = FontStyle.Bold;
            hpLText.color = Color.white;
            hpLText.alignment = TextAnchor.MiddleCenter;
        }

        private Sprite CreateSolidSprite()
        {
            Texture2D tex = new Texture2D(2, 2);
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        }

        private string EvaluateRank(float accuracy, int misses)
        {
            if (accuracy >= 99.5f && misses == 0) return "<color=#FFD700>RANGO S+ (PERFECTO)</color>";
            if (accuracy >= 95.0f && misses == 0) return "<color=#00FFFF>RANGO S (EXCELENTE)</color>";
            if (accuracy >= 90.0f) return "<color=#00FF66>RANGO A</color>";
            if (accuracy >= 80.0f) return "<color=#FFCC00>RANGO B</color>";
            if (accuracy >= 70.0f) return "<color=#FF9900>RANGO C</color>";
            return "<color=#FF3333>RANGO F (FALLIDO)</color>";
        }

        public static string GetSimpleRank(float accuracy, int misses)
        {
            if (accuracy >= 99.5f && misses == 0) return "S+";
            if (accuracy >= 95.0f && misses == 0) return "S";
            if (accuracy >= 90.0f) return "A";
            if (accuracy >= 80.0f) return "B";
            if (accuracy >= 70.0f) return "C";
            return "F";
        }

        public void DismissPracticeOverlays()=>HideAllOverlays();
        private void HideAllOverlays()
        {
            if (pausePanel != null) pausePanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (victoryPanel != null) victoryPanel.SetActive(false);
            if (rescueUI != null) rescueUI.Hide();
            if (hitFeedbackText != null) hitFeedbackText.gameObject.SetActive(false);
        }
    }
}
