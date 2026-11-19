using RhythmWare.Core;
using UnityEngine;
using RhythmWare.Rhythm;

namespace RhythmWare.UI
{
    // Static, low-contrast guidance over the original PNG. Lives on the board,
    // so menus and inactive gameplay never retain world-space lane markers.
    public sealed class LaneReadabilityGuide : MonoBehaviour
    {
        readonly LineRenderer[] routes=new LineRenderer[LaneLayout.Count];
        public void Initialize(Transform[] receptors)
        {
            for(int lane=0;lane<LaneLayout.Count;lane++)
            {
                Color color=EpicStageLayout.LaneColor(lane);color.a=.20f;
                routes[lane]=NetworkVisualAssets.Line(transform,"Approach_"+CyberUI.LaneNames[lane],new Vector3[65],color,.02f,-70);
                var shadow=new GameObject("TerminalInk");shadow.transform.SetParent(receptors[lane],false);shadow.transform.localScale=Vector3.one*1.08f;
                var ink=shadow.AddComponent<SpriteRenderer>();ink.sprite=NetworkVisualAssets.Shape(lane==LaneLayout.Center?17:15);ink.color=new Color(.025f,.018f,.06f);ink.sortingOrder=5;
                var accent=new GameObject("RouteColorFrame");accent.transform.SetParent(receptors[lane],false);
                var sr=accent.AddComponent<SpriteRenderer>();sr.sprite=NetworkVisualAssets.Shape(lane==LaneLayout.Center?16:14);
                sr.color=new Color(color.r,color.g,color.b,1f);sr.sortingOrder=7;
            }
        }
        public void RefreshLayout()
        {
            var lanes=LaneController.Instance;if(lanes==null||Camera.main==null)return;
            float unit=EpicStageLayout.WorldRect(Camera.main).height/EpicStageLayout.ReferenceHeight;
            for(int lane=0;lane<LaneLayout.Count;lane++)
            {
                var line=routes[lane];if(line==null)continue;
                line.startWidth=1*unit;line.endWidth=2*unit;
                for(int i=0;i<line.positionCount;i++)line.SetPosition(i,EpicStageLayout.PixelToWorld(EpicStageLayout.RoutePixel(lane,i/(float)(line.positionCount-1))));
            }
        }
    }
}
