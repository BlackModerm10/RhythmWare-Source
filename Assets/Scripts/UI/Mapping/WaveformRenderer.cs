using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmWare.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WaveformRenderer : MaskableGraphic
    {
        sealed class ClipAnalysis{public float[] peaks;public Vector2[] attacks;}
        static readonly Dictionary<AudioClip,ClipAnalysis> cache=new Dictionary<AudioClip,ClipAnalysis>();
        float[] peaks;
        Vector2[] attacks;
        public bool ShowAttacks { get; set; }=true;
        public int AttackCount=>attacks?.Length??0;
        double duration, start, span=12;
        Coroutine analysis;
        AudioClip requestedClip;
        public bool HasSamples => peaks!=null;
        public void SetClip(AudioClip clip)
        {
            requestedClip=clip;
            if(analysis!=null)StopCoroutine(analysis);analysis=null;peaks=null;attacks=null;
            if(clip==null){SetVerticesDirty();return;}duration=clip.length;
            if(cache.TryGetValue(clip,out var saved)){peaks=saved.peaks;attacks=saved.attacks;SetVerticesDirty();}
            else if(isActiveAndEnabled)analysis=StartCoroutine(Analyze(clip));
        }
        protected override void OnEnable()
        {
            base.OnEnable();
            if(requestedClip!=null&&!HasSamples)SetClip(requestedClip);
        }
        protected override void OnDisable()
        {
            if(analysis!=null)StopCoroutine(analysis);analysis=null;
            base.OnDisable();
        }
        IEnumerator Analyze(AudioClip clip)
        {
            var result=new float[4096];int channels=clip.channels;const int frames=4096;
            float[] buffer=new float[frames*channels];int iterations=0;
            var energy=new List<float>();int hop=Mathf.Max(1,Mathf.RoundToInt(clip.frequency*.01f)),sampleCount=0;double power=0;
            for(int offset=0;offset<clip.samples;offset+=frames)
            {
                if(!clip.GetData(buffer,offset)){Debug.LogWarning("Waveform: el clip no permite leer PCM.");yield break;}
                int count=Mathf.Min(frames,clip.samples-offset);
                for(int f=0;f<count;f++)
                {
                    int bin=Mathf.Min(result.Length-1,(int)((long)(offset+f)*result.Length/clip.samples));
                    double framePower=0;for(int c=0;c<channels;c++){float v=buffer[f*channels+c];result[bin]=Mathf.Max(result[bin],Mathf.Abs(v));framePower+=v*v;}
                    power+=framePower/channels;if(++sampleCount==hop){energy.Add(Mathf.Sqrt((float)(power/hop)));power=0;sampleCount=0;}
                }
                if(++iterations%16==0)yield return null;
            }
            var candidates=new List<Vector2>();float maximum=0;var rise=new float[energy.Count];
            for(int i=4;i<energy.Count;i++){float baseline=(energy[i-1]+energy[i-2]+energy[i-3]+energy[i-4])*.25f;rise[i]=Mathf.Max(0,energy[i]-baseline);maximum=Mathf.Max(maximum,rise[i]);}
            for(int i=4;i<energy.Count-3;i++)
            {
                if(maximum<=.00001f||rise[i]<maximum*.20f||rise[i]<rise[i-1]||rise[i]<=rise[i+1])continue;
                var candidate=new Vector2((i+.5f)*hop/clip.frequency,rise[i]/maximum);
                int last=candidates.Count-1;if(last>=0&&candidate.x-candidates[last].x<.12f){if(candidate.y>candidates[last].y)candidates[last]=candidate;}else candidates.Add(candidate);
            }
            attacks=candidates.ToArray();if(cache.Count>=12)cache.Clear();cache[clip]=new ClipAnalysis{peaks=result,attacks=attacks};peaks=result;analysis=null;SetVerticesDirty();
        }
        public void SetWindow(double windowStart,double windowSpan) { if(start==windowStart&&span==windowSpan)return;start=windowStart;span=windowSpan;SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(peaks==null||duration<=0)return;Rect r=rectTransform.rect;
            int columns=Mathf.Min(1200,Mathf.Max(1,(int)r.width));
            for(int x=0;x<columns;x++)
            {
                int bin=Mathf.Clamp((int)((start+span*x/columns)/duration*peaks.Length),0,peaks.Length-1);
                float height=peaks[bin]*r.height*.45f;float px=r.xMin+x*r.width/columns;
                MeshQuad(vh,new Rect(px,r.center.y-height,Mathf.Max(1,r.width/columns),height*2),new Color(.1f,.65f,.75f,.7f));
            }
            if(ShowAttacks&&attacks!=null)foreach(var attack in attacks)
            {
                if(attack.x<start||attack.x>start+span)continue;
                float px=r.xMin+(float)((attack.x-start)/span)*r.width,h=r.height*(.35f+.60f*attack.y);
                MeshQuad(vh,new Rect(px-1,r.yMin,2,h),new Color(1,.80f,.25f,.55f+.45f*attack.y));
                MeshQuad(vh,new Rect(px-3,r.yMin+h-3,6,3),new Color(1,.90f,.46f));
            }
        }
        internal static void MeshQuad(VertexHelper vh,Rect r,Color c)
        {
            int i=vh.currentVertCount;
            vh.AddVert(new Vector3(r.xMin,r.yMin),c,Vector2.zero);vh.AddVert(new Vector3(r.xMin,r.yMax),c,Vector2.zero);
            vh.AddVert(new Vector3(r.xMax,r.yMax),c,Vector2.zero);vh.AddVert(new Vector3(r.xMax,r.yMin),c,Vector2.zero);
            vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
    }
}
