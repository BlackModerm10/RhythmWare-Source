using RhythmWare.Core;
using System;
using UnityEngine;
using RhythmWare.Malware;

namespace RhythmWare.Rhythm
{
    [Serializable]
    public class NoteData
    {
        [Tooltip("Exact song timestamp in seconds when the note reaches the hit zone.")]
        public double timestamp;
        [System.NonSerialized] public bool trainingObserve;

        [Tooltip("Target lane index (0-1: Left, 2-3: Right, 4: SPACE).")]
        public int laneIndex;

        [Tooltip("Malware type ScriptableObject for behavior and visual representation.")]
        public MalwareData malwareData;

        [Tooltip("Custom speed modifier for this note.")]
        public float speedMultiplier = 1.0f;

        [Tooltip("Hold duration in seconds (0 = normal tap note, > 0 = long note/slider).")]
        public double duration = 0.0;

        public bool isHoldNote => duration > 0.05;
        public double endTimestamp => timestamp + duration;
    }
}
