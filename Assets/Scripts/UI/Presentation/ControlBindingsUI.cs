using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using RhythmWare.Core;

namespace RhythmWare.UI
{
    public sealed class ControlBindingsUI : MonoBehaviour
    {
        readonly Button[] keys=new Button[5],pads=new Button[5];
        TextMeshProUGUI status;Action back;int listening=-1;bool padMode;float armed,ignoreSubmitUntil;Gamepad shown;
        public bool Listening=>listening>=0;
        public void Build(Action back)
        {
            this.back=back;CandyUI.Backdrop(transform);
            CandyUI.Title(transform,"CONTROLES / A TU MANERA",new Vector2(.05f,.87f),new Vector2(.80f,.97f),38);
            CyberUI.Button(transform,"VOLVER / ESC",new Vector2(.81f,.88f),new Vector2(.96f,.96f),Close);
            CyberUI.Label(transform,"Elige una casilla y pulsa su nueva tecla o botón. Si ya está asignado, los carriles intercambian controles.",new Vector2(.06f,.77f),new Vector2(.94f,.85f),23);
            CyberUI.Label(transform,"CARRIL",new Vector2(.08f,.70f),new Vector2(.35f,.76f),22,CyberUI.Muted);
            CyberUI.Label(transform,"TECLADO",new Vector2(.38f,.70f),new Vector2(.62f,.76f),22,CyberUI.Cyan);
            CyberUI.Label(transform,"MANDO",new Vector2(.67f,.70f),new Vector2(.92f,.76f),22,CyberUI.Magenta);
            string[] names={"IZQUIERDA EXTERIOR","IZQUIERDA INTERIOR","DERECHA INTERIOR","DERECHA EXTERIOR","CENTRO"};
            for(int row=0;row<5;row++){int lane=LaneLayout.VisualOrder[row];float top=.69f-row*.095f;var line=CyberUI.Box(transform,"BindingRow"+lane,new Vector2(.06f,top-.08f),new Vector2(.94f,top),CyberUI.Panel);CyberUI.Label(line.transform,names[lane],new Vector2(.03f,.12f),new Vector2(.35f,.88f),21,EpicStageLayout.LaneColor(lane));keys[lane]=CyberUI.Button(line.transform,"",new Vector2(.37f,.10f),new Vector2(.63f,.90f),()=>Choose(lane,false));pads[lane]=CyberUI.Button(line.transform,"",new Vector2(.68f,.10f),new Vector2(.96f,.90f),()=>Choose(lane,true));}
            status=CyberUI.Label(transform,"",new Vector2(.06f,.12f),new Vector2(.94f,.20f),20,CyberUI.Cyan);
            CyberUI.Button(transform,"RESTABLECER CONTROLES",new Vector2(.06f,.035f),new Vector2(.48f,.11f),()=>{CancelCapture();InputManager.Instance.ResetBindings();Refresh();});
            CyberUI.Label(transform,"P / ESC o START: pausa. F3 / F4: velocidad de notas. ESC cancela una asignación. Teclado y mando funcionan juntos.",new Vector2(.51f,.025f),new Vector2(.95f,.115f),18,CyberUI.Muted);Refresh();
        }
        public void Listen(int lane,bool pad){listening=lane;padMode=pad;armed=Time.unscaledTime+.2f;InputManager.Instance.CapturingBinding=true;status.text=pad?"PULSA UN BOTÓN DEL MANDO · ESC CANCELA":"PULSA UNA TECLA · ESC CANCELA";}
        void Choose(int lane,bool pad){if(!Listening&&Time.unscaledTime>=ignoreSubmitUntil)Listen(lane,pad);}
        public void CancelCapture(){listening=-1;ignoreSubmitUntil=Time.unscaledTime+.25f;if(InputManager.Instance!=null)InputManager.Instance.CapturingBinding=false;}
        void Close(){CancelCapture();back?.Invoke();}
        public void FocusFirst(){if(UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject((InputManager.Instance.UsingGamepad?pads[0]:keys[0]).gameObject);}
        public void Refresh(){if(status==null)return;shown=Gamepad.current;for(int i=0;i<5;i++){keys[i].GetComponentInChildren<TextMeshProUGUI>().text=InputManager.Instance.KeyboardLabel(i);pads[i].GetComponentInChildren<TextMeshProUGUI>().text=InputManager.Instance.PadLabel(i);}status.text=Gamepad.current==null?"TECLADO LISTO · conecta un mando compatible para usarlo o asignarlo.":"MANDO CONECTADO · "+Gamepad.current.displayName;}
        void Update()
        {
            if(!Listening&&shown!=Gamepad.current)Refresh();
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true||Gamepad.current?.startButton.wasPressedThisFrame==true){if(Listening){CancelCapture();Refresh();}else Close();return;}
            if(!Listening||Time.unscaledTime<armed)return;
            if(padMode){if(Gamepad.current==null)return;foreach(var b in InputManager.BindablePad)if(Gamepad.current[b].wasPressedThisFrame){InputManager.Instance.RemapPad(listening,b);CancelCapture();Refresh();return;}}
            else if(Keyboard.current!=null)foreach(var k in Keyboard.current.allKeys)if(k.wasPressedThisFrame){if(!InputManager.Instance.RemapKey(listening,k.keyCode)){status.text="ESC / P: pausa. F3 / F4: velocidad. Elige otra tecla.";return;}CancelCapture();Refresh();return;}
        }
        void OnDisable(){CancelCapture();}
    }
}
