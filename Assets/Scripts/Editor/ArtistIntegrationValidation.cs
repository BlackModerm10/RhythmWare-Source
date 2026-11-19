#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RhythmWare.Core;
using RhythmWare.UI;
using RhythmWare.Rhythm;
using RhythmWare.Malware;
using System.Collections.Generic;

namespace RhythmWare.EditorTools
{
    // Local, explicit validation requests; never included in a player build.
    [InitializeOnLoad]
    public static class ArtistIntegrationValidation
    {
        const string Request = "Library/RhythmWareArtistRequest.txt";
        const string Output = "docs/sprite-integration/validation";
        static double next;
        static readonly List<string> checks = new List<string>();
        static readonly List<string> errors = new List<string>();
        static readonly List<MalwareData> testMalware = new List<MalwareData>();
        static AudioClip testAudio;
        static ChartData testChart;
        static int testStage = -1;
        static double testAt, deadline, previousSongTime, pausedElapsed;
        static int previousFrame;
        static NoteController pausedNote;
        static Sprite pausedSprite;
        static ChartData previousChart;
        static bool previousRunInBackground;

        static ArtistIntegrationValidation()
        {
            EditorApplication.delayCall += Inspect;
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + (testStage >= 0 ? .05 : .5);
            if (testStage >= 0) { AdvanceTest(); return; }
            if (!File.Exists(Request)) return;
            string command = File.ReadAllText(Request).Trim();
            File.Delete(Request);
            try
            {
                if (command == "inspect") Inspect();
                else if (command == "play") { Application.runInBackground = true; EditorApplication.isPaused = false; EditorApplication.isPlaying = true; }
                else if (command == "stop") EditorApplication.isPlaying = false;
                else if (command == "menu") { MainMenuUI.Instance.BackToMainMenu(); }
                else if (command == "capture") Capture("game-view.png");
                else if (command == "test") Run();
                else if (command == "refresh") AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }
            catch (Exception ex) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/error.txt", ex.ToString()); Debug.LogException(ex); }
        }

        [MenuItem("RhythmWare/Validacion/Probar sprites del artista")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Abre SampleScene y pulsa Play antes de probar los sprites.");
            if (GameManager.Instance == null) throw new InvalidOperationException("Reinicia Play despues de recompilar los scripts para restaurar los gestores del juego.");
            if (GameManager.Instance.CurrentState != GameState.MainMenu) throw new InvalidOperationException("Vuelve al menu principal antes de probar los sprites.");
            EditorApplication.isPaused = false;
            checks.Clear(); errors.Clear();
            previousRunInBackground = Application.runInBackground; Application.runInBackground = true;
            previousChart = ChartManager.Instance.CurrentChart;
            Application.logMessageReceived += CollectError;
            try
            {
                Inspect(); Capture("main-menu.png");
                ValidateClips();
                testChart = ScriptableObject.CreateInstance<ChartData>();
                testChart.songTitle = "Prueba local de sprites";
                testAudio = AudioClip.Create("ArtistSpriteValidation", 44100 * 30, 1, 44100, false);
                testChart.audioClip = testAudio;
                // Regular spawning and actual song clock, across both hands and SPACE.
                for (int i = 0; i < RhythmWare.Core.LaneLayout.Count; i++) testChart.notes.Add(new NoteData
                {
                    timestamp = 3 + i * .035, laneIndex = i,
                    malwareData = i % 4 == 0 ? null : testMalware[i % 4 - 1],
                    duration = i == RhythmWare.Core.LaneLayout.Center ? 1.0 : 0
                });
                for (int i = 0; i < RhythmWare.Core.LaneLayout.Count; i++) testChart.notes.Add(new NoteData
                { timestamp = 5 + i * .035, laneIndex = i, malwareData = i % 2 == 0 ? testMalware[2] : null });
                ChartManager.Instance.SelectChart(testChart);
                GameManager.Instance.ResetStats();
                NoteSpawner.Instance.LoadChart(testChart);
                GameManager.Instance.SetState(GameState.Playing);
                AudioManager.Instance.PlaySong(testAudio);
                previousFrame = Time.frameCount;
                previousSongTime = AudioManager.Instance.SongTime;
                deadline = EditorApplication.timeSinceStartup + 30;
                testStage = 0;
            }
            catch (Exception ex) { FinishTest(ex); }
        }

        static void CollectError(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception) errors.Add(message + "\n" + stack); }

        static void Check(bool passed, string message)
        { if (!passed) throw new Exception(message); checks.Add("PASS: " + message); }

