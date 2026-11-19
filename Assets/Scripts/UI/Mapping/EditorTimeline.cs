using RhythmWare.Core;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using RhythmWare.Rhythm;

namespace RhythmWare.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class EditorTimeline : MaskableGraphic, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        ChartEditorManager editor;
        WaveformRenderer waveform;
        readonly HashSet<NoteData> visible=new HashSet<NoteData>();
        readonly Dictionary<NoteData,EditorNoteState> originals=new Dictionary<NoteData,EditorNoteState>();
        readonly TMPro.TextMeshProUGUI[] measures=new TMPro.TextMeshProUGUI[16];
        Vector2 down, current;
        NoteData target;
        bool dragging, resizing, box, additive, scrub;
        double start, span=12;
        public double WindowStart => start;
        public double WindowSpan => span;
        public int VisibleCount => visible.Count;
        public int LastVertexCount { get; private set; }
        public bool ShowAudioAttacks { get=>waveform.ShowAttacks;set{waveform.ShowAttacks=value;waveform.SetAllDirty();} }
        public void Initialize(ChartEditorManager manager)
        {
            editor=manager;raycastTarget=true;
            var waveRect=CyberUI.Rect(transform,"PCM Waveform",new Vector2(0,.78f),Vector2.one);
            waveform=waveRect.gameObject.AddComponent<WaveformRenderer>();waveform.raycastTarget=false;
            waveRect.SetAsFirstSibling();
            for(int i=0;i<measures.Length;i++) { measures[i]=CyberUI.Label(transform,"",Vector2.zero,Vector2.zero,14,CyberUI.Muted); measures[i].rectTransform.sizeDelta=new Vector2(75,24); }
            editor.OnDocumentChanged+=DocumentChanged;editor.OnPlaybackTimeUpdated+=Playhead;
            editor.OnChartLoaded+=ChartLoaded;editor.OnAudioChanged+=AudioChanged;editor.Selection.Changed+=SelectionChanged;
            AudioChanged();DocumentChanged();
        }
        protected override void OnDestroy()
        {
            base.OnDestroy();if(editor==null)return;editor.OnDocumentChanged-=DocumentChanged;editor.OnPlaybackTimeUpdated-=Playhead;
            editor.OnChartLoaded-=ChartLoaded;editor.OnAudioChanged-=AudioChanged;editor.Selection.Changed-=SelectionChanged;
        }
        void ChartLoaded(ChartData chart) { AudioChanged();DocumentChanged(); }
        void AudioChanged() { waveform.SetClip(editor.ActiveChart?.audioClip); }
        void DocumentChanged() { RefreshVisible();UpdateMeasures();SetVerticesDirty(); }
        void SelectionChanged() { SetVerticesDirty(); }
        void Playhead(double time)
        {
            if(editor.IsPlaying&&(time>start+span*.9||time<start))SetWindow(Math.Max(0,time-span*.15),span);
            SetVerticesDirty();
        }
        public void Zoom(float factor) { SetWindow(Math.Max(0,editor.PlaybackTime-(editor.PlaybackTime-start)*factor),span*factor); }
        public void SetWindow(double time,double seconds)
        {
            span=Math.Max(.5,Math.Min(120,seconds));start=Math.Max(0,Math.Min(Math.Max(0,editor.Duration-span),time));
            waveform.SetWindow(start,span);RefreshVisible();UpdateMeasures();SetVerticesDirty();
        }
        void RefreshVisible() { editor.NoteIndex.Query(start,start+span,visible); }
        void UpdateMeasures()
        {
            if(editor.ActiveChart==null)return;Rect r=rectTransform.rect;double beat=60.0/Math.Max(1,editor.ActiveChart.bpm),offset=editor.ActiveChart.offsetMs/1000.0;
            int first=(int)Math.Ceiling((start-offset)/(beat*4));
            for(int i=0;i<measures.Length;i++)
            {
                double time=offset+(first+i)*beat*4;bool show=time<=start+span&&time>=start;
                measures[i].gameObject.SetActive(show);if(!show)continue;
                measures[i].rectTransform.anchoredPosition=new Vector2((float)((time-start)/span*r.width)+36,r.height-12);
                measures[i].text=$"M{first+i+1}";
            }
        }
        int Row(Vector2 point) => Mathf.Clamp((int)((rectTransform.rect.yMin+.76f*rectTransform.rect.height-point.y)/(.76f*rectTransform.rect.height/LaneLayout.Count)),0,LaneLayout.Count-1);
        int Lane(Vector2 p)=>CyberUI.LaneOrder[Row(p)];
        double TimeAt(Vector2 p)=>start+(p.x-rectTransform.rect.xMin)/rectTransform.rect.width*span;
        float X(double time)=>rectTransform.rect.xMin+(float)((time-start)/span)*rectTransform.rect.width;
        Rect NoteRect(NoteData note)
        {
            Rect r=rectTransform.rect;float h=r.height*.76f/LaneLayout.Count;int row=Array.IndexOf(CyberUI.LaneOrder,note.laneIndex);
            return new Rect(X(note.timestamp)-6,r.yMin+(LaneLayout.Count-1-row)*h+7,Mathf.Max(12,X(note.endTimestamp)-X(note.timestamp)+12),h-14);
        }
        NoteData Pick(Vector2 point)
        {
            NoteData closest=null;float distance=float.MaxValue;
            foreach(var n in visible)if(NoteRect(n).Contains(point))
            {
                float d=Mathf.Min(Mathf.Abs(X(n.timestamp)-point.x),Mathf.Abs(X(n.endTimestamp)-point.x));
                if(d<distance){closest=n;distance=d;}
            }
            return closest;
        }
        Vector2 Local(PointerEventData e) { RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,e.position,e.pressEventCamera,out var p);return p; }
        public void OnPointerDown(PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left)return;
            down=current=Local(e);dragging=false;scrub=down.y>rectTransform.rect.yMin+rectTransform.rect.height*.76f;
            if(scrub){editor.Seek(TimeAt(down));return;}
            additive=Keyboard.current!=null&&Keyboard.current.shiftKey.isPressed;target=Pick(down);box=target==null;
            if(target!=null)
            {
                if(!editor.Selection.Contains(target)||additive)editor.Selection.Select(target,additive);
                resizing=target.isHoldNote&&Mathf.Abs(X(target.endTimestamp)-down.x)<12;
                originals.Clear();foreach(var n in editor.Selection.Notes)originals[n]=EditorNoteState.Capture(n);
            }
            else if(!additive)editor.Selection.Clear();
        }
        public void OnPointerUp(PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left||dragging||scrub)return;
            if(target==null)editor.AddOrReplaceNote(editor.Quantize(TimeAt(down)),Lane(down));
        }
        public void OnBeginDrag(PointerEventData e) { if(e.button==PointerEventData.InputButton.Left)dragging=true; }
        public void OnDrag(PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left)return;current=Local(e);
            if(scrub){editor.Seek(TimeAt(current));return;}
            if(target!=null&&originals.Count>0)
            {
                var origin=originals[target];
                if(resizing)target.duration=Math.Max(0,editor.Quantize(TimeAt(current))-origin.time);
                else
                {
                    double delta=editor.Quantize(origin.time+TimeAt(current)-TimeAt(down))-origin.time;
                    delta=Math.Max(delta,-originals.Values.Min(s=>s.time));
                    int rowDelta=Row(current)-Row(down),minRow=originals.Values.Min(s=>Array.IndexOf(CyberUI.LaneOrder,s.lane)),maxRow=originals.Values.Max(s=>Array.IndexOf(CyberUI.LaneOrder,s.lane));
                    rowDelta=Mathf.Clamp(rowDelta,-minRow,LaneLayout.Count-1-maxRow);
                    foreach(var entry in originals){entry.Key.timestamp=entry.Value.time+delta;entry.Key.laneIndex=CyberUI.LaneOrder[Array.IndexOf(CyberUI.LaneOrder,entry.Value.lane)+rowDelta];}
                }
            }
            SetVerticesDirty();
        }
        public void OnEndDrag(PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left)return;
            if(scrub)return;
            if(target!=null)editor.History.Execute(originals.Select(p=>new EditorNoteMutation(p.Key,p.Value,EditorNoteState.Capture(p.Key))).ToList());
            else if(box)
            {
                var bounds=Bounds(down,current);editor.Selection.SelectMany(visible.Where(n=>bounds.Overlaps(NoteRect(n))),additive);
            }
            originals.Clear();box=false;SetVerticesDirty();
        }
        public void OnScroll(PointerEventData e)
        {
            if(Keyboard.current!=null&&Keyboard.current.ctrlKey.isPressed)Zoom(e.scrollDelta.y>0?.8f:1.25f);
            else SetWindow(start-e.scrollDelta.y*span*.08,span);
        }
        static Rect Bounds(Vector2 a,Vector2 b)=>Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y));
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(editor?.ActiveChart==null)return;Rect r=rectTransform.rect;
            WaveformRenderer.MeshQuad(vh,r,CyberUI.Background);
            float rowHeight=r.height*.76f/LaneLayout.Count;
            for(int row=0;row<LaneLayout.Count;row++)WaveformRenderer.MeshQuad(vh,new Rect(r.xMin,r.yMin+row*rowHeight,r.width,rowHeight-1),row%2==0?CyberUI.Panel:CyberUI.Background);
            double step=editor.GetSnapTimeSeconds(),beat=60.0/Math.Max(1,editor.ActiveChart.bpm),offset=editor.ActiveChart.offsetMs/1000.0;
            int first=(int)Math.Ceiling((start-offset)/step),count=(int)Math.Min(4096,Math.Ceiling(span/step)+1);
            for(int i=0;i<count;i++)
            {
                double time=offset+(first+i)*step;if(time>start+span)break;
                double beatIndex=(time-offset)/beat;bool onBeat=Math.Abs(beatIndex-Math.Round(beatIndex))<.001;
                bool measure=onBeat&&((int)Math.Round(beatIndex)%4==0);
                if(!onBeat&&r.width*step/span<5)continue;
                WaveformRenderer.MeshQuad(vh,new Rect(X(time),r.yMin,measure?2:1,r.height),measure?new Color(.22f,.5f,.64f,.7f):onBeat?new Color(.18f,.28f,.38f,.8f):new Color(.10f,.15f,.23f,.6f));
            }
            // The set is viewport bounded. During dragging include selected notes whose buckets haven't committed yet.
            int drawn=0;foreach(var n in visible)
            {
                if(++drawn>2500)break;Rect nr=NoteRect(n);nr.xMin=Mathf.Max(nr.xMin,r.xMin);nr.xMax=Mathf.Min(nr.xMax,r.xMax);if(nr.width<=0)continue;
                Color tint=editor.Selection.Contains(n)?Color.white:n.malwareData!=null?Color.Lerp(n.malwareData.nodeColor,Color.white,.35f):CyberUI.Cyan;
                WaveformRenderer.MeshQuad(vh,nr,tint);
                if(n.isHoldNote)WaveformRenderer.MeshQuad(vh,new Rect(nr.xMin+3,nr.yMin+5,Mathf.Max(1,nr.width-6),nr.height-10),CyberUI.Raised);
                WaveformRenderer.MeshQuad(vh,new Rect(nr.xMin,nr.yMin,5,nr.height),tint);
            }
            float cursor=X(editor.PlaybackTime);if(cursor>=r.xMin&&cursor<=r.xMax)WaveformRenderer.MeshQuad(vh,new Rect(cursor,r.yMin,2,r.height),CyberUI.Magenta);
            if(dragging&&box)WaveformRenderer.MeshQuad(vh,Bounds(down,current),new Color(.2f,.8f,1,.22f));
            LastVertexCount=vh.currentVertCount;
        }
    }
}
