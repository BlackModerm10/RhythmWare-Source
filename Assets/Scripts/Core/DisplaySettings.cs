using System.Collections.Generic;
using UnityEngine;

namespace RhythmWare.Core
{
    // Display preferences belong to this PC, independently of the participant.
    public static class DisplaySettings
    {
        const string WidthKey="DisplayWidthV1",HeightKey="DisplayHeightV1",FullscreenKey="Fullscreen";
        static string Prefix=>ProfileStorage.IsValidation?"RWValidation_Display_":"";
        public static Vector2Int NativeResolution
        {
            get
            {
                int width=Display.main!=null?Display.main.systemWidth:0,height=Display.main!=null?Display.main.systemHeight:0;
                if(width<640||height<360){var current=Screen.currentResolution;width=current.width;height=current.height;}
                return width>=640&&height>=360?new Vector2Int(width,height):new Vector2Int(1280,720);
            }
        }
        public static Vector2Int Resolution
        {
            get
            {
                var native=NativeResolution;
                int width=PlayerPrefs.GetInt(Prefix+WidthKey,native.x),height=PlayerPrefs.GetInt(Prefix+HeightKey,native.y);
                return width>=640&&height>=360&&width<=native.x&&height<=native.y?new Vector2Int(width,height):native;
            }
        }
        public static bool IsFullscreen
        {
            get=>PlayerPrefs.GetInt(Prefix+FullscreenKey,1)==1;
            set=>Set(Resolution,value);
        }
        public static List<Vector2Int> Resolutions
        {
            get
            {
                var native=NativeResolution;
                var choices=new List<Vector2Int>();
                foreach(var item in new[]{new Vector2Int(960,540),new Vector2Int(1280,720),new Vector2Int(1366,768),new Vector2Int(1600,900),new Vector2Int(1920,1080),new Vector2Int(2560,1440),new Vector2Int(3840,2160),native,Resolution})
                    if(item.x<=native.x&&item.y<=native.y&&!choices.Contains(item))choices.Add(item);
                choices.Sort((a,b)=>a.x==b.x?a.y.CompareTo(b.y):a.x.CompareTo(b.x));return choices;
            }
        }
        public static void CycleResolution(int direction)
        {
            var options=Resolutions;int index=options.IndexOf(Resolution);
            Set(options[(index+(direction<0?-1:1)+options.Count)%options.Count],IsFullscreen);
        }
        public static void Set(Vector2Int resolution,bool fullscreen)
        {
            var native=NativeResolution;
            if(resolution.x<640||resolution.y<360||resolution.x>native.x||resolution.y>native.y)return;
            PlayerPrefs.SetInt(Prefix+WidthKey,resolution.x);PlayerPrefs.SetInt(Prefix+HeightKey,resolution.y);
            PlayerPrefs.SetInt(Prefix+FullscreenKey,fullscreen?1:0);PlayerPrefs.Save();Apply();
        }
        public static void Apply()
        {
#if !UNITY_EDITOR
            var size=Resolution;
            Screen.SetResolution(size.x,size.y,IsFullscreen?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed);
#endif
        }
        public static string ResolutionLabel
        {
            get
            {
                var r=Resolution;float aspect=(float)r.x/r.y;
                string ratio=Mathf.Abs(aspect-16f/9)<.02f?"16:9":Mathf.Abs(aspect-16f/10)<.02f?"16:10":Mathf.Abs(aspect-4f/3)<.02f?"4:3":"NATIVA";
                return r.x+" × "+r.y+" · "+ratio;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            // Automated runs request their own viewport and never change the player's display settings.
            if(!ProfileStorage.IsValidation)Apply();
        }
    }
}
