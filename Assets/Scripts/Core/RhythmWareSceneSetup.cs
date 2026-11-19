using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RhythmWare.Core;
using RhythmWare.Rhythm;
using RhythmWare.Malware;
using RhythmWare.UI;

namespace RhythmWare.Core
{
    /// <summary>
    /// Automatic Scene Bootstrapper for RhythmWare.
    /// Exclusively initializes the Scene, Camera, Background, Board, and Main Menu.
    /// Strictly starts at the Main Menu with no audio playback or dummy notes.
    /// </summary>
    public class RhythmWareSceneSetup : MonoBehaviour
    {
        [Header("Auto-Setup Options")]
        [SerializeField] private bool autoBuildSceneOnStart = true;

        [Header("Custom Background Sprite")]
        [Tooltip("Asigna aquí la imagen de fondo cuando la tengas lista. Si está vacía, se usa el fondo ciberespacio.")]
        [SerializeField] private Sprite customBackgroundSprite;

        private GameObject gameBoardContainer;

        private void Awake()
        {
            EnsureCoreManagersExist();
            if (autoBuildSceneOnStart)
            {
                BuildSceneHierarchy();
            }
        }

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged += HandleGameStateChanged;
                GameManager.Instance.SetState(GameState.MainMenu);
            }
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopSong();
            }

            UpdateBoardVisibility(GameState.MainMenu);
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged -= HandleGameStateChanged;
            }
        }

        private GameObject gameplayHUDObj;

        private void HandleGameStateChanged(GameState state)
        {
            UpdateBoardVisibility(state);
            UpdateHUDVisibility(state);
        }

        private void UpdateHUDVisibility(GameState state)
        {
            if (gameplayHUDObj == null)
            {
                var ui = FindAnyObjectByType<RhythmWare.UI.GameplayUI>(FindObjectsInactive.Include);
                if (ui != null) gameplayHUDObj = ui.gameObject;
                else gameplayHUDObj = GameObject.Find("GameplayHUD");
            }

            if (gameplayHUDObj != null)
            {
                bool showHUD = (state == GameState.TutorialInstruction || state == GameState.Playing || state == GameState.Paused || state == GameState.RescueQuestion || state == GameState.Reviving || state == GameState.GameOver || state == GameState.Victory);
                gameplayHUDObj.SetActive(showHUD);
            }
        }

        private void UpdateBoardVisibility(GameState state)
        {
            if (gameBoardContainer == null)
            {
                gameBoardContainer = GameObject.Find("[GameBoardContainer]");
            }

            if (gameBoardContainer != null)
            {
                bool showBoard = (state == GameState.TutorialInstruction || state == GameState.Playing || state == GameState.Paused || state == GameState.RescueQuestion || state == GameState.Reviving || state == GameState.GameOver);
                gameBoardContainer.SetActive(showBoard);
            }
        }

        public void EnsureCoreManagersExist()
        {
            if (GameManager.Instance == null)
            {
                GameObject gmObj = new GameObject("[GameManager]");
                gmObj.AddComponent<GameManager>();
            }

            if (AudioManager.Instance == null)
            {
                GameObject amObj = new GameObject("[AudioManager]");
                amObj.AddComponent<AudioManager>();
            }

            if (InputManager.Instance == null)
            {
                GameObject imObj = new GameObject("[InputManager]");
                imObj.AddComponent<InputManager>();
            }

            if (ChartManager.Instance == null)
            {
                GameObject cmObj = new GameObject("[ChartManager]");
                cmObj.AddComponent<ChartManager>();
            }

            if (BackgroundManager.Instance == null)
            {
                GameObject bgObj = new GameObject("[BackgroundManager]");
                BackgroundManager bg = bgObj.AddComponent<BackgroundManager>();
                if (customBackgroundSprite != null)
                {
                    bg.SetBackground(customBackgroundSprite);
                }
            }

            if (ChartEditorManager.Instance == null)
            {
                GameObject edObj = new GameObject("[ChartEditorManager]");
                edObj.AddComponent<ChartEditorManager>();
            }
            if(ChartManager.Instance.GetComponent<RhythmWare.Boss.BonziStageController>()==null)
                ChartManager.Instance.gameObject.AddComponent<RhythmWare.Boss.BonziStageController>();
            if(ChartManager.Instance.GetComponent<TutorialGuide>()==null)ChartManager.Instance.gameObject.AddComponent<TutorialGuide>();
        }

        public void BuildSceneHierarchy()
        {
            // 1. Camera setup
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                mainCam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
            }
            mainCam.backgroundColor = new Color(0.02f, 0.03f, 0.06f, 1f); // Deep Space Black
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.orthographic = true;
            mainCam.orthographicSize = 5f;

            // 2. The PNG's road opening is the emission point for future bosses.
            bool epicStage=true;
            Vector3 originPos = epicStage?EpicStageLayout.PixelToWorld(EpicStageLayout.OriginPixel):new Vector3(0f,4,0);

            GameObject originObj = GameObject.Find("CentralNetworkNode");
            if (originObj == null)
            {
                originObj = new GameObject("CentralNetworkNode");
                originObj.transform.position = originPos;

                SpriteRenderer originSr = originObj.AddComponent<SpriteRenderer>();
                originSr.sprite = NetworkVisualAssets.Shape(10);
                originSr.color = new Color(0f, 0.95f, 1f, 0.9f); // Cyan Core
                originObj.transform.localScale = Vector3.one * 0.5f;

                GameObject labelObj = new GameObject("LabelText");
                labelObj.transform.SetParent(originObj.transform);
                labelObj.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                TextMesh tm = labelObj.AddComponent<TextMesh>();
                tm.text = "INBOUND // GATEWAY";
                tm.fontSize = 20;
                tm.characterSize = 0.14f;
                tm.color = new Color(0f, 0.95f, 1f, 1f);
                tm.alignment = TextAlignment.Center;
                tm.anchor = TextAnchor.MiddleCenter;
            }
            if(epicStage)
            {
                originObj.GetComponent<SpriteRenderer>().enabled=false;
                var originLabel=originObj.GetComponentInChildren<TextMesh>();
                if(originLabel!=null)originLabel.gameObject.SetActive(false);
            }

            // 3. Lane Controller & Board Visual Generator (Exact Sketch Outline)
            if (LaneController.Instance == null)
            {
                GameObject laneObj = new GameObject("[LaneController]");
                LaneController laneController = laneObj.AddComponent<LaneController>();

                // 7 Hitboxes layout positions (Matching sketch: 3 Left stack, 3 Right stack, 1 Center Special)
                var hitboxPositions=new Vector3[LaneLayout.Count];
                for(int i=0;i<LaneLayout.Count;i++)hitboxPositions[i]=EpicStageLayout.ReceptorPosition(i);
                var hitboxTransforms=new Transform[LaneLayout.Count];
                string[] keyLabels = CyberUI.LaneNames;

                for (int i = 0; i < LaneLayout.Count; i++)
                {
                    Color laneColor=new Color(.035f,.025f,.085f,1);
                    // Create Hitbox Target Box (Square matching sketch)
                    GameObject hb = new GameObject($"Hitbox_Lane_{i}_({keyLabels[i]})");
                    hb.transform.SetParent(laneObj.transform);
                    hb.transform.position = hitboxPositions[i];

                    SpriteRenderer sr = hb.AddComponent<SpriteRenderer>();
                    sr.sprite = NetworkVisualAssets.Shape(i==LaneLayout.Center?17:15);
                    sr.color = laneColor;
                    sr.sortingOrder=6;
                    hb.transform.localScale = (i == LaneLayout.Center) ? new Vector3(1.4f, 0.7f, 1f) : Vector3.one * 0.9f;
                    if(epicStage)hb.transform.localScale=EpicStageLayout.ReceptorScale(i);

                    // Key text label
                    GameObject keyTextObj = new GameObject("KeyLabel");
                    keyTextObj.transform.SetParent(hb.transform);
                    keyTextObj.transform.localPosition = new Vector3(0f,0f,-.1f);
                    TextMesh tm = keyTextObj.AddComponent<TextMesh>();
                    tm.text = keyLabels[i];
                    tm.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    keyTextObj.GetComponent<MeshRenderer>().sharedMaterial=tm.font.material;
                    keyTextObj.GetComponent<MeshRenderer>().sortingOrder=250;
                    hb.AddComponent<TerminalFeedback>().Initialize(i,laneColor,!epicStage);
                    tm.fontSize = 64;
                    tm.characterSize = 0.18f;
                    if(epicStage)
                    {
                        tm.characterSize=.115f;
                        keyTextObj.transform.localScale=new Vector3(1/hb.transform.localScale.x,1/hb.transform.localScale.y,1);
                    }
                    tm.fontStyle = FontStyle.BoldAndItalic;
                    tm.color = CandyMenuGraphic.Cream;
                    tm.alignment = TextAlignment.Center;
                    tm.anchor = TextAnchor.MiddleCenter;

                    hitboxTransforms[i] = hb.transform;
                }

                // The artwork supplies road edges; retain only ambient data traffic.
                BoardVisualGenerator visualGen = laneObj.AddComponent<BoardVisualGenerator>();
                // Build route visuals after typed lane initialization.

                laneController.Configure(originObj.transform,hitboxTransforms,epicStage);
                if(epicStage)laneObj.AddComponent<EpicStagePresenter>().Initialize(originObj.transform,hitboxTransforms);
                visualGen.BuildBoardVisuals(laneObj.transform,hitboxPositions);
            }

            // 4. Note Spawner setup
            if (NoteSpawner.Instance == null)
            {
                GameObject spawnerObj = new GameObject("[NoteSpawner]");
                NoteSpawner spawner = spawnerObj.AddComponent<NoteSpawner>();

                GameObject notePrefab = new GameObject("NotePrefab");
                SpriteRenderer sr = notePrefab.AddComponent<SpriteRenderer>();
                sr.sprite = CreateSquareSprite();
                notePrefab.transform.localScale = Vector3.one * 0.55f;
                notePrefab.AddComponent<NoteController>();
                notePrefab.SetActive(false);

                spawner.Configure(notePrefab);
            }

            // Group all board components inside [GameBoardContainer]
            if (gameBoardContainer == null)
            {
                gameBoardContainer = GameObject.Find("[GameBoardContainer]");
                if (gameBoardContainer == null)
                {
                    gameBoardContainer = new GameObject("[GameBoardContainer]");
                }
            }

            if (originObj != null) originObj.transform.SetParent(gameBoardContainer.transform);
            if (LaneController.Instance != null) LaneController.Instance.transform.SetParent(gameBoardContainer.transform);
            if (NoteSpawner.Instance != null) NoteSpawner.Instance.transform.SetParent(gameBoardContainer.transform);

            // Hide the entire board initially while in menus!
            gameBoardContainer.SetActive(false);

            // 5. UI Canvas & Overlay setup
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("UI Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            if (ScreenGlitchEffect.Instance == null)
            {
                GameObject glitchObj = new GameObject("ScreenGlitchOverlay");
                glitchObj.transform.SetParent(canvas.transform, false);

                RectTransform rect = glitchObj.AddComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;

                CanvasGroup cg = glitchObj.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                cg.blocksRaycasts = false;

                Image img = glitchObj.AddComponent<Image>();
                img.color = new Color(0.1f, 0.0f, 0.2f, 0.85f);

                glitchObj.AddComponent<ScreenGlitchEffect>();
            }

            if (MalwareEffectManager.Instance == null)
            {
                GameObject malwareMgrObj = new GameObject("[MalwareEffectManager]");
                malwareMgrObj.transform.SetParent(canvas.transform, false);
                malwareMgrObj.AddComponent<MalwareEffectManager>();
                malwareMgrObj.transform.SetAsLastSibling();
            }

            var scaler=canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;scaler.matchWidthOrHeight=.5f;
            var hudRect=CyberUI.Rect(canvas.transform,"GameplayHUD",Vector2.zero,Vector2.one);
            var modernHUD=hudRect.gameObject.AddComponent<GameplayHUD>();modernHUD.Build();
            var gameplay=hudRect.gameObject.AddComponent<GameplayUI>();gameplay.Configure(modernHUD);
            gameplayHUDObj=hudRect.gameObject;gameplayHUDObj.SetActive(false);

            // 7. Build Full Menu Suite (Main Menu, Level Select, Chart Editor, Settings, Extra Content)
            string[] panelNames = new string[] { "MainMenuPanel", "LevelSelectPanel", "EditorPanel", "SettingsPanel", "ExtraContentPanel" };
            foreach (var pName in panelNames)
            {
                Transform t = canvas.transform.Find(pName);
                if (t != null) DestroyImmediate(t.gameObject);
            }

            MenuHierarchyBuilder.BuildFullMenuSuite(canvas);
            var learning=new GameObject("SocLearningFlow");learning.transform.SetParent(canvas.transform,false);
            var learningRect=learning.AddComponent<RectTransform>();learningRect.anchorMin=Vector2.zero;learningRect.anchorMax=Vector2.one;learningRect.offsetMin=learningRect.offsetMax=Vector2.zero;learning.AddComponent<SocLearningFlow>();
        }

        private Sprite CreateSquareSprite()
        {
            Texture2D tex = new Texture2D(32, 32);
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    tex.SetPixel(x, y, Color.white);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
        }
    }
}

