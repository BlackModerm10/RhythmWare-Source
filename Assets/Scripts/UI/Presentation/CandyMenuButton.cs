using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace RhythmWare.UI
{
    public sealed class CandyMenuButton : MonoBehaviour,IPointerEnterHandler,ISelectHandler
    {
        Image background;
        TextMeshProUGUI caption,indexLabel,hint;
        Color accent;
        Action focus;
        bool selected;
        Vector2 rest;
        RectTransform rect;
        public void Initialize(int index,string label,string description,Color color,Action onFocus)
        {
            rect=(RectTransform)transform;rest=rect.anchoredPosition;accent=color;focus=onFocus;background=GetComponent<Image>();
            GetComponent<Button>().transition=Selectable.Transition.None;
            indexLabel=CyberUI.Label(transform,(index+1).ToString("00"),new Vector2(.035f,.16f),new Vector2(.16f,.86f),30,CandyMenuGraphic.Mint);
            caption=CyberUI.Label(transform,label,new Vector2(.18f,.32f),new Vector2(.94f,.92f),33,CandyMenuGraphic.Cream);caption.fontStyle=FontStyles.Bold;
            caption.enableAutoSizing=true;caption.fontSizeMin=23;
            hint=CyberUI.Label(transform,description,new Vector2(.18f,.06f),new Vector2(.94f,.36f),16,CandyMenuGraphic.Cream);hint.fontSize=16;
            var stripe=CyberUI.Box(transform,"CandyAccent",new Vector2(.975f,0),Vector2.one,accent);
            SetSelected(false);
        }
        public void SetSelected(bool value)
        {
            selected=value;if(background==null)return;background.color=value?accent:new Color(.16f,.075f,.29f);
            hint.color=caption.color=indexLabel.color=value?CandyMenuGraphic.Ink:CandyMenuGraphic.Cream;
        }
        public void OnPointerEnter(PointerEventData data) { focus?.Invoke(); }
        public void OnSelect(BaseEventData data) { if(!selected)focus?.Invoke(); }
        void Update()
        {
            if(rect==null)return;float amount=selected?1:0;
            Vector2 target=rest+Vector2.left*amount*24;
            if(VisualPreferences.ReducedEffects){rect.anchoredPosition=target;rect.localRotation=Quaternion.identity;return;}
            rect.anchoredPosition=Vector2.Lerp(rect.anchoredPosition,target,Time.unscaledDeltaTime*15);
            rect.localRotation=Quaternion.Euler(0,0,selected?-2:0);
        }
    }
}
