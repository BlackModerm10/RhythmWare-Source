using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using RhythmWare.Rhythm;
using RhythmWare.UI;

namespace RhythmWare.Core
{
    // Opt-in diagnostics for the actual standalone player; never runs in normal play.
    public sealed class DistributionSelfTest : MonoBehaviour
    {
        public static bool IsRunning { get; private set; }
        readonly List<string> results=new List<string>();
        string report;
        bool finished;
        float deadline;
        readonly List<Action> restorePreferences=new List<Action>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {
            if(Application.isEditor)return;
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--rw-self-test");
            if(i<0||i+1>=args.Length)return;
            IsRunning=true;
            var probe=new GameObject("DistributionSelfTest").AddComponent<DistributionSelfTest>();
            probe.report=Path.GetFullPath(args[i+1]);Directory.CreateDirectory(Path.GetDirectoryName(probe.report));
            Application.runInBackground=true;probe.deadline=Time.realtimeSinceStartup+(args.Contains("--rw-test-online")?240:90);probe.StartCoroutine(probe.Run());
        }
        void Update() { if(!finished&&report!=null&&Time.realtimeSinceStartup>deadline){results.Add("FAIL: Player diagnostic timeout");Finish(false);} }
        void Check(bool success,string label)
        {
            results.Add((success?"PASS: ":"FAIL: ")+label);
            if(!success){Finish(false);throw new InvalidOperationException(label);}
        }
        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(2);
            Check(!Application.isEditor,"Running the exported Windows player");
            Check(Inside(RhythmWare.Education.SocLearningProgress.Pathname,Path.GetDirectoryName(report)),"SOC diagnostics use a separate file outside the player's learning profile");
            if(Environment.GetCommandLineArgs().Contains("--rw-test-display"))yield return CheckDisplayAndAudioDefaults();
            if(Environment.GetCommandLineArgs().Contains("--rw-test-online"))yield return OnlineLiveSelfTest.Run(Check,Capture,Path.GetDirectoryName(report));
            Check(Inside(GameContentPaths.AudioRoot,Application.streamingAssetsPath),"Audio root belongs to this executable's StreamingAssets");
            string manifestPath=Path.Combine(Application.streamingAssetsPath,"content-manifest.json");
            Check(File.Exists(manifestPath),"Build includes a bundled-content manifest");
            var manifest=JsonUtility.FromJson<BundledContentManifest>(File.ReadAllText(manifestPath));
            Check(manifest.version==1&&manifest.files.Length>=6,"Campaign music, menu and SFX are registered");
            foreach(var entry in manifest.files)
            {
                string path=Path.Combine(Application.streamingAssetsPath,entry.path);Check(File.Exists(path),"Bundled file exists: "+entry.path);
                using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))
                    Check(stream.Length==entry.bytes&&BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant()==entry.sha256,"Bundled hash matches: "+entry.path);
            }
            var audio=AudioManager.Instance;Check(audio!=null,"Audio manager initializes in the player");
            float wait=Time.realtimeSinceStartup+15;while(!audio.IsMenuMusicPlaying&&Time.realtimeSinceStartup<wait)yield return null;
            Check(audio.IsMenuMusicPlaying,"Bundled menu music loads and starts automatically");
            var sources=audio.GetComponents<AudioSource>();Check(sources.Length>=3,"Music, menu and SFX have their own sources");
            Check(Mathf.Approximately(sources[1].volume,GameSettings.MasterVolume*GameSettings.SFXVolume),"SFX source applies saved master/SFX gain once");
            // Per-source changes affect this diagnostic process only; no preferences are saved.
            AudioListener.pause=false;AudioListener.volume=1;foreach(var source in sources){source.volume=1;source.mute=false;}
            var charts=CampaignCatalog.CreateLevels().Select(e=>e.chartData).ToArray();
            foreach(var chart in charts)
            {
                Check(Inside(chart.audioClipPath,GameContentPaths.AudioRoot)&&File.Exists(chart.audioClipPath),"Campaign resolves inside bundle: "+chart.songTitle);
                AudioClip clip=null;yield return audio.LoadAudioFile(chart.audioClipPath,c=>clip=c);
                Check(clip!=null&&clip.length>chart.notes.Max(n=>n.timestamp+n.duration),"Full song decodes: "+chart.songTitle);chart.audioClip=clip;
                float[] pcm=new float[Mathf.Min(clip.samples,44100)*clip.channels];
                Check(clip.GetData(pcm,Mathf.Min(clip.samples-pcm.Length/clip.channels,(int)(clip.frequency*clip.length*.25f)))&&pcm.Any(v=>Mathf.Abs(v)>.00001f),"Decoded PCM is non-silent: "+chart.songTitle);
                audio.PlaySong(clip,.05,clip.length*.25);double before=audio.SongTime;
                yield return new WaitForSecondsRealtime(.35f);
                Check(audio.IsPlaying&&audio.SongTime>before+.2,"Song transport advances: "+chart.songTitle);
            }
            yield return CheckMixedAudio("Music reaches the audio mix");
            audio.StopSong();audio.StopMenuBGM();
            yield return new WaitForSecondsRealtime(.15f); // Drain the last music output buffer before measuring SFX.
            foreach(var rating in new[]{HitRating.Perfect,HitRating.Good,HitRating.Miss})
            {
                sources[1].Stop();audio.PlayHitRating(rating);yield return CheckMixedAudio("Audible gameplay SFX: "+rating);
            }
            sources[1].Stop();audio.PlayMenuHover();yield return CheckMixedAudio("Bundled hover WAV reaches the mix");
            sources[1].Stop();audio.PlayMenuClick();yield return CheckMixedAudio("Bundled select WAV reaches the mix");
            sources[1].Stop();audio.PlayMenuBack();yield return CheckMixedAudio("Back navigation fallback reaches the mix");
            string legacy=GameContentPaths.ResolveAudio("C:/AnotherPC/OldProject/Assets\\Audio\\Music\\Levels\\Sara.mp3");
            Check(legacy==GameContentPaths.ResolveAudio("Audio/Music/Levels/Sara.mp3")&&File.Exists(legacy),"Legacy developer paths migrate to bundled audio");
            Check(charts.All(c=>!c.ToJson().Contains(Application.dataPath)&&JsonUtility.FromJson<ChartJsonDto>(c.ToJson()).audioClipPath.StartsWith("Audio/")),"Chart exports use portable bundled audio paths");
            Check(!Directory.Exists(GameContentPaths.BundledChartsRoot)||Directory.GetFiles(GameContentPaths.BundledChartsRoot,"*.json").Length==0,"Beta ships only campaign maps; personal maps remain separate");
            Check(Resources.Load<Sprite>("Presentation/EpicStage")!=null&&Resources.Load<Texture2D>("Boss/BonziAtlas")!=null,"Stage artwork and Bonzi atlas survive build stripping");
            Check(Resources.Load<Material>("Presentation/FluidStage").shader.isSupported&&Resources.Load<Material>("Boss/BonziKey").shader.isSupported,"Stage and Bonzi shaders are supported by the player");
            Check(CyberUI.Font!=null,"Menu TMP font is available in the player");
            yield return new WaitForEndOfFrame();Capture("player-main-menu.png");
            // This is a passive preview, not an autoplay. The current boss has
            // dense notes from its first beat; protect it while capturing.
            GameplayUI.IsTestPlayFromEditor=true;GameManager.Instance.TrainingSafety=true;ChartManager.Instance.SelectChart(charts[2]);NoteSpawner.Instance.LoadChart(charts[2]);
            audio.PlaySong(charts[2].audioClip,.1);GameManager.Instance.StartGame();yield return new WaitForSecondsRealtime(2);
            Check(GameManager.Instance.CurrentState==GameState.Playing&&RhythmWare.Boss.BonziStageController.Instance.IsEncounter,"Bonzi gameplay and dialogue initialize in the standalone player (state="+GameManager.Instance.CurrentState+")");
            yield return new WaitForEndOfFrame();Capture("player-bonzi.png");
            yield return null; // Allow deferred render resource destruction before quitting.
            Finish(true);
        }
        IEnumerator CheckDisplayAndAudioDefaults()
        {
            const string volume="RWValidation_MasterVolume";
            bool hadVolume=PlayerPrefs.HasKey(volume);float oldVolume=PlayerPrefs.GetFloat(volume);
            restorePreferences.Add(()=>{if(hadVolume)PlayerPrefs.SetFloat(volume,oldVolume);else PlayerPrefs.DeleteKey(volume);});
            PlayerPrefs.DeleteKey(volume);
            Check(ParticipantProfiles.Current==null&&Mathf.Approximately(GameSettings.MasterVolume,.3f),"A fresh participant starts at 30% master volume");
            foreach(string key in new[]{"RWValidation_Display_DisplayWidthV1","RWValidation_Display_DisplayHeightV1","RWValidation_Display_Fullscreen"})
            {
                string captured=key;bool existed=PlayerPrefs.HasKey(key);int prior=PlayerPrefs.GetInt(key);
                restorePreferences.Add(()=>{if(existed)PlayerPrefs.SetInt(captured,prior);else PlayerPrefs.DeleteKey(captured);});PlayerPrefs.DeleteKey(key);
            }
            var native=DisplaySettings.NativeResolution;
            Check(DisplaySettings.Resolution==native,"Fresh display preferences choose the monitor instead of a remembered 4:3 render size");
            DisplaySettings.Apply();yield return AwaitDisplay(native,true);
            var window=new Vector2Int(1280,720);
            DisplaySettings.Set(window,false);yield return AwaitDisplay(window,false);
            Check(DisplaySettings.ResolutionLabel.Contains("16:9"),"The resolution selector identifies the 16:9 aspect ratio");
            GameSettings.MasterVolume=.42f;Check(Mathf.Approximately(GameSettings.MasterVolume,.42f),"An explicitly saved volume is preserved");GameSettings.MasterVolume=.3f;AudioManager.Instance.UpdateVolumes();
            MainMenuUI.Instance.OpenSettings();yield return null;
            var settings=FindAnyObjectByType<SettingsUI>();
            Check(settings.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Any(t=>t.text.Contains("1280 × 720")),"Settings displays the selected resolution");
            Capture("player-settings-1280.png");
            var next=settings.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.GetComponentInChildren<TMPro.TextMeshProUGUI>()?.text=="›");
            next.onClick.Invoke();var changed=DisplaySettings.Resolution;
            Check(changed!=window,"The visible next-resolution button changes the saved selection");yield return AwaitDisplay(changed,false);
            var toggle=settings.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.GetComponentInChildren<TMPro.TextMeshProUGUI>()?.text=="MODO VENTANA");
            toggle.onClick.Invoke();yield return AwaitDisplay(changed,true);
            Check(DisplaySettings.Resolution==changed&&DisplaySettings.IsFullscreen,"Resolution and fullscreen mode remain stored together");
            Capture("player-settings-fullscreen.png");
            DisplaySettings.Set(window,false);yield return AwaitDisplay(window,false);MainMenuUI.Instance.BackToMainMenu();
        }
        IEnumerator AwaitDisplay(Vector2Int expected,bool fullscreen)
        {
            float until=Time.realtimeSinceStartup+4;
            do {yield return null;}while(Time.realtimeSinceStartup<until&&(Screen.width!=expected.x||Screen.height!=expected.y||Screen.fullScreen!=fullscreen));
            Check(Screen.width==expected.x&&Screen.height==expected.y&&Screen.fullScreen==fullscreen,"Actual player display applies "+expected.x+"x"+expected.y+(fullscreen?" fullscreen":" windowed"));
        }
        IEnumerator CheckMixedAudio(string label)
        {
            var samples=new float[1024];float peak=0,end=Time.realtimeSinceStartup+.35f;
            while(Time.realtimeSinceStartup<end)
            {
                AudioListener.GetOutputData(samples,0);foreach(float v in samples)peak=Mathf.Max(peak,Mathf.Abs(v));yield return null;
            }
            Check(peak>.000001f,label+" (peak "+peak.ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+")");
        }
        static bool Inside(string path,string directory)=>Path.GetFullPath(path).StartsWith(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
        void Capture(string name)
        {
            // A hidden Windows swapchain can be black. Render the actual scene and
            // Canvas directly, just as the native editor validation does.
            var camera=Camera.main;var canvas=FindAnyObjectByType<Canvas>();
            var target=new RenderTexture(Screen.width,Screen.height,24,RenderTextureFormat.ARGB32);target.Create();
            var oldTarget=camera.targetTexture;var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;
            float oldPlane=canvas.planeDistance;int oldSort=canvas.sortingOrder;var previous=RenderTexture.active;
            Texture2D image=null;
            try
            {
                camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;
                canvas.planeDistance=camera.nearClipPlane+.1f;canvas.sortingOrder=10000;Canvas.ForceUpdateCanvases();
                foreach(var graphic in FindObjectsByType<UnityEngine.UI.Graphic>())graphic.Rebuild(UnityEngine.UI.CanvasUpdate.PreRender);
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=target});
                RenderTexture.active=target;image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
                var sample=image.GetPixels32().Where((c,i)=>i%101==0).ToArray();
                Check(sample.Any(c=>c.r>100||c.g>100||c.b>100)&&sample.Select(c=>(c.r<<16)|(c.g<<8)|c.b).Distinct().Take(25).Count()>20,"Player renders visible scene/UI pixels: "+name);
                File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(report),name),image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=oldTarget;canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldPlane;canvas.sortingOrder=oldSort;
                RenderTexture.active=previous;if(image!=null)Destroy(image);target.Release();Destroy(target);Canvas.ForceUpdateCanvases();
            }
        }
        void Finish(bool success)
        {
            if(finished)return;finished=true;foreach(var restore in restorePreferences)restore();PlayerPrefs.Save();File.WriteAllText(report,(success?"PASS":"FAIL")+"\n"+string.Join("\n",results)+"\n");Application.Quit(success?0:1);
        }
    }
}
