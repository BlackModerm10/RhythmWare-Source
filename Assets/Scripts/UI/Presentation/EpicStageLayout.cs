using UnityEngine;
using RhythmWare.Core;

namespace RhythmWare.UI
{
    // A flat timing plane: parallel columns, fixed visual units per second.
    // Artwork, actors, notes, guide curves and holds remain separate layers.
    public static class EpicStageLayout
    {
        public const float ReferenceWidth=1920, ReferenceHeight=1080;
        public static readonly Vector2 OriginPixel=new Vector2(960,365);
        public static readonly Vector2 BossPixel=new Vector2(960,179);
        public const float HitLineY=915, RoadEndY=980;
        public const float NoteSizePixels=112, TrackHalfWidth=730;
        static readonly Vector2[] receptors={new Vector2(380,HitLineY),new Vector2(645,HitLineY),
            new Vector2(1275,HitLineY),new Vector2(1540,HitLineY),new Vector2(960,HitLineY)};
        public static readonly float[] LaneEdges={-1,-447.5f/730,-157.5f/730,157.5f/730,447.5f/730,1};
        public static Vector2 ReceptorSize(int lane)=>lane==LaneLayout.Center?new Vector2(310,112):new Vector2(190,132);
        static Sprite artwork;
        public static Sprite Artwork => artwork!=null?artwork:(artwork=Resources.Load<Sprite>("Presentation/EpicStage"));
        public static Rect WorldRect(Camera camera)
        {
            float height=camera.orthographicSize*2, width=height*camera.aspect;
            float scale=Mathf.Min(height/ReferenceHeight,width/ReferenceWidth);
            float artWidth=ReferenceWidth*scale, artHeight=ReferenceHeight*scale;
            return new Rect(camera.transform.position.x-artWidth*.5f,
                camera.transform.position.y-artHeight*.5f,artWidth,artHeight);
        }
        public static Vector3 PixelToWorld(Vector2 pixel,Camera camera=null)
        {
            if(camera==null)camera=Camera.main;
            if(camera==null)return Vector3.zero;
            Rect rect=WorldRect(camera);
            return new Vector3(rect.xMin+pixel.x/ReferenceWidth*rect.width,rect.yMax-pixel.y/ReferenceHeight*rect.height,0);
        }
        public static Vector2 ReceptorPixel(int lane) => receptors[Mathf.Clamp(lane,0,LaneLayout.Count-1)];
        public static Vector3 ReceptorPosition(int lane,Camera camera=null) => PixelToWorld(ReceptorPixel(lane),camera);
        public static Vector3 ReceptorScale(int lane,Camera camera=null)
        {
            if(camera==null)camera=Camera.main;
            float unit=WorldRect(camera).height/ReferenceHeight;
            // The visible ring spans 94% of its sprite bounds.
            var size=ReceptorSize(lane);return new Vector3(size.x*unit/(lane==LaneLayout.Center?.96f:.94f),size.y*unit/.94f,1);
        }
        public static float ProjectProgress(float progress)
        {
            float t=Mathf.Max(0,progress);
            // Keep moving beyond the hit line while a late hit is still valid.
            return t;
        }
        public static float UnprojectProgress(float projected)
        { return Mathf.Max(0,projected); }
        // Two separated approaches in each half, with gold SPACE on the axis.
        public static float ApproachSlot(int lane)
        { return (ReceptorPixel(lane).x-OriginPixel.x)/730; }
        public static float RoadHalfWidth(float pixelY) => TrackHalfWidth;
        public static Color LaneColor(int lane) => lane==LaneLayout.Center?new Color(1f,.83f,.34f):LaneLayout.IsLeft(lane)?new Color(.43f,1,.80f):new Color(1,.38f,.77f);
        public static Vector2 RoutePixel(int lane,float projected)
        {
            float t=Mathf.Max(0,projected);
            Vector2 end=ReceptorPixel(lane),start=new Vector2(end.x,OriginPixel.y);
            return Vector2.LerpUnclamped(start,end,t);
        }
    }
}
