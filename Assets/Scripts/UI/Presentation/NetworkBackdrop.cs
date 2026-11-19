using UnityEngine;
using RhythmWare.Core;
using RhythmWare.Rhythm;

namespace RhythmWare.UI
{
    public sealed class NetworkBackdrop : MonoBehaviour
    {
        readonly SpriteRenderer[] nodes=new SpriteRenderer[20];
        void Start()
        {
            for(int i=-12;i<=12;i++)NetworkVisualAssets.Line(transform,"GridX",new[]{new Vector3(i,-8,0),new Vector3(i,8,0)},new Color(.12f,.25f,.35f,.14f),.012f,-80);
            for(int i=-8;i<=8;i++)NetworkVisualAssets.Line(transform,"GridY",new[]{new Vector3(-12,i,0),new Vector3(12,i,0)},new Color(.12f,.25f,.35f,.14f),.012f,-80);
            for(int i=0;i<nodes.Length;i++)
            {
                var go=new GameObject("NetworkNode");go.transform.SetParent(transform,false);go.transform.position=new Vector3(Mathf.Sin(i*2.399f)*7,Mathf.Cos(i*2.399f)*4,0);
                var sr=go.AddComponent<SpriteRenderer>();sr.sprite=NetworkVisualAssets.Shape(10);sr.sortingOrder=-79;sr.transform.localScale=Vector3.one*.09f;nodes[i]=sr;
                if(i>0)NetworkVisualAssets.Line(transform,"Edge",new[]{nodes[i-1].transform.position,go.transform.position},new Color(.09f,.25f,.32f,.09f),.009f,-81);
            }
        }
        void Update()
        {
            var chart=ChartManager.Instance?.SelectedChart;double t=AudioManager.Instance!=null?AudioManager.Instance.SongTime:Time.time;
            float beat=chart!=null&&chart.bpm>0?(float)(t*chart.bpm/60)%1:0;
            for(int i=0;i<nodes.Length;i++)if(nodes[i]!=null)nodes[i].color=new Color(.2f,.75f,.85f,.18f+.09f*(1-beat));
        }
    }
}
