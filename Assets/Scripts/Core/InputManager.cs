using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmWare.Core
{
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }
        public event Action<int> OnLanePressed,OnLaneReleased;
        public event Action<int,double> OnLanePressedDsp,OnLaneReleasedDsp;
        public event Action OnBindingsChanged;
        readonly InputAction[] laneActions=new InputAction[LaneLayout.Count];
        public bool UsingGamepad { get; private set; }
        public bool CapturingBinding { get; set; }
        string preferencePrefix="";
        string PrefsPrefix=>preferencePrefix.Length>0?preferencePrefix:ParticipantProfiles.Current!=null?"RWProfile_"+ParticipantProfiles.Current.localId+"_":ProfileStorage.IsValidation?"RWValidation_":"";
#if UNITY_EDITOR
        public void UseValidationStore(string prefix)
        {if(preferencePrefix.Length>0){for(int i=0;i<5;i++){PlayerPrefs.DeleteKey(preferencePrefix+"Keys_"+LaneLayout.Id+"_"+i);PlayerPrefs.DeleteKey(preferencePrefix+"Pad_"+LaneLayout.Id+"_"+i);}}preferencePrefix=prefix??"";ReloadBindings();}
#endif
        Key[] laneKeys;
        GamepadButton[] padKeys;
        readonly bool[] held=new bool[LaneLayout.Count];
        float wheelCarry;bool wheelEffects;
        public static readonly GamepadButton[] DefaultPad={GamepadButton.LeftShoulder,GamepadButton.LeftTrigger,GamepadButton.RightShoulder,GamepadButton.RightTrigger,GamepadButton.South};
        public static readonly GamepadButton[] BindablePad={GamepadButton.South,GamepadButton.East,GamepadButton.West,GamepadButton.North,GamepadButton.LeftShoulder,GamepadButton.RightShoulder,GamepadButton.LeftTrigger,GamepadButton.RightTrigger,GamepadButton.DpadLeft,GamepadButton.DpadRight,GamepadButton.DpadUp,GamepadButton.DpadDown,GamepadButton.LeftStick,GamepadButton.RightStick};
        void Awake(){if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}Instance=this;DontDestroyOnLoad(gameObject);ReloadBindings();}
        public void ReloadBindings()
        {
            laneKeys=(Key[])LaneLayout.DefaultKeys.Clone();padKeys=(GamepadButton[])DefaultPad.Clone();
            for(int i=0;i<5;i++)
            {
                var k=(Key)PlayerPrefs.GetInt(PrefsPrefix+"Keys_"+LaneLayout.Id+"_"+i,(int)laneKeys[i]);if(ValidKey(k))laneKeys[i]=k;
                var p=(GamepadButton)PlayerPrefs.GetInt(PrefsPrefix+"Pad_"+LaneLayout.Id+"_"+i,(int)padKeys[i]);if(BindablePad.Contains(p))padKeys[i]=p;
            }
            if(laneKeys.Distinct().Count()!=5)laneKeys=(Key[])LaneLayout.DefaultKeys.Clone();
            if(padKeys.Distinct().Count()!=5)padKeys=(GamepadButton[])DefaultPad.Clone();
            RebuildLaneActions();FlushHeld();OnBindingsChanged?.Invoke();
        }
        public static bool ValidKey(Key key)=>Enum.IsDefined(typeof(Key),key)&&key!=Key.None&&key!=Key.Escape&&key!=Key.P&&key!=Key.F3&&key!=Key.F4;
        void Update()
        {
            var kb=Keyboard.current;var pad=Gamepad.current;
            bool padPress=pad!=null&&(BindablePad.Any(b=>pad[b].wasPressedThisFrame)||pad.startButton.wasPressedThisFrame);
            bool keyPress=kb!=null&&kb.allKeys.Any(k=>k.wasPressedThisFrame);
            bool use=padPress?true:keyPress?false:UsingGamepad&&pad!=null;
            if(use!=UsingGamepad){UsingGamepad=use;OnBindingsChanged?.Invoke();}
            var state=GameManager.Instance!=null?GameManager.Instance.CurrentState:GameState.MainMenu;
            AdjustGameplayVolume(state,kb);
            if(!CapturingBinding&&!RhythmWare.UI.CyberUI.ModalOpen&&kb!=null&&(state==GameState.Playing||state==GameState.Paused||state==GameState.TutorialInstruction))
            {if(kb.f3Key.wasPressedThisFrame)GameSettings.NoteSpeed-=.1f;if(kb.f4Key.wasPressedThisFrame)GameSettings.NoteSpeed+=.1f;}
            if(CapturingBinding||state!=GameState.Playing&&state!=GameState.Editor)
            {
                FlushHeld();
                if(padPress&&!CapturingBinding&&EventSystem.current!=null)
                {
                    var choices=Selectable.allSelectablesArray.Where(s=>s.isActiveAndEnabled&&s.IsInteractable()).ToArray();
                    foreach(var button in choices.OfType<Button>())if(button.navigation.mode==Navigation.Mode.None)button.navigation=new Navigation{mode=Navigation.Mode.Automatic};
                    if(EventSystem.current.currentSelectedGameObject==null||!EventSystem.current.currentSelectedGameObject.activeInHierarchy)
                    {var first=choices.FirstOrDefault();if(first!=null)EventSystem.current.SetSelectedGameObject(first.gameObject);}
                }
                return;
            }
            // Lane transitions are dispatched by Input System callbacks, including all
            // transitions queued during a long frame, with their original timestamps.
        }
        void RebuildLaneActions()
        {
            bool previousCapture=CapturingBinding;CapturingBinding=true;
            for(int i=0;i<laneActions.Length;i++)
            {
                laneActions[i]?.Dispose();int lane=i;
                var action=new InputAction("Lane"+i,InputActionType.Button);
                action.AddBinding("<Keyboard>/"+(Keyboard.current!=null?Keyboard.current[laneKeys[i]].name:laneKeys[i].ToString()));
                action.AddBinding("<Gamepad>/"+PadControlPath(padKeys[i]));
                action.performed+=ctx=>DispatchLane(lane,true,ctx.time);
                action.canceled+=ctx=>DispatchLane(lane,false,ctx.time);
                laneActions[i]=action;action.Enable();
            }
            CapturingBinding=previousCapture;
        }
        static string PadControlPath(GamepadButton button)
        {
            switch(button){case GamepadButton.South:return "buttonSouth";case GamepadButton.East:return "buttonEast";case GamepadButton.West:return "buttonWest";case GamepadButton.North:return "buttonNorth";
            case GamepadButton.DpadLeft:return "dpad/left";case GamepadButton.DpadRight:return "dpad/right";case GamepadButton.DpadUp:return "dpad/up";case GamepadButton.DpadDown:return "dpad/down";case GamepadButton.LeftStick:return "leftStickPress";case GamepadButton.RightStick:return "rightStickPress";default:return button.ToString();}
        }
        void DispatchLane(int lane,bool down,double eventTime)
        {
            bool wasHeld=held[lane];held[lane]=down;
            var state=GameManager.Instance!=null?GameManager.Instance.CurrentState:GameState.MainMenu;
            if(CapturingBinding||RhythmWare.UI.CyberUI.ModalOpen||state!=GameState.Playing&&state!=GameState.Editor)return;
            double dsp=AudioSettings.dspTime+(eventTime-Time.realtimeSinceStartupAsDouble);
            if(down&&!wasHeld){OnLanePressedDsp?.Invoke(lane,dsp);OnLanePressed?.Invoke(lane);}
            else if(!down&&wasHeld){OnLaneReleasedDsp?.Invoke(lane,dsp);OnLaneReleased?.Invoke(lane);}
        }
        void OnDestroy(){foreach(var action in laneActions)action?.Dispose();}
        void AdjustGameplayVolume(GameState state,Keyboard kb)
        {
            bool gameplay=state==GameState.Playing||state==GameState.Paused||state==GameState.TutorialInstruction;
            if(!gameplay||CapturingBinding||RhythmWare.UI.CyberUI.ModalOpen||RhythmWare.UI.CyberUI.Typing||Mouse.current==null){wheelCarry=0;return;}
            float delta=Mouse.current.scroll.y.ReadValue();if(Mathf.Abs(delta)<.0001f)return;
            if(InputSystem.settings.scrollDeltaBehavior==InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange&&(Application.platform==RuntimePlatform.WindowsEditor||Application.platform==RuntimePlatform.WindowsPlayer))delta/=120;
            bool effects=kb?.leftShiftKey.isPressed==true||kb?.rightShiftKey.isPressed==true;
            if(effects!=wheelEffects){wheelCarry=0;wheelEffects=effects;}
            wheelCarry+=delta;int ticks=(int)wheelCarry;if(ticks==0)return;wheelCarry-=ticks;
            if(effects)GameSettings.SFXVolume+=ticks*.05f;else GameSettings.MasterVolume+=ticks*.05f;
            AudioManager.Instance?.UpdateVolumes();
        }
        public void FlushHeld(){if(laneKeys==null)return;for(int i=0;i<5;i++)held[i]=IsLaneHeld(i);}
        public Key GetKeyForLane(int lane)=>LaneLayout.Valid(lane)?laneKeys[lane]:Key.None;
        public GamepadButton GetPadForLane(int lane)=>padKeys[Mathf.Clamp(lane,0,4)];
        public bool IsLaneHeld(int lane)=>LaneLayout.Valid(lane)&&((Keyboard.current!=null&&Keyboard.current[laneKeys[lane]].isPressed)||(Gamepad.current!=null&&Gamepad.current[padKeys[lane]].isPressed));
        public string KeyboardLabel(int lane){var k=GetKeyForLane(lane);return k==Key.Space?"SPACE":Keyboard.current?[k]?.displayName?.ToUpperInvariant()??k.ToString().ToUpperInvariant();}
        public string PadLabel(int lane)
        {
            var b=GetPadForLane(lane);bool ps=Gamepad.current!=null&&Gamepad.current.layout.ToLowerInvariant().Contains("dual");
            switch(b){case GamepadButton.LeftShoulder:return ps?"L1":"LB";case GamepadButton.RightShoulder:return ps?"R1":"RB";case GamepadButton.LeftTrigger:return ps?"L2":"LT";case GamepadButton.RightTrigger:return ps?"R2":"RT";case GamepadButton.South:return ps?"×":"A";case GamepadButton.East:return ps?"○":"B";case GamepadButton.West:return ps?"□":"X";case GamepadButton.North:return ps?"△":"Y";case GamepadButton.DpadLeft:return "←";case GamepadButton.DpadRight:return "→";case GamepadButton.DpadUp:return "↑";case GamepadButton.DpadDown:return "↓";case GamepadButton.LeftStick:return "L3";default:return "R3";}
        }
        public string LabelForLane(int lane)=>UsingGamepad?PadLabel(lane):KeyboardLabel(lane);
        public string ConfirmLabel=>Gamepad.current!=null&&Gamepad.current.layout.ToLowerInvariant().Contains("dual")?"CRUZ":"A";
        public bool RemapKey(int lane,Key key,bool save=true)
        {
            if(!LaneLayout.Valid(lane)||!ValidKey(key))return false;int other=Array.IndexOf(laneKeys,key);if(other>=0)laneKeys[other]=laneKeys[lane];laneKeys[lane]=key;Changed(save);return true;
        }
        public bool RemapPad(int lane,GamepadButton button,bool save=true)
        {if(!LaneLayout.Valid(lane)||!BindablePad.Contains(button))return false;int other=Array.IndexOf(padKeys,button);if(other>=0)padKeys[other]=padKeys[lane];padKeys[lane]=button;Changed(save);return true;}
        public void ResetBindings(bool save=true){laneKeys=(Key[])LaneLayout.DefaultKeys.Clone();padKeys=(GamepadButton[])DefaultPad.Clone();Changed(save);}
        void Changed(bool save)
        {
            if(save){for(int i=0;i<5;i++){PlayerPrefs.SetInt(PrefsPrefix+"Keys_"+LaneLayout.Id+"_"+i,(int)laneKeys[i]);PlayerPrefs.SetInt(PrefsPrefix+"Pad_"+LaneLayout.Id+"_"+i,(int)padKeys[i]);}PlayerPrefs.Save();}
            bool capture=CapturingBinding;CapturingBinding=true;RebuildLaneActions();CapturingBinding=capture;FlushHeld();OnBindingsChanged?.Invoke();
        }
    }
}
