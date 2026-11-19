using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace RhythmWare.Core
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource menuBgmSource;

        [Header("Menu Audio Clips")]
        [SerializeField] private AudioClip menuMusicClip;
        [SerializeField] private AudioClip menuHoverClip;
        [SerializeField] private AudioClip menuClickClip;
        [SerializeField] private AudioClip menuBackClip;

        [Header("Gameplay SFX Clips")]
        [SerializeField] private AudioClip hitPerfectClip;
        [SerializeField] private AudioClip hitGoodClip;
        [SerializeField] private AudioClip hitMissClip;

        private AudioClip fallbackHoverClip;
        private AudioClip fallbackClickClip;
        private AudioClip fallbackBackClip;

        private double dspSongStart;
        private double scheduledAudioStart;
        private double pausedLeadIn;
        private bool isPlaying;
        private bool isPaused;
        private double pauseTimeOffset;
        private double offsetAtPause;
        private double inputEpochDsp;
        public bool AcceptsInputDsp(double dsp)=>isPlaying&&!isPaused&&dsp>=inputEpochDsp-.001&&dsp<=AudioSettings.dspTime+.02;
        public double SongTimeAtDsp(double dsp)=>(dsp-dspSongStart)*(musicSource!=null?musicSource.pitch:1.0)+GameSettings.AudioOffsetSec;

        public bool IsPlaying => isPlaying && !isPaused;
        public bool IsMenuMusicPlaying => menuBgmSource != null && menuBgmSource.isPlaying;
        public AudioClip CurrentSongClip => musicSource != null ? musicSource.clip : null;

        /// <summary>
        /// Exact audio time in seconds synced with AudioSettings.dspTime.
        /// Accounts for user latency offset.
        /// </summary>
        public double SongTime
        {
            get
            {
                if (!isPlaying) return 0.0;
                if (isPaused) return pauseTimeOffset+GameSettings.AudioOffsetSec-offsetAtPause;

                double rawTime = (AudioSettings.dspTime - dspSongStart) * (musicSource != null ? musicSource.pitch : 1.0);
                return rawTime + GameSettings.AudioOffsetSec;
            }
        }

        public double Duration => musicSource != null && musicSource.clip != null ? musicSource.clip.length : 0.0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
            if (menuBgmSource == null) menuBgmSource = gameObject.AddComponent<AudioSource>();

            musicSource.playOnAwake = false;
            sfxSource.playOnAwake = false;
            menuBgmSource.playOnAwake = false;
            menuBgmSource.loop = true;

            GenerateFallbackClips();
            UpdateVolumes();
        }

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged += HandleGameStateChanged;
                GameManager.Instance.OnNoteHit += HandleNoteHit;
            }

            ScanAndAutoLoadClips();

            // If we start in MainMenu, play menu music
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.MainMenu)
            {
                PlayMenuBGM();
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged -= HandleGameStateChanged;
                GameManager.Instance.OnNoteHit -= HandleNoteHit;
            }
        }

        private void HandleGameStateChanged(GameState state)
        {
            if (state == GameState.MainMenu)
            {
                StopSong();
                PlayMenuBGM();
            }
            else if (state == GameState.Editor || state == GameState.Playing)
            {
                StopMenuBGM();
            }
        }

        private void GenerateFallbackClips()
        {
            fallbackHoverClip = CreateToneClip("CyberHover", 880f, 0.04f, 0.15f);
            fallbackClickClip = CreateToneClip("CyberClick", 1320f, 0.06f, 0.25f);
            fallbackBackClip = CreateToneClip("CyberBack", 440f, 0.08f, 0.25f);
            if(hitPerfectClip==null)hitPerfectClip=CreateToneClip("CyberPerfect",1760f,.075f,.35f);
            if(hitGoodClip==null)hitGoodClip=CreateToneClip("CyberGood",1174.66f,.065f,.28f);
            if(hitMissClip==null)hitMissClip=CreateToneClip("CyberMiss",146.83f,.13f,.30f);
        }
        void HandleNoteHit(HitRating rating,int lane) { PlayHitRating(rating); }

        private AudioClip CreateToneClip(string name, float freq, float duration, float volume)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1f - ((float)i / samples);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * volume;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void ScanAndAutoLoadClips()
        {
            StartCoroutine(AutoScanFolderRoutine());
        }

        private IEnumerator AutoScanFolderRoutine()
        {
            // 1. Scan Menu Music
            string menuMusicDir = Path.Combine(GameContentPaths.AudioRoot, "Music", "Menu");
            if (Directory.Exists(menuMusicDir) && menuMusicClip == null)
            {
                string[] files = Directory.GetFiles(menuMusicDir);
                foreach (var f in files)
                {
                    string ext = Path.GetExtension(f).ToLower();
                    if (ext == ".mp3" || ext == ".ogg" || ext == ".wav")
                    {
                        yield return LoadAudioFile(f, clip =>
                        {
                            menuMusicClip = clip;
                            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.MainMenu)
                            {
                                PlayMenuBGM();
                            }
                        });
                        break;
                    }
                }
            }

            // 2. Scan UI SFX
            string sfxDir = Path.Combine(GameContentPaths.AudioRoot, "SFX", "UI");
            if (Directory.Exists(sfxDir))
            {
                string[] files = Directory.GetFiles(sfxDir);
                foreach (var f in files)
                {
                    string name = Path.GetFileNameWithoutExtension(f).ToLower();
                    string ext = Path.GetExtension(f).ToLower();
                    if (ext != ".mp3" && ext != ".ogg" && ext != ".wav") continue;

                    if ((name.Contains("hover") || name.Contains("nav") || name.Contains("move")) && menuHoverClip == null)
                    {
                        yield return LoadAudioFile(f, clip => menuHoverClip = clip);
                    }
                    else if ((name.Contains("click") || name.Contains("select") || name.Contains("confirm") || name.Contains("press")) && menuClickClip == null)
                    {
                        yield return LoadAudioFile(f, clip => menuClickClip = clip);
                    }
                    else if ((name.Contains("back") || name.Contains("cancel") || name.Contains("exit")) && menuBackClip == null)
                    {
                        yield return LoadAudioFile(f, clip => menuBackClip = clip);
                    }
                }
            }
            string gameplayDir=Path.Combine(GameContentPaths.AudioRoot,"SFX","Gameplay");
            if(Directory.Exists(gameplayDir))foreach(string file in Directory.GetFiles(gameplayDir))
            {
                string ext=Path.GetExtension(file).ToLowerInvariant();if(ext!=".wav"&&ext!=".ogg"&&ext!=".mp3")continue;
                string name=Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                if(name.Contains("perfect"))yield return LoadAudioFile(file,clip=>{if(clip!=null)hitPerfectClip=clip;});
                else if(name.Contains("good"))yield return LoadAudioFile(file,clip=>{if(clip!=null)hitGoodClip=clip;});
                else if(name.Contains("miss"))yield return LoadAudioFile(file,clip=>{if(clip!=null)hitMissClip=clip;});
            }
        }

        #region Menu Audio API

        public void PlayMenuBGM(AudioClip clip = null)
        {
            if (clip != null) menuMusicClip = clip;
            if (menuMusicClip == null) return;

            if (menuBgmSource != null)
            {
                if (menuBgmSource.clip != menuMusicClip)
                {
                    menuBgmSource.clip = menuMusicClip;
                }
                if (!menuBgmSource.isPlaying)
                {
                    menuBgmSource.loop = true;
                    menuBgmSource.Play();
                }
            }
        }

        public void StopMenuBGM()
        {
            if (menuBgmSource != null && menuBgmSource.isPlaying)
            {
                menuBgmSource.Stop();
            }
        }

        public bool SuspendMenuBGM(){if(menuBgmSource==null||!menuBgmSource.isPlaying)return false;menuBgmSource.Pause();return true;}
        public void ResumeMenuBGM(){if(menuBgmSource!=null&&menuBgmSource.clip!=null)menuBgmSource.UnPause();}
        public void PlayMenuHover()
        {
            AudioClip clip = menuHoverClip != null ? menuHoverClip : fallbackHoverClip;
            PlaySFX(clip, 0.4f);
        }

        public void PlayMenuClick()
        {
            AudioClip clip = menuClickClip != null ? menuClickClip : fallbackClickClip;
            PlaySFX(clip, 0.8f);
        }

        public void PlayMenuBack()
        {
            AudioClip clip = menuBackClip != null ? menuBackClip : fallbackBackClip;
            PlaySFX(clip, 0.7f);
        }

        public void PlayHitRating(HitRating rating)
        {
            switch (rating)
            {
                case HitRating.Perfect:
                    if (hitPerfectClip != null) PlaySFX(hitPerfectClip, 0.9f);
                    break;
                case HitRating.Good:
                    if (hitGoodClip != null) PlaySFX(hitGoodClip, 0.75f);
                    break;
                case HitRating.Miss:
                    if (hitMissClip != null) PlaySFX(hitMissClip, 0.8f);
                    break;
            }
        }

        #endregion

        #region Gameplay Music API

        public void PlaySong(AudioClip clip, double startDelaySec = 1.0, double startAtSec = 0.0)
        {
            if (clip == null)
            {
                Debug.LogWarning("[AudioManager] Attempted to play null AudioClip.");
                return;
            }

            StopMenuBGM();

            musicSource.clip = clip;
            musicSource.Stop();

            double scheduled = AudioSettings.dspTime + startDelaySec;
            scheduledAudioStart=scheduled;pausedLeadIn=0;inputEpochDsp=AudioSettings.dspTime;
            double start = Math.Max(0, Math.Min(clip.length - .01, startAtSec));
            musicSource.time = (float)start;
            dspSongStart = scheduled - start / musicSource.pitch;
            musicSource.PlayScheduled(scheduled);

            isPlaying = true;
            isPaused = false;
            pauseTimeOffset = 0.0;
        }

        public void PauseSong()
        {
            if (!isPlaying || isPaused) return;

            pauseTimeOffset = SongTime;
            offsetAtPause=GameSettings.AudioOffsetSec;
            pausedLeadIn=Math.Max(0,scheduledAudioStart-AudioSettings.dspTime);
            musicSource.Pause();
            isPaused = true;
        }

        public void ResumeSong()
        {
            if (!isPlaying || !isPaused) return;

            // A paused pre-roll must be rescheduled: UnPause would start audio
            // immediately while the note clock is still approaching its cue.
            double now=AudioSettings.dspTime;inputEpochDsp=now;
            double pitch=musicSource!=null?musicSource.pitch:1f;
            double frozen=SongTime;
            dspSongStart = now - ((frozen-GameSettings.AudioOffsetSec) / pitch);
            if(pausedLeadIn>0)
            {
                double cue=frozen-GameSettings.AudioOffsetSec+pausedLeadIn*pitch;
                musicSource.Stop();musicSource.time=(float)Math.Max(0,Math.Min(musicSource.clip.length-.01,cue));
                scheduledAudioStart=now+pausedLeadIn;musicSource.PlayScheduled(scheduledAudioStart);
            }
            else musicSource.UnPause();
            pausedLeadIn=0;
            isPaused = false;
        }

        public void StopSong()
        {
            if (musicSource != null)
            {
                musicSource.Stop();
            }
            isPlaying = false;
            isPaused = false;
            pausedLeadIn=0;scheduledAudioStart=0;
        }

        public void PlaySFX(AudioClip clip, float volumeScale = 1.0f)
        {
            if (clip != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
            }
        }

        public void UpdateVolumes()
        {
            if (musicSource != null)
                musicSource.volume = GameSettings.MusicVolume * GameSettings.MasterVolume;

            if (menuBgmSource != null)
                menuBgmSource.volume = GameSettings.MusicVolume * GameSettings.MasterVolume;

            if (sfxSource != null)
                sfxSource.volume = GameSettings.SFXVolume * GameSettings.MasterVolume;
        }

        public void SetPitch(float pitch)
        {
            if (musicSource != null)
            {
                musicSource.pitch = Mathf.Clamp(pitch, 0.5f, 2.0f);
            }
        }

        #endregion

        #region Dynamic Disk Audio Loader (.mp3 / .ogg / .wav)

        public IEnumerator LoadAudioFile(string fullPath, Action<AudioClip> callback)
        {
            fullPath=GameContentPaths.ResolveAudio(fullPath);
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            {
                Debug.LogWarning($"[AudioManager] File does not exist at: {fullPath}");
                callback?.Invoke(null);
                yield break;
            }

            string ext = Path.GetExtension(fullPath).ToLower();
            AudioType type = AudioType.UNKNOWN;
            if (ext == ".mp3") type = AudioType.MPEG;
            else if (ext == ".ogg") type = AudioType.OGGVORBIS;
            else if (ext == ".wav") type = AudioType.WAV;

            string uri = new System.Uri(Path.GetFullPath(fullPath)).AbsoluteUri;
            using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(uri, type))
            {
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
                    clip.name = Path.GetFileNameWithoutExtension(fullPath);
                    Debug.Log($"[AudioManager] Successfully loaded {clip.name} ({clip.length:F1}s) from {fullPath}");
                    callback?.Invoke(clip);
                }
                else
                {
                    Debug.LogWarning($"[AudioManager] Could not load audio from {uri}: {req.error}");
                    callback?.Invoke(null);
                }
            }
        }

        #endregion
    }
}
