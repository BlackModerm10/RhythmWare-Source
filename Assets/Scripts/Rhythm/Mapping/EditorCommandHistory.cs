using System;
using System.Collections.Generic;
using RhythmWare.Malware;

namespace RhythmWare.Rhythm
{
    public struct EditorNoteState
    {
        public double time, duration;
        public int lane;
        public float speed;
        public MalwareData malware;
        public static EditorNoteState Capture(NoteData n) => new EditorNoteState { time=n.timestamp,duration=n.duration,lane=n.laneIndex,speed=n.speedMultiplier,malware=n.malwareData };
        public void Apply(NoteData n) { n.timestamp=time;n.duration=duration;n.laneIndex=lane;n.speedMultiplier=speed;n.malwareData=malware; }
    }
    public sealed class EditorNoteMutation
    {
        public NoteData note;
        public EditorNoteState? before, after;
        public EditorNoteMutation(NoteData target, EditorNoteState? oldState, EditorNoteState? newState) { note=target;before=oldState;after=newState; }
    }
    public sealed class EditorCommandHistory
    {
        sealed class Command { public List<EditorNoteMutation> changes; public long beforeId, afterId; }
        readonly List<Command> commands=new List<Command>();
        readonly ChartData chart;
        readonly Action changed;
        int position;
        long stateId, serial, savedId;
        public bool Dirty => stateId!=savedId;
        public bool CanUndo => position>0;
        public bool CanRedo => position<commands.Count;
        public EditorCommandHistory(ChartData model, Action onChanged) { chart=model;changed=onChanged; }
        public void MarkSaved() { savedId=stateId; }
        public void Execute(List<EditorNoteMutation> changes)
        {
            if(changes.Count==0)return;
            if(position<commands.Count)commands.RemoveRange(position,commands.Count-position);
            var command=new Command{changes=changes,beforeId=stateId,afterId=++serial};commands.Add(command);position++;
            Apply(command,true);
        }
        public void Undo() { if(CanUndo)Apply(commands[--position],false); }
        public void Redo() { if(CanRedo)Apply(commands[position++],true); }
        void Apply(Command command,bool forward)
        {
            var existing=new HashSet<NoteData>(chart.notes);
            var removed=new HashSet<NoteData>();
            foreach(var m in command.changes)
            {
                var state=forward?m.after:m.before;
                if(!state.HasValue){removed.Add(m.note);existing.Remove(m.note);}
                else { state.Value.Apply(m.note);removed.Remove(m.note);if(existing.Add(m.note))chart.notes.Add(m.note); }
            }
            if(removed.Count>0)chart.notes.RemoveAll(removed.Contains);
            chart.SortNotes();stateId=forward?command.afterId:command.beforeId;changed?.Invoke();
        }
    }
}
