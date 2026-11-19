using UnityEngine;
using UnityEngine.UI;

namespace RhythmWare.UI
{
    // A single UI mesh: no particles, textures, coroutines or objects per frame.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CandyMenuGraphic : MaskableGraphic
    {
        public static readonly Color Ink=new Color(.095f,.045f,.21f);
        public static readonly Color Pink=new Color(1,.30f,.67f);
        public static readonly Color Mint=new Color(.29f,1,.92f);
        public static readonly Color Cream=new Color(1,.96f,.80f);
        public bool Subtle { get; set; }
        float clock;
        protected override void OnEnable() { base.OnEnable();raycastTarget=false; }
        void Update() { float value=VisualPreferences.ReducedEffects?0:Time.unscaledTime;if(value!=clock){clock=value;SetVerticesDirty();} }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();Rect r=rectTransform.rect;float sx=r.width/1920,sy=r.height/1080;
            Quad(vh,new Vector2(0,0),new Vector2(1920,0),new Vector2(1920,1080),new Vector2(0,1080),Ink,r,sx,sy);
            Quad(vh,new Vector2(1180,0),new Vector2(1920,0),new Vector2(1920,1080),new Vector2(1460,1080),new Color(.24f,.10f,.43f),r,sx,sy);
            Quad(vh,new Vector2(0,820),new Vector2(1080,1080),new Vector2(0,1080),new Vector2(0,1080),new Color(.41f,.14f,.50f),r,sx,sy);
            for(int i=0;i<(Subtle?1:4);i++)Ring(vh,new Vector2(570,540),310+i*48,6,i%2==0?new Color(1,.30f,.67f,.19f):new Color(.29f,1,.92f,.15f),clock*.05f+i,r,sx,sy);
            for(int i=0;i<(Subtle?22:65);i++)
            {
                float x=(i*137+45)%1880,y=(i*271+25)%1020;
                float drift=Mathf.Sin(clock*.6f+i)*12;
                float size=i%5==0?14:4;
                Diamond(vh,new Vector2(x,y+drift),size,i%3==0?Pink:Mint,r,sx,sy);
            }
            for(int i=0;i<(Subtle?0:36);i++)
            {
                float h=12+Mathf.Abs(Mathf.Sin(i*.75f+clock*2))*46;
                float x=86+i*22;
                Quad(vh,new Vector2(x,190),new Vector2(x+10,190),new Vector2(x+10,190+h),new Vector2(x,190+h),i%3==0?Pink:Mint,r,sx,sy);
            }
            // Striped arcade floor, below all interactive text.
            for(int i=0;i<24;i++)
            {
                float x=i*90-20;
                Quad(vh,new Vector2(x,0),new Vector2(x+44,0),new Vector2(x+82,36),new Vector2(x+38,36),i%2==0?Pink:Mint,r,sx,sy);
            }
        }
        static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color,Rect r,float sx,float sy)
        {
            int n=vh.currentVertCount;var v=UIVertex.simpleVert;v.color=color;
            v.position=new Vector3(r.x+a.x*sx,r.y+a.y*sy);vh.AddVert(v);v.position=new Vector3(r.x+b.x*sx,r.y+b.y*sy);vh.AddVert(v);
            v.position=new Vector3(r.x+c.x*sx,r.y+c.y*sy);vh.AddVert(v);v.position=new Vector3(r.x+d.x*sx,r.y+d.y*sy);vh.AddVert(v);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
        static void Diamond(VertexHelper vh,Vector2 p,float size,Color c,Rect r,float sx,float sy)
        { Quad(vh,p+Vector2.left*size,p+Vector2.down*size,p+Vector2.right*size,p+Vector2.up*size,c,r,sx,sy); }
        static void Ring(VertexHelper vh,Vector2 p,float radius,float width,Color c,float phase,Rect r,float sx,float sy)
        {
            for(int i=0;i<80;i++)
            {
                if(i%20>16)continue;float a=i*Mathf.PI/40+phase,b=(i+1)*Mathf.PI/40+phase;
                var x=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var y=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                Quad(vh,p+x*radius,p+y*radius,p+y*(radius+width),p+x*(radius+width),c,r,sx,sy);
            }
        }
    }
}
