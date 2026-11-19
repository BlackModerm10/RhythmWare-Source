using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RhythmWare.Education;

namespace RhythmWare.UI
{
    public sealed class SocQuestionCard
    {
        public Button[] Options { get; private set; }
        public CyberQuestion Question { get; private set; }
        TextMeshProUGUI category,question,feedback,caseLabel,lesson;
        readonly int[] displayToCanonical={0,1,2};
        public int CanonicalOption(int displayed)=>displayToCanonical[displayed];
        TextMeshProUGUI[] optionLabels,statusLabels;
        Image feedbackPanel;
        public void Build(Transform parent,Action<int> select)
        {
            var card=CyberUI.Box(parent,"SocQuestionCard",Vector2.zero,Vector2.one,CyberUI.Panel);
            CyberUI.Box(card.transform,"Accent",new Vector2(0,.992f),Vector2.one,CyberUI.Cyan);
            caseLabel=CyberUI.Label(card.transform,"CASO PRÁCTICO",new Vector2(.035f,.945f),new Vector2(.965f,.99f),17,CyberUI.Cyan);
            category=CyberUI.Label(card.transform,"",new Vector2(.035f,.89f),new Vector2(.965f,.945f),21,CyberUI.Magenta);
            caseLabel.rectTransform.offsetMin=caseLabel.rectTransform.offsetMax=Vector2.zero;
            category.rectTransform.offsetMin=category.rectTransform.offsetMax=Vector2.zero;
            lesson=CyberUI.Label(card.transform,"",new Vector2(.035f,.72f),new Vector2(.965f,.89f),22,CyberUI.Muted);Fit(lesson,18,22);
            question=CyberUI.Label(card.transform,"",new Vector2(.035f,.55f),new Vector2(.965f,.72f),27);Fit(question,20,27);
            Options=new Button[3];optionLabels=new TextMeshProUGUI[3];statusLabels=new TextMeshProUGUI[3];
            for(int i=0;i<3;i++)
            {
                int index=i;float top=.54f-i*.115f;
                var row=CyberUI.Box(card.transform,"Option"+i,new Vector2(.035f,top-.105f),new Vector2(.965f,top),CyberUI.Raised,2);row.raycastTarget=true;
                var button=row.gameObject.AddComponent<Button>();button.targetGraphic=row;button.navigation=new Navigation{mode=Navigation.Mode.Automatic};
                var colors=button.colors;colors.highlightedColor=new Color(.75f,1,1);colors.disabledColor=Color.white;button.colors=colors;
                button.onClick.AddListener(()=>select(displayToCanonical[index]));Options[i]=button;
                var chip=CyberUI.Box(row.transform,"Number",new Vector2(.013f,.20f),new Vector2(.074f,.80f),CyberUI.Raised);
                var number=CyberUI.Label(chip.transform,(i+1).ToString(),Vector2.zero,Vector2.one,26,CyberUI.Cyan);number.alignment=TextAlignmentOptions.Center;Fit(number,16,26);
                number.rectTransform.offsetMin=number.rectTransform.offsetMax=Vector2.zero;
                optionLabels[i]=CyberUI.Label(row.transform,"",new Vector2(.09f,.03f),new Vector2(.83f,.97f),24);Fit(optionLabels[i],18,24);
                optionLabels[i].rectTransform.offsetMin=new Vector2(4,0);optionLabels[i].rectTransform.offsetMax=new Vector2(-4,0);
                statusLabels[i]=CyberUI.Label(row.transform,"",new Vector2(.83f,.15f),new Vector2(.985f,.85f),15,CyberUI.Muted);statusLabels[i].alignment=TextAlignmentOptions.Center;
            }
            feedbackPanel=CyberUI.Box(card.transform,"Protocol",new Vector2(.035f,.012f),new Vector2(.965f,.20f),new Color(.19f,.10f,.30f));
            feedback=CyberUI.Label(feedbackPanel.transform,"",new Vector2(.025f,.07f),new Vector2(.975f,.93f),21,CyberUI.Muted);Fit(feedback,16,21);
        }
        static void Fit(TextMeshProUGUI text,float min,float max)
        { text.enableAutoSizing=true;text.fontSizeMin=min;text.fontSizeMax=max;text.overflowMode=TextOverflowModes.Overflow; }
        public void Bind(CyberQuestion value,string progress,string hint="Elige una respuesta. Usa 1, 2, 3 o haz clic en una opción.")
        {
            bool changed=Question!=value;Question=value;caseLabel.text=progress;category.text=value.category;question.text=value.questionText;
            lesson.text="<b>"+value.lessonTitle+"</b>\n"+value.lessonText;
            if(changed){for(int i=0;i<3;i++)displayToCanonical[i]=i;
            for(int i=2;i>0;i--){int j=UnityEngine.Random.Range(0,i+1);int swap=displayToCanonical[i];displayToCanonical[i]=displayToCanonical[j];displayToCanonical[j]=swap;}}
            feedback.text=hint;feedback.color=CyberUI.Muted;feedbackPanel.color=new Color(.19f,.10f,.30f);
            for(int i=0;i<Options.Length;i++)
            {
                bool visible=i<value.options.Length;Options[i].gameObject.SetActive(visible);if(!visible)continue;
                Options[i].interactable=true;Options[i].GetComponent<Image>().color=CyberUI.Raised;optionLabels[i].text=value.options[displayToCanonical[i]];statusLabels[i].text="";
            }
        }
        public void Reveal(int selected)
        {
            bool correct=selected==Question.correctIndex;
            for(int i=0;i<Options.Length;i++)
            {
                int canonical=displayToCanonical[i];Options[i].interactable=false;bool answer=canonical==Question.correctIndex;
                Options[i].GetComponent<Image>().color=answer?new Color(.09f,.30f,.28f):canonical==selected?new Color(.30f,.095f,.15f):CyberUI.Raised;
                statusLabels[i].text=answer?"CORRECTA":canonical==selected?"TU ELECCIÓN":"";statusLabels[i].color=answer?CyberUI.Cyan:CyberUI.Red;
            }
            feedbackPanel.color=correct?new Color(.09f,.25f,.24f):new Color(.18f,.07f,.12f);feedback.color=Color.white;
            feedback.text=(correct?"<b>PROTOCOLO CORRECTO</b>\n":"<b>REVISA EL PROTOCOLO</b>\n")+(Question.optionFeedback!=null?Question.optionFeedback[selected]:Question.explanation);
        }
    }
}
