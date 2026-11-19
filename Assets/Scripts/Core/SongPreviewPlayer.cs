using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmWare.Core
{
    // Preview has its own source and request generation; never touches the DSP
    // gameplay clock and never overlaps two songs.
    public sealed class SongPreviewPlayer : MonoBehaviour
    {
        AudioSource source;
        Coroutine transition;
        int generation;
        readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        public void Initialize() { source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false; source.loop = true; }
        public void Select(string path, AudioClip clip, Action<AudioClip> ready)
        {
            Stop();
            int token = generation;
            transition = StartCoroutine(Change(path,clip,ready,token));
        }
        IEnumerator Change(string path, AudioClip clip, Action<AudioClip> ready, int token)
        {
            float original = source.volume;
            for(float t=0;t<.12f;t+=Time.unscaledDeltaTime) { source.volume=original*(1-t/.12f); yield return null; }
            source.Stop();
            if (clip == null && !string.IsNullOrEmpty(path)) cache.TryGetValue(path,out clip);
            if (clip == null) yield return AudioManager.Instance.LoadAudioFile(path,c=>clip=c);
            if(token != generation) yield break;
            if(clip != null)
            {
                if(!string.IsNullOrEmpty(path)) cache[path]=clip;
                AudioManager.Instance.StopMenuBGM();
                source.clip=clip; source.time=Mathf.Min(clip.length*.25f,Mathf.Max(0,clip.length-1)); source.Play();
            }
            ready?.Invoke(clip);
            for(float t=0;t<.18f;t+=Time.unscaledDeltaTime) { source.volume=GameSettings.MasterVolume*GameSettings.MusicVolume*(t/.18f);yield return null; }
            source.volume=GameSettings.MasterVolume*GameSettings.MusicVolume;
            transition=null;
        }
        public void Stop()
        {
            generation++; if(transition!=null)StopCoroutine(transition); transition=null;
            // Retain the source during selection changes so Change can fade it.
        }
        public void Silence() { Stop(); if(source!=null) source.Stop(); }
        public void Toggle() { if(source==null||source.clip==null)return; if(source.isPlaying)source.Pause();else source.UnPause(); }
    }
}
