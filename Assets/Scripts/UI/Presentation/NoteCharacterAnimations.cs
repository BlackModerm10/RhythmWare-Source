using System;
using System.Collections.Generic;
using UnityEngine;
using RhythmWare.Malware;

namespace RhythmWare.UI
{
    // PNG sequences keep the artist's full canvas and face screen-bottom on every lane.
    // CSP exports did not include cel exposure times: preserve the complete timeline
    // duration at 30 fps and space the exported images evenly within that duration.
    public static class NoteCharacterAnimations
    {
        [Serializable] public sealed class Definition
        {
            public string id, resourceFolder;
            public int timelineFrames, disguisedFrameCount, revealStartFrame, revealedStillFrame;
        }

        [Serializable] sealed class Settings
        {
            public float sourceFramesPerSecond = 30;
            public Definition[] sequences;
        }

        public sealed class Clip
        {
            public Definition Definition { get; }
            public Sprite[] Frames { get; }
            public double Duration { get; }
            public double FrameDuration => Duration / Frames.Length;

            public Clip(Definition definition, Sprite[] frames, double duration)
            { Definition = definition; Frames = frames; Duration = duration; }

            public int FrameIndex(double elapsed, double revealAt = -1, bool reducedEffects = false)
            {
                if (Definition.disguisedFrameCount > 0 && revealAt >= 0)
                {
                    if (elapsed < revealAt)
                        return reducedEffects ? 0 : Step(elapsed, Mathf.Min(Definition.disguisedFrameCount, Frames.Length));
                    if (reducedEffects) return Mathf.Clamp(Definition.revealedStillFrame, 0, Frames.Length - 1);
                    return (Mathf.Clamp(Definition.revealStartFrame, 0, Frames.Length - 1) + Step(elapsed - revealAt, Frames.Length)) % Frames.Length;
                }
                return reducedEffects ? 0 : Step(elapsed, Frames.Length);
            }

            int Step(double elapsed, int count) => (int)(Math.Floor(Math.Max(0, elapsed) / FrameDuration) % count);
        }

        static readonly Dictionary<string, Clip> clips = new Dictionary<string, Clip>();
        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { clips.Clear(); loaded = false; }

        public static Clip For(MalwareData malware)
        {
            string id = malware == null ? "Normal" : malware is SpywareData ? "Spyware" :
                malware is AdwareData ? "Adware" : malware is TrojanData ? "Trojan" : null;
            if (id == null) return null;
            Load();
            return clips.TryGetValue(id, out var clip) ? clip : null;
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            var source = Resources.Load<TextAsset>("NoteCharacters/AnimationSettings");
            if (source == null) return;
            var settings = JsonUtility.FromJson<Settings>(source.text);
            if (settings?.sequences == null || settings.sourceFramesPerSecond <= 0) return;
            foreach (var definition in settings.sequences)
            {
                if (string.IsNullOrEmpty(definition.id) || string.IsNullOrEmpty(definition.resourceFolder) || definition.timelineFrames <= 0) continue;
                var frames = Resources.LoadAll<Sprite>(definition.resourceFolder);
                Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
                if (frames.Length == 0) continue;
                clips[definition.id] = new Clip(definition, frames, definition.timelineFrames / (double)settings.sourceFramesPerSecond);
            }
        }
    }
}
