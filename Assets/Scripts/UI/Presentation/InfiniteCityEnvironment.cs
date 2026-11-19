using UnityEngine;

namespace RhythmWare.UI
{
    // Buildings repeatedly approach from the horizon and leave through the
    // edges. All recycling happens in the distance or outside the view.
    public sealed class InfiniteCityEnvironment : MonoBehaviour
    {
        EnvironmentDrawing buildings,street;
        public const float Period=24;
        public double RenderedTime { get; private set; }
        public bool Visible { get; private set; }
        public int BuildingVertexCount=>buildings.VertexCount;
        static readonly Color Ink=new Color(.025f,.012f,.065f);
        public void Initialize()
        {buildings=new EnvironmentDrawing(transform,"MovingComicCity",-94);street=new EnvironmentDrawing(transform,"AdvancingStreetLights",-72);}
        public void SetVisible(bool visible){Visible=visible;buildings.Visible=street.Visible=visible;}
        public static Vector2 BuildingPosition(int side,int index,double clock,bool fast=false)
        {
            float p=Mathf.Repeat((float)(clock/(fast?20:Period))+index/9f,1),scale=.035f+.965f*p*p;
            return new Vector2(960+side*(150+1450*scale),365+780*scale);
        }
        public void Render(double clock,bool fast,bool reduced)
        {
            RenderedTime=clock;if(!Visible||Camera.main==null)return;
            buildings.Begin();street.Begin();
            // Keep the illustrated city and its existing parallax; no procedural towers.
            if(!reduced)for(int i=0;i<10;i++)
            {
                float p=Mathf.Repeat((float)(clock/4)+i/10f,1),y=Mathf.Lerp(EpicStageLayout.OriginPixel.y,EpicStageLayout.RoadEndY,p),half=EpicStageLayout.RoadHalfWidth(y);
                float a=Mathf.SmoothStep(0,1,p/.2f)*.34f;
                for(int side=-1;side<=1;side+=2)street.Line(new Vector2(960+side*half*.89f,y),new Vector2(960+side*half*.97f,y),1+5*p,Tint(EpicStageLayout.LaneColor(side<0?0:2),a));
            }
            buildings.End();street.End();
        }
        static Color Tint(Color c,float a){c.a=a;return c;}
        void OnDestroy(){buildings?.Dispose();street?.Dispose();}
    }
}
