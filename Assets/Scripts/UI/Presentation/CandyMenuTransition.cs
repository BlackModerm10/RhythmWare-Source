using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmWare.UI
{
    public sealed class CandyMenuTransition : MonoBehaviour
    {
        public static bool IsRunning => active!=null&&active.overlay!=null&&active.overlay.activeSelf;
        static CandyMenuTransition active;
        GameObject overlay;
        CandyWipeGraphic wipe;
        TextMeshProUGUI label;
        CanvasGroup interactionGroup;
        bool restoreInteraction;
        float age;
        public void Play(string destination)
        {
            if(VisualPreferences.ReducedEffects){Cancel();return;}
            Cancel();
            active=this;
            interactionGroup=GetComponent<CanvasGroup>();if(interactionGroup==null)interactionGroup=gameObject.AddComponent<CanvasGroup>();
            restoreInteraction=interactionGroup.interactable;interactionGroup.interactable=false;
            if(overlay==null)
            {
                overlay=CyberUI.Rect(transform,"CandyMenuTransition",Vector2.zero,Vector2.one).gameObject;
                wipe=overlay.AddComponent<CandyWipeGraphic>();wipe.raycastTarget=true;
                label=CyberUI.Label(overlay.transform,"",new Vector2(.12f,.40f),new Vector2(.88f,.60f),62,CandyMenuGraphic.Ink);
                label.fontStyle=FontStyles.Bold;label.alignment=TextAlignmentOptions.Center;
            }
            age=0;overlay.SetActive(true);overlay.transform.SetAsLastSibling();label.text=destination;Draw();
        }
        public void Cancel()
        {
            if(overlay!=null)overlay.SetActive(false);
            if(interactionGroup!=null){interactionGroup.interactable=restoreInteraction;interactionGroup=null;}
        }
        void Update() { if(!IsRunning||active!=this)return;age+=Time.unscaledDeltaTime;if(age>=.48f){Cancel();return;}Draw(); }
        void Draw() { wipe.Progress=age/.48f;label.alpha=Mathf.Sin(Mathf.Clamp01((wipe.Progress-.10f)/.80f)*Mathf.PI);label.rectTransform.anchoredPosition=new Vector2((wipe.Progress-.5f)*180,0); }
        void OnDisable() { Cancel(); }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CandyWipeGraphic : MaskableGraphic
    {
        float progress;
        public float Progress { get=>progress;set { progress=value;SetVerticesDirty(); } }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;float center=Mathf.Lerp(-1.6f,2.6f,Mathf.SmoothStep(0,1,progress))*r.width;
            Band(vh,r,center-r.width*.7f,r.width*1.25f,CyberUI.Magenta);
            Band(vh,r,center-r.width*.52f,r.width*.89f,CandyMenuGraphic.Mint);
            Band(vh,r,center-r.width*.25f,r.width*.12f,CandyMenuGraphic.Cream);
        }
        static void Band(VertexHelper vh,Rect r,float left,float width,Color color)
        {
            int n=vh.currentVertCount;float slant=r.width*.35f;
            vh.AddVert(new Vector3(r.xMin+left,r.yMin),color,Vector2.zero);vh.AddVert(new Vector3(r.xMin+left+width,r.yMin),color,Vector2.zero);
            vh.AddVert(new Vector3(r.xMin+left+width+slant,r.yMax),color,Vector2.zero);vh.AddVert(new Vector3(r.xMin+left+slant,r.yMax),color,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
    }
}
