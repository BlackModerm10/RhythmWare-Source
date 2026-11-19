using System;
using System.Collections.Generic;

namespace RhythmWare.Rhythm
{
    // Temporal buckets, rebuilt after a document edit, never per frame. Holds
    // occupy each intersected bucket so heads outside the viewport still render.
    public sealed class EditorNoteIndex
    {
        readonly Dictionary<int,List<NoteData>> buckets=new Dictionary<int,List<NoteData>>();
        public void Rebuild(ChartData chart)
        {
            buckets.Clear();foreach(var n in chart.notes)
            {
                int first=(int)Math.Floor(n.timestamp/2),last=(int)Math.Floor(n.endTimestamp/2);
                for(int i=first;i<=last;i++){if(!buckets.TryGetValue(i,out var list))buckets[i]=list=new List<NoteData>();list.Add(n);}
            }
        }
        public void Query(double start,double end,HashSet<NoteData> result)
        {
            result.Clear();for(int i=(int)Math.Floor(start/2);i<=(int)Math.Floor(end/2);i++)
                if(buckets.TryGetValue(i,out var list))foreach(var n in list)if(n.timestamp<=end&&n.endTimestamp>=start)result.Add(n);
        }
    }
}
