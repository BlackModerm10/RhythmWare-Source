using UnityEngine;

namespace RhythmWare.UI
{
    public static class VisualPreferences
    {
        static bool? reduced;
        public static bool ReducedEffects
        {
            get => reduced ?? (reduced=PlayerPrefs.GetInt("ReducedVisualEffects",0)!=0).Value;
            set { reduced=value;PlayerPrefs.SetInt("ReducedVisualEffects",value?1:0);PlayerPrefs.Save(); }
        }
    }
}