        static void ValidateClips()
        {
            var spy = ScriptableObject.CreateInstance<SpywareData>(); spy.minOpacity = .38f;
            var adware = ScriptableObject.CreateInstance<AdwareData>();
            var trojan = ScriptableObject.CreateInstance<TrojanData>();
            testMalware.Add(spy); testMalware.Add(adware); testMalware.Add(trojan);
            MalwareData[] types = { null, spy, adware, trojan };
            int[] counts = { 9, 10, 9, 14 }, timelines = { 16, 17, 16, 20 };
            for (int i = 0; i < types.Length; i++)
            {
                var clip = NoteCharacterAnimations.For(types[i]);
                Check(clip != null && clip.Frames.Length == counts[i], "Artist frame count " + i);
                Check(Math.Abs(clip.Duration - timelines[i] / 30.0) < .000001, "30 fps source timeline duration " + i);
                var visited = new HashSet<int>();
                for (int j = 0; j < clip.Frames.Length; j++)
                {
                    var frame = clip.Frames[j];
                    Check(frame.rect.size == new Vector2(256, 256) && frame.pivot == new Vector2(128, 128) && frame.pixelsPerUnit == 256, "Uncropped centered sprite " + i + ":" + j);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(frame));
                    Check(importer.DoesSourceTextureHaveAlpha() && !importer.mipmapEnabled && importer.textureCompression == TextureImporterCompression.Uncompressed, "Alpha and lossless import " + i + ":" + j);
                    visited.Add(clip.FrameIndex((j + .25) * clip.FrameDuration));
                }
                Check(visited.Count == counts[i] && clip.FrameIndex(clip.Duration + .000001) == 0, "Every exported frame plays and loops " + i);
            }
            var reveal = NoteCharacterAnimations.For(trojan);
            Check(reveal.FrameIndex(.69, .7) < 4 && reveal.FrameIndex(.7, .7) == 4, "Trojan stays closed until reveal, then opens");
            Check(reveal.FrameIndex(.8, .7, true) == 7 && reveal.FrameIndex(.6, .7, true) == 0, "Reduced effects preserve Trojan disguise/reveal");
            var fallback = ScriptableObject.CreateInstance<RansomwareData>();
            Check(NoteCharacterAnimations.For(fallback) == null, "Other implemented families keep procedural fallback");
            UnityEngine.Object.Destroy(fallback);
        }

        static void AdvanceTest()
        {
            try
            {
                if (!EditorApplication.isPlaying) throw new Exception("Play mode stopped during validation");
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out waiting for actual game frames");
                if (testStage == 0)
                {
                    double songTime = AudioManager.Instance.SongTime;
                    if (songTime < 2.1) return;
                    Check(Time.frameCount > previousFrame && songTime > previousSongTime, "Live game frames and audio clock advance");
                    var notes = UnityEngine.Object.FindObjectsByType<NoteController>(FindObjectsInactive.Exclude);
                    Check(notes.Length == RhythmWare.Core.LaneLayout.Count && notes.All(n => n.UsesCharacterAnimation), "Real spawner uses the four artist sequences across five lanes");
                    Check(notes.All(n => n.SpriteRenderer.color.r == 1 && n.SpriteRenderer.color.g == 1 && n.SpriteRenderer.color.b == 1), "Original art RGB preserved");
                    Check(notes.All(n => !n.SpriteRenderer.flipX && !n.SpriteRenderer.flipY && n.transform.rotation == Quaternion.identity), "Every lane keeps downward-facing art");
                    Check(notes.All(n => !n.transform.Find("BadgeText").gameObject.activeSelf), "Old text badges do not cover faces");
                    Check(notes.Any(n => n.Data.isHoldNote && n.GetComponent<LineRenderer>() != null), "Artist sprites keep existing hold tails");
                    pausedNote = notes.First(n => n.Data.malwareData is SpywareData);
                    Check(pausedNote.SpriteRenderer.color.a > 0 && pausedNote.SpriteRenderer.color.a < 1, "Spyware preserves gradual transparency");
                    pausedElapsed = pausedNote.AnimationElapsed; pausedSprite = pausedNote.SpriteRenderer.sprite;
                    Capture("sprites-in-game.png");
                    UnityEngine.Object.FindAnyObjectByType<GameplayUI>().TogglePause();
                    testAt = EditorApplication.timeSinceStartup + .8; testStage = 1;
                }
                else if (testStage == 1 && EditorApplication.timeSinceStartup >= testAt)
                {
                    Check(pausedNote != null && pausedNote.AnimationElapsed == pausedElapsed && pausedNote.SpriteRenderer.sprite == pausedSprite, "Pause freezes the sprite frame with the song");
                    UnityEngine.Object.FindAnyObjectByType<GameplayUI>().ResumeGame();
                    testAt = EditorApplication.timeSinceStartup + .18; testStage = 2;
                }
                else if (testStage == 2 && EditorApplication.timeSinceStartup >= testAt)
                {
                    Check(pausedNote != null && pausedNote.AnimationElapsed > pausedElapsed, "Resume advances animation again");
                    Capture("sprites-in-game-resumed.png");
                    Check(errors.Count == 0, "No runtime errors during sprite validation");
                    FinishTest(null);
                }
            }
            catch (Exception ex) { FinishTest(ex); }
        }

        static void FinishTest(Exception failure)
        {
            testStage = -1;
            Application.logMessageReceived -= CollectError;
            if (failure != null) errors.Add(failure.ToString());
            NoteSpawner.Instance?.StopAndClear(); AudioManager.Instance?.StopSong();
            ChartManager.Instance.SelectChart(previousChart);
            if (GameManager.Instance != null) { GameManager.Instance.ResetStats(); GameManager.Instance.SetState(GameState.MainMenu); }
            Application.runInBackground = previousRunInBackground;
            if (testChart != null) UnityEngine.Object.Destroy(testChart);
            if (testAudio != null) UnityEngine.Object.Destroy(testAudio);
            foreach (var malware in testMalware) if (malware != null) UnityEngine.Object.Destroy(malware);
            testMalware.Clear();
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/sprite-tests.json", JsonUtility.ToJson(new TestResult { passed = failure == null && errors.Count == 0, checks = checks.ToArray(), errors = errors.ToArray() }, true));
            Inspect();
        }

        [Serializable] sealed class TestResult { public bool passed; public string[] checks, errors; }

        [MenuItem("RhythmWare/Validacion/Inspeccionar arranque")]
        public static void Inspect()
        {
            Directory.CreateDirectory(Output);
            var scene = SceneManager.GetActiveScene();
            File.WriteAllText(Output + "/status.json", JsonUtility.ToJson(new Status
            {
                utc = DateTime.UtcNow.ToString("O"), playing = EditorApplication.isPlaying,
                paused = EditorApplication.isPaused, timeScale = Time.timeScale, frame = Time.frameCount,
                scene = scene.path, dirty = scene.isDirty,
                roots = scene.GetRootGameObjects().Select(o => o.name + " active=" + o.activeInHierarchy).ToArray(),
                setup = UnityEngine.Object.FindObjectsByType<RhythmWareSceneSetup>(FindObjectsInactive.Include).Select(s => s.name + " enabled=" + s.enabled + " active=" + s.gameObject.activeInHierarchy).ToArray(),
                cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include).Select(c => c.name + " enabled=" + c.enabled + " active=" + c.gameObject.activeInHierarchy + " mask=" + c.cullingMask + " display=" + c.targetDisplay + " target=" + c.targetTexture).ToArray(),
                canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Select(c => c.name + " enabled=" + c.enabled + " active=" + c.gameObject.activeInHierarchy + " mode=" + c.renderMode + " display=" + c.targetDisplay).ToArray(),
                gameState = GameManager.Instance == null ? "no manager" : GameManager.Instance.CurrentState.ToString(),
                menu = MainMenuUI.Instance == null ? "no menu" : MainMenuUI.Instance.name,
                startScene = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene)
            }, true));
        }

        public static void Capture(string name)
        {
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("No main camera");
            var canvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).FirstOrDefault(c => c.isRootCanvas);
            var target = new RenderTexture(1280, 720, 24);
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var oldMode = canvas != null ? canvas.renderMode : RenderMode.ScreenSpaceOverlay;
            var oldCamera = canvas != null ? canvas.worldCamera : null;
            var oldDistance = canvas != null ? canvas.planeDistance : 0;
            var oldSorting = canvas != null ? canvas.sortingOrder : 0;
            try
            {
                camera.targetTexture = target;
                if (canvas != null) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + .1f; canvas.sortingOrder = 10000; }
                Canvas.ForceUpdateCanvases();
                foreach (var graphic in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Graphic>()) graphic.Rebuild(UnityEngine.UI.CanvasUpdate.PreRender);
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
                Directory.CreateDirectory(Output); File.WriteAllBytes(Output + "/" + name, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                if (canvas != null) { canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance; canvas.sortingOrder = oldSorting; }
                target.Release(); UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Serializable] class Status
        {
            public string utc, scene, gameState, menu, startScene;
            public bool playing, paused, dirty;
            public int frame;
            public float timeScale;
            public string[] roots, setup, cameras, canvases;
        }
    }
}
#endif

