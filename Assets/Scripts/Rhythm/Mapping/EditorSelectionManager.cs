using System;
using System.Collections.Generic;
using System.Linq;

namespace RhythmWare.Rhythm
{
    public sealed class EditorSelectionManager
    {
        readonly HashSet<NoteData> selected=new HashSet<NoteData>();
        readonly List<EditorNoteState> clipboard=new List<EditorNoteState>();
        public IEnumerable<NoteData> Notes => selected;
        public int Count => selected.Count;
        public event Action Changed;
        public bool Contains(NoteData note) => selected.Contains(note);
        public void Clear() { selected.Clear();Changed?.Invoke(); }
        public void Select(NoteData note,bool additive=false)
        {
            if(!additive)selected.Clear();
            if(note!=null) {if(additive&&selected.Contains(note))selected.Remove(note);else selected.Add(note);}
            Changed?.Invoke();
        }
        public void SelectMany(IEnumerable<NoteData> notes,bool additive=false)
        { if(!additive)selected.Clear();foreach(var n in notes)selected.Add(n);Changed?.Invoke(); }
        public void Reconcile(ChartData chart) { var present=new HashSet<NoteData>(chart.notes);selected.RemoveWhere(n=>!present.Contains(n));Changed?.Invoke(); }
        public void Delete(EditorCommandHistory history)
        { history.Execute(selected.Select(n=>new EditorNoteMutation(n,EditorNoteState.Capture(n),null)).ToList());Clear(); }
        public void Copy()
        {
            clipboard.Clear();if(selected.Count==0)return;double origin=selected.Min(n=>n.timestamp);
            foreach(var n in selected){var state=EditorNoteState.Capture(n);state.time-=origin;clipboard.Add(state);}
        }
        public void Paste(EditorCommandHistory history,double atTime)
        {
            if(clipboard.Count==0)return;var added=new List<NoteData>();var changes=new List<EditorNoteMutation>();
            foreach(var copied in clipboard){var state=copied;state.time+=atTime;var note=new NoteData();changes.Add(new EditorNoteMutation(note,null,state));added.Add(note);}
            history.Execute(changes);SelectMany(added);
        }
    }
}
