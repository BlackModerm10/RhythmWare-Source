using UnityEngine;
using RhythmWare.Core;
using RhythmWare.Rhythm;

namespace RhythmWare.UI
{
    public class BoardVisualGenerator : MonoBehaviour
    {
        readonly SpriteRenderer[] packets=new SpriteRenderer[LaneLayout.Count*3];
        readonly LineRenderer[] paths=new LineRenderer[LaneLayout.Count];
        public void BuildBoardVisuals(Transform parent,Vector3[] hits)
        {
            for(int lane=0;lane<LaneLayout.Count;lane++)
            {
                var points=new Vector3[33];for(int p=0;p<points.Length;p++)points[p]=LaneController.Instance.EvaluateLanePosition(lane,p/32f);
                Color tint=EpicStageLayout.LaneColor(lane);
                if(!LaneController.Instance.UsesEpicStage)
                {
                    NetworkVisualAssets.Line(parent,"ConduitShadow",points,new Color(.06f,.12f,.2f,.8f),.16f,-49);
                    paths[lane]=NetworkVisualAssets.Line(parent,"Route_"+CyberUI.LaneNames[lane],points,new Color(tint.r,tint.g,tint.b,.32f),.024f);
                }
                for(int j=0;j<3;j++)
                {
                    var go=new GameObject("AmbientPacket");go.transform.SetParent(parent,false);var sr=go.AddComponent<SpriteRenderer>();
                    sr.sprite=NetworkVisualAssets.Shape(0);sr.color=new Color(tint.r,tint.g,tint.b,.25f);sr.sortingOrder=-40;go.transform.localScale=Vector3.one*.06f;packets[lane*3+j]=sr;
                }
            }
        }
        void Update()
        {
            if(LaneController.Instance==null)return;
            var chart=ChartManager.Instance?.SelectedChart;double time=AudioManager.Instance!=null?AudioManager.Instance.SongTime:0;
            float beat=chart!=null&&chart.bpm>0?(float)(time*chart.bpm/60)%1:0;
            for(int lane=0;lane<LaneLayout.Count;lane++)
            {
                if(paths[lane]!=null){Color c=paths[lane].startColor;c.a=.18f+.12f*(1-beat);paths[lane].startColor=paths[lane].endColor=c;}
                for(int j=0;j<3;j++)if(packets[lane*3+j]!=null)
                {
                    packets[lane*3+j].enabled=!VisualPreferences.ReducedEffects;
                    packets[lane*3+j].transform.position=LaneController.Instance.EvaluateLanePosition(lane,Mathf.Repeat(Time.time*.12f+j/3f+lane*.04f,1));
                }
            }
        }
    }
}
