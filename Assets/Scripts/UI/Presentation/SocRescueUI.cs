using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using RhythmWare.Core;

namespace RhythmWare.UI
{
    public sealed class SocRescueUI : MonoBehaviour
    {
        SocQuestionCard card;
        TextMeshProUGUI title,number,status;
        Image dim;
        GameObject questionShell,countdownShell;
        TextMeshProUGUI countdownNumber;
        public SocQuestionCard QuestionCard => card;
        public void Build()
        {
            dim=CyberUI.Box(transform,"RecoveryDim",Vector2.zero,Vector2.one,new Color(.095f,.045f,.21f,.96f));dim.raycastTarget=true;
            var shell=CyberUI.Rect(transform,"RecoveryShell",new Vector2(.065f,.08f),new Vector2(.935f,.92f));
            questionShell=shell.gameObject;
            CyberUI.Label(shell,"SOC / PROTOCOLO DE RECUPERACIÓN",new Vector2(0,.90f),new Vector2(.9f,1),30,CyberUI.Cyan);
            var side=CyberUI.Box(shell,"RecoveryStatus",Vector2.zero,new Vector2(.26f,.88f),CyberUI.Panel);
            title=CyberUI.Label(side.transform,"FIREWALL CAÍDO",new Vector2(.07f,.77f),new Vector2(.93f,.96f),26);
            number=CyberUI.Label(side.transform,"0%",new Vector2(.06f,.44f),new Vector2(.94f,.79f),100,CyberUI.Cyan);number.alignment=TextAlignmentOptions.Center;
            status=CyberUI.Label(side.transform,"",new Vector2(.07f,.15f),new Vector2(.93f,.44f),21,CyberUI.Muted);
            CyberUI.Button(side.transform,"Finalizar intento  /  Esc",new Vector2(.05f,.035f),new Vector2(.95f,.125f),()=>GameManager.Instance.ForfeitRescue());
            var content=CyberUI.Rect(shell,"Challenge",new Vector2(.285f,0),new Vector2(1,.88f));
            card=new SocQuestionCard();card.Build(content,i=>GameManager.Instance.AnswerRescue(i));
            var ready=CyberUI.Box(transform,"ReadyCountdown",new Vector2(.33f,.67f),new Vector2(.67f,.85f),CyberUI.Panel);countdownShell=ready.gameObject;
            var heading=CyberUI.Label(ready.transform,"PREPÁRATE PARA VOLVER",new Vector2(.03f,.73f),new Vector2(.97f,.98f),18,CyberUI.Cyan);heading.alignment=TextAlignmentOptions.Center;
            countdownNumber=CyberUI.Label(ready.transform,"3",new Vector2(.15f,.18f),new Vector2(.85f,.78f),78);countdownNumber.alignment=TextAlignmentOptions.Center;
            var keys=CyberUI.Label(ready.transform,"FIREWALL 30%  /  D F · SPACE · J K",new Vector2(.02f,.005f),new Vector2(.98f,.22f),18,CyberUI.Muted);keys.alignment=TextAlignmentOptions.Center;
        }
        void OnEnable()
        {
            var gm=GameManager.Instance;if(gm==null)return;
            gm.OnStateChanged+=State;gm.OnRescueAnswered+=Refresh;gm.OnRescueCountdownChanged+=Countdown;
        }
        void OnDisable()
        {
            var gm=GameManager.Instance;if(gm==null)return;
            gm.OnStateChanged-=State;gm.OnRescueAnswered-=Refresh;gm.OnRescueCountdownChanged-=Countdown;
        }
        public void Show() { gameObject.SetActive(true);transform.SetAsLastSibling();Refresh(); }
        public void Hide() { gameObject.SetActive(false); }
        void State(GameState state) { if(state==GameState.RescueQuestion||state==GameState.Reviving)Refresh();else Hide(); }
        void Countdown(int seconds) { Refresh(); }
        void Refresh()
        {
            var gm=GameManager.Instance;if(card==null||gm.RescueQuestion==null)return;
            RhythmWare.Education.SocLearningProgress.Seen(gm.RescueQuestion);
            card.Bind(gm.RescueQuestion,"OVERCLOCK LOGRADO   ·   RESCATE SOC 1 / 1","Responde correctamente para recuperar el 30% del firewall. La canción está detenida.");
            if(gm.RescueAnswerLocked)card.Reveal(gm.RescueSelectedOption);
            bool reviving=gm.CurrentState==GameState.Reviving;
            questionShell.SetActive(!reviving);countdownShell.SetActive(reviving);dim.color=new Color(.095f,.045f,.21f,reviving?.18f:.96f);
            countdownNumber.text=Mathf.Max(1,gm.RescueCountdown).ToString();
            title.text=reviving?"PREPÁRATE PARA VOLVER":"FIREWALL CAÍDO";
            number.text=reviving?Mathf.Max(1,gm.RescueCountdown).ToString():"0%";
            status.text=reviving?"<b>FIREWALL RESTAURADO: 30%</b>\n\nColoca los dedos en los carriles. La canción continúa al terminar la cuenta atrás.":gm.RescueAnswerLocked?"<b>RECUPERACIÓN DENEGADA</b>\n\nRevisa el protocolo. Este intento ha terminado.":"<b>OVERCLOCK DESBLOQUEÓ TU RESCATE</b>\n\nUna pregunta. Una oportunidad. Elige con 1, 2, 3 o el ratón.";
        }
        void Update()
        {
            var key=Keyboard.current;if(key==null)return;
            if(key.escapeKey.wasPressedThisFrame){GameManager.Instance.ForfeitRescue();return;}
            if(key.digit1Key.wasPressedThisFrame||key.numpad1Key.wasPressedThisFrame)GameManager.Instance.AnswerRescue(card.CanonicalOption(0));
            else if(key.digit2Key.wasPressedThisFrame||key.numpad2Key.wasPressedThisFrame)GameManager.Instance.AnswerRescue(card.CanonicalOption(1));
            else if(key.digit3Key.wasPressedThisFrame||key.numpad3Key.wasPressedThisFrame)GameManager.Instance.AnswerRescue(card.CanonicalOption(2));
        }
    }
}
