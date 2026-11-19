using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using RhythmWare.Education;

namespace RhythmWare.UI
{
    public sealed class CyberAcademyUI : MonoBehaviour
    {
        readonly Dictionary<string,int> answers=new Dictionary<string,int>();
        readonly Dictionary<string,Button> modules=new Dictionary<string,Button>();
        List<CyberQuestion> all,visible;
        int index;
        string module="Todos";
        SocQuestionCard card;
        TextMeshProUGUI progress,rank;
        Image progressFill;
        bool built;
        public CyberQuestion CurrentQuestion => visible!=null && visible.Count>0?visible[index]:null;
        public int AnsweredCount => answers.Count;
        public int CorrectCount => all==null?0:all.Count(q=>answers.TryGetValue(q.id,out int chosen)&&chosen==q.correctIndex);
        public int VisibleCount => visible?.Count??0;
        public SocQuestionCard QuestionCard => card;
        void Start() { if(!built)Build(); }
        public void Build()
        {
            if(built)return;built=true;all=CyberQuestionBank.GetAllQuestions();
            foreach(var answer in SocLearningProgress.LatestAnswers())if(all.Any(q=>q.id==answer.Key))answers[answer.Key]=answer.Value;
            var side=CyberUI.Box(transform,"CourseSidebar",Vector2.zero,new Vector2(.255f,1),CyberUI.Panel);
            CyberUI.Label(side.transform,"TU FORMACIÓN",new Vector2(.055f,.91f),new Vector2(.95f,.98f),18,CyberUI.Cyan);
            progress=CyberUI.Label(side.transform,"",new Vector2(.055f,.83f),new Vector2(.95f,.92f),30);
            rank=CyberUI.Label(side.transform,"",new Vector2(.055f,.76f),new Vector2(.95f,.84f),16,CyberUI.Muted);
            var track=CyberUI.Box(side.transform,"Completion",new Vector2(.075f,.727f),new Vector2(.92f,.737f),CyberUI.Raised);
            progressFill=CyberUI.Box(track.transform,"Fill",Vector2.zero,Vector2.one,CyberUI.Cyan);
            CyberUI.Label(side.transform,"MÓDULOS DE PRÁCTICA",new Vector2(.055f,.66f),new Vector2(.95f,.72f),16,CyberUI.Muted);
            CyberUI.Scroll(side.transform,new Vector2(.04f,.24f),new Vector2(.96f,.66f),out var content);
            var names=new[]{"Todos"}.Concat(all.Select(q=>q.relatedThreat).Distinct()).ToArray();
            content.sizeDelta=new Vector2(0,names.Length*46);
            for(int i=0;i<names.Length;i++)
            {
                string filter=names[i];var b=CyberUI.Button(content,ModuleName(filter),Vector2.zero,Vector2.one,()=>SelectModule(filter));
                var rt=(RectTransform)b.transform;rt.anchorMin=new Vector2(0,1);rt.anchorMax=Vector2.one;rt.pivot=new Vector2(.5f,1);rt.anchoredPosition=new Vector2(0,-i*46);rt.sizeDelta=new Vector2(-8,42);
                modules[filter]=b;
            }
            CyberUI.Label(side.transform,"<b>APRENDER A TU RITMO</b>\nLee el protocolo y resuelve los casos. Tu aprendizaje se guarda aparte de los combos. No necesitas ganar ni lograr OVERCLOCK para practicar.",new Vector2(.055f,.025f),new Vector2(.95f,.23f),18,CyberUI.Muted);
            var questionRoot=CyberUI.Rect(transform,"Practice",new Vector2(.28f,.12f),Vector2.one);
            card=new SocQuestionCard();card.Build(questionRoot,Answer);
            CyberUI.Button(transform,"Anterior",new Vector2(.28f,.02f),new Vector2(.45f,.095f),PrevQuestion);
            CyberUI.Button(transform,"Repetir módulo",new Vector2(.47f,.02f),new Vector2(.69f,.095f),RestartQuiz);
            CyberUI.Button(transform,"Siguiente  /  Enter",new Vector2(.71f,.02f),new Vector2(1,.095f),NextQuestion,new Color(.37f,.15f,.43f));
            SelectModule("Todos");
        }
        static string ModuleName(string value)
        {
            switch(value){case "Todos":return "Todos los casos";case "General":return "Fundamentos";case "Phishing":return "Phishing";case "Trojan":return "Troyanos";case "Worm":return "Propagación y parches";case "Spyware":return "Espionaje y privacidad";default:return value;}
        }
        public void SelectModule(string value)
        {
            module=value;visible=all.Where(q=>value=="Todos"||q.relatedThreat==value).ToList();index=0;
            foreach(var entry in modules)entry.Value.GetComponent<Image>().color=entry.Key==value?new Color(.37f,.15f,.43f):CyberUI.Raised;
            DisplayCurrentQuestion();
        }
        public void DisplayCurrentQuestion()
        {
            var q=CurrentQuestion;if(q==null)return;
            if(gameObject.activeInHierarchy&&RhythmWare.Core.GameManager.Instance?.CurrentState==RhythmWare.Core.GameState.ExtraContent)SocLearningProgress.Seen(q);
            card.Bind(q,$"CASO {index+1:00} / {visible.Count:00}   ·   {ModuleName(module).ToUpper()}");
            if(answers.TryGetValue(q.id,out int chosen))card.Reveal(chosen);
            progress.text=$"{AnsweredCount:00} / {all.Count:00} practicados";
            rank.text=$"Correctos: {CorrectCount} · Primer intento: {SocLearningProgress.FirstTryCorrect}\n"+(SocLearningProgress.LastSaveSucceeded?"Guardado local":"No se pudo guardar en disco");
            progressFill.rectTransform.anchorMax=new Vector2((float)AnsweredCount/all.Count,1);
        }
        public void Answer(int option)
        {
            var q=CurrentQuestion;if(q==null || answers.ContainsKey(q.id) || option<0 || option>=q.options.Length)return;
            SocLearningProgress.Record(q,option,"academy");answers.Add(q.id,option);DisplayCurrentQuestion();
        }
        public void NextQuestion() { if(visible?.Count>0){index=(index+1)%visible.Count;DisplayCurrentQuestion();} }
        public void PrevQuestion() { if(visible?.Count>0){index=(index-1+visible.Count)%visible.Count;DisplayCurrentQuestion();} }
        public void RestartQuiz() { foreach(var q in visible)answers.Remove(q.id);index=0;DisplayCurrentQuestion(); }
        void OnEnable()
        {if(!built)return;answers.Clear();foreach(var answer in SocLearningProgress.LatestAnswers())if(all.Any(q=>q.id==answer.Key))answers[answer.Key]=answer.Value;DisplayCurrentQuestion();}
        void Update()
        {
            if(CandyMenuTransition.IsRunning)return;
            var keyboard=Keyboard.current;if(keyboard==null || CurrentQuestion==null)return;
            if(keyboard.digit1Key.wasPressedThisFrame||keyboard.numpad1Key.wasPressedThisFrame)Answer(card.CanonicalOption(0));
            else if(keyboard.digit2Key.wasPressedThisFrame||keyboard.numpad2Key.wasPressedThisFrame)Answer(card.CanonicalOption(1));
            else if(keyboard.digit3Key.wasPressedThisFrame||keyboard.numpad3Key.wasPressedThisFrame)Answer(card.CanonicalOption(2));
            if(keyboard.rightArrowKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)NextQuestion();
            if(keyboard.leftArrowKey.wasPressedThisFrame)PrevQuestion();
        }
    }
}
