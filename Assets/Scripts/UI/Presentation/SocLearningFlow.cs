using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using RhythmWare.Core;
using RhythmWare.Rhythm;
using RhythmWare.Education;

namespace RhythmWare.UI
{
    public sealed class SocLearningFlow : MonoBehaviour
    {
        public static SocLearningFlow Instance { get; private set; }
        GameObject overlay;
        Action complete;
        Action briefCancel;
        CyberQuestion current;
        SocQuestionCard card;
        bool answered;
        bool defeat;
        float feedbackAt,openedAt;
        Action defeatExit;
        UnityEngine.UI.Button retry;
        public bool IsDefeatReview=>IsOpen&&defeat;
        public bool CanRetry=>IsDefeatReview&&answered&&Time.unscaledTime-feedbackAt>=3;
        public bool IsOpen=>overlay!=null;
        public CyberQuestion CurrentQuestion=>current;
        void Awake(){Instance=this;}
        Transform Shell(string title)
        {
            Close();CyberUI.ModalOpen=true;
            overlay=CyberUI.Box(transform,"SocLearningOverlay",Vector2.zero,Vector2.one,new Color(.035f,.015f,.08f,.98f)).gameObject;
            overlay.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;overlay.transform.SetAsLastSibling();CandyUI.Backdrop(overlay.transform);
            CyberUI.Label(overlay.transform,title,new Vector2(.06f,.88f),new Vector2(.94f,.97f),32,CyberUI.Cyan);
            return overlay.transform;
        }
        public void Briefing(ChartData chart,Action start,Action cancel)
        {
            var parent=Shell("ANTES DEL RITMO / PROTOCOLO SOC");
            briefCancel=cancel;
            CyberUI.Label(parent,string.Join("     ·     ",LaneLayout.VisualOrder.Select(i=>InputManager.Instance.LabelForLane(i))),new Vector2(.07f,.78f),new Vector2(.93f,.87f),34);
            string[] focus=chart.tutorialId=="sara-basics-5"?new[]{"Spyware"}:chart.bossId=="bonzi-buddy"?new[]{"Ransomware","DDoS"}:new[]{"Adware","Trojan"};
            for(int i=0;i<focus.Length;i++)
            {
                string topic=focus[i];var first=CyberQuestionBank.GetAllQuestions().First(q=>q.relatedThreat==topic);
                var lesson=topic=="Spyware"?first:CyberQuestionBank.GetAllQuestions().First(q=>q.relatedThreat==topic&&q.objectiveId.EndsWith(topic=="Ransomware"?"contain":topic=="DDoS"?"mitigate":topic=="Adware"?"install":"disguise"));
                foreach(var topicLesson in CyberQuestionBank.GetAllQuestions().Where(q=>q.relatedThreat==topic))SocLearningProgress.Seen(topicLesson);float top=.76f-i*.235f;
                var box=CyberUI.Box(parent,"Briefing_"+topic,new Vector2(.07f,top-.21f),new Vector2(.93f,top),CyberUI.Panel);
                CyberUI.Label(box.transform,CyberQuestionBank.Name(topic)+" / "+lesson.lessonTitle,new Vector2(.025f,.66f),new Vector2(.975f,.97f),26,CyberUI.Magenta);
                var text=CyberUI.Label(box.transform,CyberQuestionBank.BriefingSummary(topic),new Vector2(.025f,.05f),new Vector2(.975f,.67f),26);text.enableAutoSizing=true;text.fontSizeMin=20;
            }
            CyberUI.Label(parent,chart.bossId=="bonzi-buddy"?"Bonzi usa Spyware, Adware y Troyanos. Ransomware y DDoS se practican como casos SOC, sin añadir enemigos nuevos.":"En el juego, pulsar representa una defensa. En la vida real debes identificar, contener y seguir el protocolo adecuado.",new Vector2(.07f,.21f),new Vector2(.93f,.30f),21,CyberUI.Muted);
            CyberUI.Button(parent,"VOLVER",new Vector2(.07f,.075f),new Vector2(.35f,.17f),()=>{Close();cancel?.Invoke();});
            CyberUI.Button(parent,"PREPARADO / JUGAR",new Vector2(.40f,.075f),new Vector2(.93f,.17f),()=>{Close();start();});
        }
        public void Debrief(ChartData chart,Action done)
        {
            var parent=Shell("DESPUÉS DEL RITMO / APRENDE DEL CASO");complete=done;
            string focus=chart?.bossId=="bonzi-buddy"?"DDoS":chart?.tutorialId=="sara-basics-5"?"Spyware":"Adware";
            current=CyberQuestionBank.GetRandomQuestionForThreat(focus);answered=false;
            SocLearningProgress.Seen(current);GameManager.Instance.SetState(GameState.Learning);
            var area=CyberUI.Rect(parent,"DebriefCard",new Vector2(.08f,.17f),new Vector2(.92f,.87f));
            card=new SocQuestionCard();card.Build(area,Answer);card.Bind(current,"PRÁCTICA SIN REQUISITO DE COMBO");
            CyberUI.Button(parent,"VOLVER AL MENÚ",new Vector2(.08f,.04f),new Vector2(.39f,.13f),Finish);
            CyberUI.Button(parent,"ABRIR LA ACADEMIA",new Vector2(.43f,.04f),new Vector2(.92f,.13f),()=>{var callback=complete;Close();callback?.Invoke();MainMenuUI.Instance.OpenExtraContent();});
        }
        public void Defeat(ChartData chart,Action again,Action leave)
        {
            var parent=Shell("FIREWALL CAÍDO / APRENDE DEL INCIDENTE");
            defeat=true;answered=false;complete=again;defeatExit=leave;openedAt=Time.unscaledTime;
            string focus=CyberQuestionBank.Normalize(SocLearningProgress.LastThreat);
            current=CyberQuestionBank.GetRandomQuestionForThreat(string.IsNullOrEmpty(focus)?"Spyware":focus);
            SocLearningProgress.Seen(current);GameManager.Instance.SetState(GameState.Learning);
            var gm=GameManager.Instance;
            CyberUI.Label(parent,$"Puntuación {gm.CurrentScore:N0} · Perfect {gm.PerfectHits} · Good {gm.GoodHits} · Miss {gm.MissHits}. Responde y revisa la explicación antes de reintentar.",new Vector2(.07f,.79f),new Vector2(.93f,.87f),21,CyberUI.Muted);
            var area=CyberUI.Rect(parent,"DefeatCase",new Vector2(.08f,.17f),new Vector2(.92f,.79f));
            card=new SocQuestionCard();card.Build(area,Answer);card.Bind(current,"REPASO SOC / UNA DECISIÓN PARA EL PRÓXIMO INTENTO","Lee el caso y elige una respuesta. Después revisaremos por qué.");
            retry=CyberUI.Button(parent,"RESPONDE PARA REINTENTAR",new Vector2(.43f,.04f),new Vector2(.92f,.13f),Finish);retry.interactable=false;
            CyberUI.Button(parent,"VOLVER AL MENÚ / ESC",new Vector2(.08f,.04f),new Vector2(.39f,.13f),LeaveDefeat);
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
        }
        void LeaveDefeat(){var callback=defeatExit;Close();callback?.Invoke();}
        public void Answer(int canonical)
        {
            if(answered||current==null||canonical<0||canonical>=current.options.Length||defeat&&Time.unscaledTime-openedAt<.75f)return;
            answered=true;feedbackAt=Time.unscaledTime;SocLearningProgress.Record(current,canonical,defeat?"defeat":"debrief");card.Reveal(canonical);
            if(defeat)UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
        }
        public void Finish(){if(defeat&&!CanRetry)return;var callback=complete;Close();callback?.Invoke();}
        public void Close(){if(overlay!=null){overlay.SetActive(false);Destroy(overlay);overlay=null;}current=null;card=null;complete=null;briefCancel=null;defeat=false;defeatExit=null;retry=null;CyberUI.ModalOpen=false;}
        void Update()
        {
            if(!IsOpen)return;
            if(defeat&&retry!=null){retry.interactable=CanRetry;var label=retry.GetComponentInChildren<TextMeshProUGUI>();label.text=!answered?"RESPONDE PARA REINTENTAR":CanRetry?"HE REVISADO EL CASO / REINTENTAR":"REVISA LA EXPLICACIÓN · "+Mathf.CeilToInt(3-(Time.unscaledTime-feedbackAt))+" s";}
            if(Keyboard.current==null)return;var k=Keyboard.current;
            if(card==null){if(k.escapeKey.wasPressedThisFrame){var cancel=briefCancel;Close();cancel?.Invoke();}return;}
            if(k.digit1Key.wasPressedThisFrame)Answer(card.CanonicalOption(0));else if(k.digit2Key.wasPressedThisFrame)Answer(card.CanonicalOption(1));else if(k.digit3Key.wasPressedThisFrame)Answer(card.CanonicalOption(2));
            if(k.escapeKey.wasPressedThisFrame){if(defeat)LeaveDefeat();else Finish();}
        }
    }
}
