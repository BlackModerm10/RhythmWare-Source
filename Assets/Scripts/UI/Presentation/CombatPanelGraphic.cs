using UnityEngine;
using UnityEngine.UI;

namespace RhythmWare.UI
{
    // Resolution-independent inked plates and bounded continuous meter fills.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CombatPanelGraphic : MaskableGraphic
    {
        public bool Meter { get; private set; }
        public Color Accent { get; private set; }
        public void SetAccent(Color accent){if(Accent==accent)return;Accent=accent;SetVerticesDirty();}
        float value=1;
        static readonly Color Ink=new Color(.028f,.018f,.065f);
        public float Fill { get=>value; set{float next=Mathf.Clamp01(value);if(this.value==next)return;this.value=next;SetVerticesDirty();} }
        public static CombatPanelGraphic Create(Transform parent,string name,Vector2 min,Vector2 max,Color accent,bool meter=false)
        {
            var graphic=CyberUI.Rect(parent,name,min,max).gameObject.AddComponent<CombatPanelGraphic>();
            graphic.Accent=accent;graphic.Meter=meter;graphic.raycastTarget=false;return graphic;
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();Rect r=rectTransform.rect;
            float unit=Mathf.Min(r.height/60,1.25f),border=Meter?2*unit:3*unit;
            Bevel(vh,new Rect(r.x+2*unit,r.y-5*unit,r.width,r.height),Ink);
            Bevel(vh,r,Accent);Rect inset=Inset(r,border);Bevel(vh,inset,Ink);
            Rect face=Inset(inset,border);Bevel(vh,face,new Color(.075f,.055f,.16f));
            if(Meter&&value>0)
            {
                float w=face.width*value,cut=Mathf.Min(10*unit,w*.35f);
                Polygon(vh,new[]{new Vector2(face.x,face.y),new Vector2(face.x+w-cut,face.y),new Vector2(face.x+w,face.yMax),new Vector2(face.x+Mathf.Min(cut,w*.5f),face.yMax)},Accent);
            }
            if(!Meter)
            {
                var h=new Rect(face.x+face.height*.18f,face.yMax-2*unit,Mathf.Max(0,face.width-face.height*.36f),unit);
                Bevel(vh,h,new Color(1,1,1,.17f));
            }
        }
        static Rect Inset(Rect r,float n)=>new Rect(r.x+n,r.y+n,Mathf.Max(0,r.width-2*n),Mathf.Max(0,r.height-2*n));
        static void Bevel(VertexHelper vh,Rect r,Color c)
        {
            float cut=Mathf.Min(r.height*.20f,r.width*.10f);
            Polygon(vh,new[]{new Vector2(r.x,r.y),new Vector2(r.xMax-cut,r.y),new Vector2(r.xMax,r.yMax),new Vector2(r.x+cut,r.yMax)},c);
        }
        static void Polygon(VertexHelper vh,Vector2[] p,Color c)
        {
            int first=vh.currentVertCount;for(int i=0;i<p.Length;i++)vh.AddVert(p[i],c,Vector2.zero);
            for(int i=1;i<p.Length-1;i++)vh.AddTriangle(first,first+i,first+i+1);
        }
    }
}
