using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace RhythmWare.UI
{
    public static class CandyUI
    {
        public static void Backdrop(Transform root)
        {
            var graphic=CyberUI.Rect(root,"CandyScreenBackdrop",Vector2.zero,Vector2.one).gameObject.AddComponent<CandyMenuGraphic>();graphic.Subtle=true;
        }
        public static TextMeshProUGUI Title(Transform root,string text,Vector2 min,Vector2 max,float size=38)
        {
            var t=CyberUI.Label(root,text,min,max,size,CandyMenuGraphic.Cream);t.fontStyle=FontStyles.Bold;
            t.rectTransform.localRotation=Quaternion.Euler(0,0,1.2f);
            CyberUI.Box(root,"TitleUnderline",new Vector2(min.x,min.y-.004f),new Vector2(Mathf.Min(max.x,min.x+.25f),min.y+.001f),CyberUI.Magenta);return t;
        }
        public static void Entrance(GameObject panel) { if(panel.GetComponent<CandyPanelEntrance>()==null)panel.AddComponent<CandyPanelEntrance>(); }
    }

    public sealed class CandyPanelEntrance : MonoBehaviour
    {
        float age;
        void OnEnable() { age=0;Render(); }
        void Update() { if(age>=.25f)return;age+=Time.unscaledDeltaTime;Render(); }
        void Render()
        {
            float p=VisualPreferences.ReducedEffects?1:Mathf.SmoothStep(0,1,Mathf.Clamp01(age/.25f));
            transform.localScale=Vector3.one*Mathf.Lerp(.975f,1,p);transform.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(-.65f,0,p));
        }
        void OnDisable() { transform.localScale=Vector3.one;transform.localRotation=Quaternion.identity; }
    }

    public sealed class CandyButtonFeedback : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler
    {
        RectTransform caption;
        bool hovered,selected;
        void Start() { var label=GetComponentInChildren<TextMeshProUGUI>();if(label!=null)caption=label.rectTransform; }
        public void OnPointerEnter(PointerEventData e) { hovered=true; }
        public void OnPointerExit(PointerEventData e) { hovered=false; }
        public void OnSelect(BaseEventData e) { selected=true; }
        public void OnDeselect(BaseEventData e) { selected=false; }
        void Update()
        {
            if(caption==null||GetComponent<CandyMenuButton>()!=null)return;
            float scale=(hovered||selected)&&GetComponent<Button>().interactable?1.035f:1;
            if(VisualPreferences.ReducedEffects){caption.localScale=Vector3.one;return;}
            caption.localScale=Vector3.Lerp(caption.localScale,Vector3.one*scale,Time.unscaledDeltaTime*18);
        }
        void OnDisable() { hovered=selected=false;if(caption!=null)caption.localScale=Vector3.one; }
    }
}
