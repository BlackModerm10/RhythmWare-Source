using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using RhythmWare.Core;

namespace RhythmWare.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Menu Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject levelSelectPanel;
        [SerializeField] private GameObject editorPanel;
        [SerializeField] private GameObject extraContentPanel;
        [SerializeField] private GameObject settingsPanel;

        [Header("Main Buttons")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button editorButton;
        [SerializeField] private Button extraContentButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button exitButton;

        public static MainMenuUI Instance { get; private set; }

        private Button[] menuButtons;
        private int selectedIndex = 0;
        CandyMenuTransition transition;
        GameObject visiblePanel;
        bool ready;

        public void Configure(GameObject main,GameObject songs,GameObject editor,GameObject settings,GameObject extra,Button[] buttons)
        {
            mainMenuPanel=main;levelSelectPanel=songs;editorPanel=editor;settingsPanel=settings;extraContentPanel=extra;
            playButton=buttons[0];editorButton=buttons[1];extraContentButton=buttons[2];settingsButton=buttons[3];exitButton=buttons[4];
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (playButton != null) playButton.onClick.AddListener(OpenLevelSelect);
            if (editorButton != null) editorButton.onClick.AddListener(OpenEditor);
            if (extraContentButton != null) extraContentButton.onClick.AddListener(OpenExtraContent);
            if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
            if (exitButton != null) exitButton.onClick.AddListener(QuitGame);

            menuButtons = new Button[] { playButton, editorButton, extraContentButton, settingsButton, exitButton };

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged += HandleGameStateChanged;
            }

            ShowPanel(mainMenuPanel);
            ready=true;
            HighlightButton(selectedIndex);
            StartCoroutine(OfferFirstRunAudio());
        }


        System.Collections.IEnumerator OfferFirstRunAudio()
        {
            yield return null;
#if UNITY_EDITOR
            if(UnityEditor.SessionState.GetBool("RWVisualValidation",false))yield break;
#endif
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--rw-self-test")>=0)yield break;
            if(ParticipantProfiles.Current==null){OpenParticipantProfiles(true);yield break;}
            if(!GameSettings.HasSeenAudioSetup&&GameManager.Instance.CurrentState==GameState.MainMenu)
            {OpenSettings();settingsPanel.GetComponent<SettingsUI>().OpenFirstRunCalibration();}
        }
        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged -= HandleGameStateChanged;
            }
        }

        private void HandleGameStateChanged(GameState newState)
        {
            if (newState == GameState.MainMenu)
            {
                ShowPanel(mainMenuPanel);
            }
            else if (newState == GameState.Playing)
            {
                ShowPanel(null); // Hide all menu panels during gameplay
            }
        }

        private void Update()
        {
            if(CandyMenuTransition.IsRunning)return;
            if (mainMenuPanel != null && !mainMenuPanel.activeSelf) return;
            if (Keyboard.current == null) return;

            // Keyboard navigation (Up / Down)
            if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame)
            {
                selectedIndex = (selectedIndex - 1 + menuButtons.Length) % menuButtons.Length;
                HighlightButton(selectedIndex);
            }
            else if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)
            {
                selectedIndex = (selectedIndex + 1) % menuButtons.Length;
                HighlightButton(selectedIndex);
            }
            // Submit is owned by InputSystemUIInputModule; do not invoke twice.
        }

        private void HighlightButton(int index, bool playSound = true)
        {
            if (playSound && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayMenuHover();
            }

            for (int i = 0; i < menuButtons.Length; i++)
            {
                if (menuButtons[i] != null)
                {
                    var candy=menuButtons[i].GetComponent<CandyMenuButton>();
                    if(candy!=null)candy.SetSelected(i==index);
                    if(i==index)menuButtons[i].Select();
                }
            }
        }

        public void FocusMenu(int index)
        {
            if(menuButtons==null||index<0||index>=menuButtons.Length||selectedIndex==index)return;
            selectedIndex=index;HighlightButton(index);
        }

        public void OpenChartInEditor(RhythmWare.Rhythm.ChartData chart,string sourcePath=null)
        {
            OpenEditor();editorPanel.GetComponent<ChartEditorUI>().OpenCatalogChart(chart,sourcePath);
        }

        public void OpenLevelSelect()
        {
            if(RequireProfile())return;
            if (AudioManager.Instance != null) AudioManager.Instance.PlayMenuClick();
            if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.LevelSelect);
            ShowPanel(levelSelectPanel);
        }

        public void OpenEditor()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayMenuClick();
                AudioManager.Instance.StopMenuBGM();
            }
            if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.Editor);
            ShowPanel(editorPanel);
        }

        public void OpenExtraContent()
        {
            if(RequireProfile())return;
            if (AudioManager.Instance != null) AudioManager.Instance.PlayMenuClick();
            if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.ExtraContent);
            ShowPanel(extraContentPanel);
        }

        bool RequireProfile(){if(ProfileStorage.IsValidation||ParticipantProfiles.Current!=null)return false;OpenParticipantProfiles(true);return true;}
        public void OpenParticipantProfiles(bool welcome=false){OpenSettings();settingsPanel.GetComponent<SettingsUI>().OpenProfiles(welcome);}
        public void OpenSettings()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayMenuClick();
            if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.Settings);
            ShowPanel(settingsPanel);
        }

        public void BackToMainMenu()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayMenuBack();
                AudioManager.Instance.PlayMenuBGM();
            }
            if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.MainMenu);
            ShowPanel(mainMenuPanel);
            HighlightButton(selectedIndex, false);
        }

        public void QuitGame()
        {
            Debug.Log("[MainMenuUI] Quitting game application...");
            Application.Quit();
        }

        public void ShowPanel(GameObject targetPanel)
        {
            bool changed=visiblePanel!=targetPanel;visiblePanel=targetPanel;
            if (mainMenuPanel != null) mainMenuPanel.SetActive(targetPanel == mainMenuPanel);
            if (levelSelectPanel != null) levelSelectPanel.SetActive(targetPanel == levelSelectPanel);
            if (editorPanel != null) editorPanel.SetActive(targetPanel == editorPanel);
            if (extraContentPanel != null) extraContentPanel.SetActive(targetPanel == extraContentPanel);
            if (settingsPanel != null) settingsPanel.SetActive(targetPanel == settingsPanel);
            if(transition==null)transition=gameObject.AddComponent<CandyMenuTransition>();
            if(targetPanel==null)transition.Cancel();
            else if(ready&&changed)
            {
                CandyUI.Entrance(targetPanel);
                transition.Play(targetPanel==mainMenuPanel?"RHYTHMWARE":targetPanel==levelSelectPanel?"TRACK SELECT":targetPanel==editorPanel?"MAPPING STUDIO":targetPanel==extraContentPanel?"CYBER ACADEMY":"SETTINGS");
            }
        }
    }
}
