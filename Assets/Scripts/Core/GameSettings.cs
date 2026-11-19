using UnityEngine;

namespace RhythmWare.Core
{
    public static class GameSettings
    {
        private const string AUDIO_OFFSET_KEY = "AudioOffsetSec";
        private const string MASTER_VOL_KEY = "MasterVolume";
        private const string MUSIC_VOL_KEY = "MusicVolume";
        private const string SFX_VOL_KEY = "SFXVolume";
        private const string BG_DIM_KEY = "BackgroundDim";
        const string SCROLL_SPEED_KEY="NoteScrollSpeed";
        public const float MinNoteSpeed=.5f,MaxNoteSpeed=8;
        static string rhythmPrefix="";
        static string PrefsPrefix=>rhythmPrefix.Length>0?rhythmPrefix:ParticipantProfiles.Current!=null?"RWProfile_"+ParticipantProfiles.Current.localId+"_":ProfileStorage.IsValidation?"RWValidation_":"";
        public static void NotifyProfileChanged(){OnNoteSpeedChanged?.Invoke(NoteSpeed);OnBackgroundDimChanged?.Invoke(BackgroundDim);}
        const string FIRST_RUN_KEY="AudioSetupSeenV1";
        public static bool HasSeenAudioSetup {get=>PlayerPrefs.GetInt(PrefsPrefix+FIRST_RUN_KEY,0)==1;set {PlayerPrefs.SetInt(PrefsPrefix+FIRST_RUN_KEY,value?1:0);PlayerPrefs.Save();}}
        public static event System.Action<float> OnNoteSpeedChanged;
        public static float NoteSpeed
        {
            get {float value=PlayerPrefs.GetFloat(PrefsPrefix+SCROLL_SPEED_KEY,1);return float.IsNaN(value)||float.IsInfinity(value)?1:Mathf.Clamp(value,MinNoteSpeed,MaxNoteSpeed);}
            set {if(float.IsNaN(value)||float.IsInfinity(value))return;float next=Mathf.Round(Mathf.Clamp(value,MinNoteSpeed,MaxNoteSpeed)*10)/10;if(Mathf.Abs(next-NoteSpeed)<.0001f)return;PlayerPrefs.SetFloat(PrefsPrefix+SCROLL_SPEED_KEY,next);PlayerPrefs.Save();OnNoteSpeedChanged?.Invoke(next);}
        }
        public static double NoteTravelSeconds=>2.4/NoteSpeed;
#if UNITY_EDITOR
        public static void UseRhythmValidationStore(string prefix)
        {if(rhythmPrefix.Length>0){PlayerPrefs.DeleteKey(rhythmPrefix+FIRST_RUN_KEY);PlayerPrefs.DeleteKey(rhythmPrefix+SCROLL_SPEED_KEY);PlayerPrefs.DeleteKey(rhythmPrefix+AUDIO_OFFSET_KEY);PlayerPrefs.DeleteKey(rhythmPrefix+MASTER_VOL_KEY);PlayerPrefs.DeleteKey(rhythmPrefix+MUSIC_VOL_KEY);PlayerPrefs.DeleteKey(rhythmPrefix+SFX_VOL_KEY);}rhythmPrefix=prefix??"";OnNoteSpeedChanged?.Invoke(NoteSpeed);}
#endif

        public static event System.Action<float> OnBackgroundDimChanged;

        public static float BackgroundDim
        {
            get => PlayerPrefs.GetFloat(BG_DIM_KEY, 0.35f);
            set
            {
                float clamped = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(BG_DIM_KEY, clamped);
                PlayerPrefs.Save();
                OnBackgroundDimChanged?.Invoke(clamped);
            }
        }

        public static float AudioOffsetSec
        {
            get {float value=PlayerPrefs.GetFloat(PrefsPrefix+AUDIO_OFFSET_KEY,0);return float.IsNaN(value)||float.IsInfinity(value)?0:Mathf.Clamp(value,-.5f,.5f);}
            set
            {
                if(float.IsNaN(value)||float.IsInfinity(value))return;
                PlayerPrefs.SetFloat(PrefsPrefix+AUDIO_OFFSET_KEY, Mathf.Clamp(value,-.5f,.5f));
                PlayerPrefs.Save();
            }
        }

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(PrefsPrefix+MASTER_VOL_KEY, 0.3f);
            set
            {
                PlayerPrefs.SetFloat(PrefsPrefix+MASTER_VOL_KEY, Mathf.Clamp01(value));
                PlayerPrefs.Save();
            }
        }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(PrefsPrefix+MUSIC_VOL_KEY, 1.0f);
            set
            {
                PlayerPrefs.SetFloat(PrefsPrefix+MUSIC_VOL_KEY, Mathf.Clamp01(value));
                PlayerPrefs.Save();
            }
        }

        public static float SFXVolume
        {
            get => PlayerPrefs.GetFloat(PrefsPrefix+SFX_VOL_KEY, 1.0f);
            set
            {
                PlayerPrefs.SetFloat(PrefsPrefix+SFX_VOL_KEY, Mathf.Clamp01(value));
                PlayerPrefs.Save();
            }
        }

        public static bool IsFullscreen
        {
            get => DisplaySettings.IsFullscreen;
            set => DisplaySettings.IsFullscreen=value;
        }
    }
}
