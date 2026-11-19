#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using RhythmWare.Core;
using RhythmWare.Rhythm;
using RhythmWare.UI;

namespace RhythmWare.EditorTools
{
    // Invoked explicitly from the command line. No changes to production scenes.
    [InitializeOnLoad]
    public static class VisualRedesignValidation
    {
        static int stage;
        static UnityEngine.InputSystem.InputSettings originalInputSettings,validationInputSettings;
        static double next, deadline;
        static readonly List<string> results=new List<string>(), errors=new List<string>();
        static readonly List<string> editorIssues=new List<string>();
        static string Output => Path.Combine(Directory.GetParent(Application.dataPath).FullName,"docs",SessionState.GetString("RWValidationFolder","visual-validation"));
        static VisualRedesignValidation()
        {
            if(SessionState.GetBool("RWVisualValidation",false))EditorApplication.delayCall+=Resume;
        }
        public static void Run()
        { RunFiveKeys(); }
        public static void RunEpicStage()
        { RunFiveKeys(); }
        public static void RunFluidStage()
        { RunFiveKeys(); }
        public static void RunAcademy()
        { RunFiveKeys(); }
        public static void RunCandyBonzi()
        { RunFiveKeys(); }
        static bool AcademyRun => new[]{"academy-validation","candy-bonzi-validation","campaign-validation"}.Contains(SessionState.GetString("RWValidationFolder",""));
        public static void RunCampaign()
        { RunFiveKeys(); }
        public static void RunBonziDialogue()
        { SessionState.SetString("RWValidationFolder","bonzi-dialogue-validation");RunInternal(); }
        public static void RunReadability()
        { RunFiveKeys(); }
        public static void RunFiveKeys()
        { SessionState.SetString("RWValidationFolder","five-key-validation");RunInternal(); }
        public static void RunArena()
        { SessionState.SetString("RWValidationFolder","arena-validation");RunInternal(); }
        public static void RunEnvironments()
        { SessionState.SetString("RWValidationFolder","loop-desktop-validation");RunInternal(); }
        public static void RunBossXP()
        { SessionState.SetString("RWValidationFolder","bonzi-xp-validation");RunInternal(); }
        public static void RunMalware()
        { SessionState.SetString("RWValidationFolder","malware-validation");RunInternal(); }
        public static void RunOsuBoss()
        { SessionState.SetString("RWValidationFolder","osu-bonzi-validation");RunInternal(); }
        public static void RunComfort()
        { SessionState.SetString("RWValidationFolder","comfort-validation");RunInternal(); }
        public static void RunTutorial()
        { SessionState.SetString("RWValidationFolder","tutorial-validation");RunInternal(); }
        public static void RunSaraComplete()
        { SessionState.SetString("RWValidationFolder","sara-complete-validation");RunInternal(); }
        public static void RunChroma()
        { SessionState.SetString("RWValidationFolder","chroma-validation");RunInternal(); }
        public static void RunBottomLanes()
        { SessionState.SetString("RWValidationFolder","bottom-lanes-validation");RunInternal(); }
        public static void RunScrollCalibration()
        { SessionState.SetString("RWValidationFolder","scroll-calibration-validation");RunInternal(); }
        public static void RunConstantArrival()
        { SessionState.SetString("RWValidationFolder","constant-arrival-validation");RunInternal(); }
        public static void RunUniformSpacing()
        { SessionState.SetString("RWValidationFolder","uniform-spacing-validation");RunInternal(); }
        public static void RunBossLearning()
        { SessionState.SetString("RWValidationFolder","boss-learning-validation");RunInternal(); }
        public static void RunBetaPolish()
        { SessionState.SetString("RWValidationFolder","beta-polish-validation");RunInternal(); }
        public static void RunParticipants()
        { SessionState.SetString("RWValidationFolder","participants-validation");RunInternal(); }
        public static void RunBetaCore()
        { SessionState.SetString("RWValidationFolder","beta-core-validation");RunInternal(); }
        static bool BetaCoreRun=>SessionState.GetString("RWValidationFolder","")=="beta-core-validation";
        static bool BetaRun=>SessionState.GetString("RWValidationFolder","")=="beta-polish-validation";
        public static void RunSpeedWheel()
        { SessionState.SetString("RWValidationFolder","speed-wheel-validation");RunInternal(); }
        public static void RunTutorialSong()
        { SessionState.SetString("RWValidationFolder","tutorial-song-validation");RunInternal(); }
        static void RunInternal()
        {
            Directory.CreateDirectory(Output);
            if(!File.Exists("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset"))
            {
                var package=Directory.GetFiles("Library/PackageCache","TMP Essential Resources.unitypackage",SearchOption.AllDirectories).First();
                AssetDatabase.ImportPackage(package,false);
            }
            SessionState.SetBool("RWVisualValidation",true);
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            SetGameViewSize(1920,1080);
            Resume();EditorApplication.isPlaying=true;
        }
        static void Resume()
        {
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
            deadline=EditorApplication.timeSinceStartup+600;next=EditorApplication.timeSinceStartup+3;
        }
        static void Log(string text,string trace,LogType type)
        {
            if(type!=LogType.Exception&&type!=LogType.Error)return;
            // Keep editor Search-index startup failures visible but separate
            // from game exceptions; never discard or classify game stack frames.
            if(trace.Contains("UnityEditor.Search.SearchDatabase")&&!trace.Contains("RhythmWare."))editorIssues.Add(text+"\n"+trace);
            else errors.Add(text+"\n"+trace);
        }
        static void Check(bool value,string description)
        { if(!value)throw new Exception("FAILED: "+description);results.Add("PASS: "+description); }
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup>deadline){Finish(false,"Timeout waiting for Unity.");return;}
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
            try
            {
                next=EditorApplication.timeSinceStartup+2;
                var editor=ChartEditorManager.Instance;
                switch(stage)
                {
                    case 0:
                        // Headless validation injects events without a focused
                        // Game view. Keep that policy local to this QA process.
                        originalInputSettings=UnityEngine.InputSystem.InputSystem.settings;
                        validationInputSettings=UnityEngine.Object.Instantiate(originalInputSettings);
                        UnityEngine.InputSystem.InputSystem.settings=validationInputSettings;
                        UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
                        UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode=UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                        Check(MainMenuUI.Instance!=null&&editor!=null,"Scene bootstrap and typed menus");
                        var scaler=UnityEngine.Object.FindAnyObjectByType<UnityEngine.UI.CanvasScaler>();
                        Check(scaler.referenceResolution==new Vector2(1920,1080),"Canvas reference resolution");
                        Capture("main-menu-1920.png");
                        if(SessionState.GetString("RWValidationFolder","")=="participants-validation")stage=43;
                        else if(BetaRun||BetaCoreRun)stage=42;
                        else if(SessionState.GetString("RWValidationFolder","")=="speed-wheel-validation")stage=41;
                        else if(SessionState.GetString("RWValidationFolder","")=="boss-learning-validation")stage=39;
                        else if(new[]{"scroll-calibration-validation","constant-arrival-validation","uniform-spacing-validation"}.Contains(SessionState.GetString("RWValidationFolder","")))stage=37;
                        else if(SessionState.GetString("RWValidationFolder","")=="bottom-lanes-validation")stage=36;
                        else if(SessionState.GetString("RWValidationFolder","")=="chroma-validation")stage=35;
                        else if(SessionState.GetString("RWValidationFolder","")=="tutorial-song-validation")stage=33;
                        else if(new[]{"five-key-validation","arena-validation","loop-desktop-validation","bonzi-xp-validation","malware-validation","osu-bonzi-validation","comfort-validation","tutorial-validation","sara-complete-validation"}.Contains(SessionState.GetString("RWValidationFolder","")))stage=24;
                        else if(SessionState.GetString("RWValidationFolder","")=="gameplay-readability/validation")stage=23;
                        else if(SessionState.GetString("RWValidationFolder","")=="bonzi-dialogue-validation")stage=22;
                        else if(new[]{"candy-bonzi-validation","campaign-validation"}.Contains(SessionState.GetString("RWValidationFolder","")))stage=20;
                        else if(AcademyRun){MainMenuUI.Instance.OpenExtraContent();stage=10;}
                        else { MainMenuUI.Instance.OpenLevelSelect();stage++; }
                        break;
                    case 1:
                        Capture("song-select-1920.png");
                        ValidateLeaderboardPresentation();
                        var ls=UnityEngine.Object.FindAnyObjectByType<LevelSelectUI>();ls.StartSelectedLevel();
                        Check(GameManager.Instance.CurrentState==GameState.Playing&&AudioManager.Instance.CurrentSongClip!=null,"Song select starts loaded audio and gameplay");
                        stage++;break;
                    case 2:
                        if(SessionState.GetString("RWValidationFolder","")!="visual-validation"&&!CaptureEpicStageSequence())break;
                        if(AcademyRun&&!AcademyRescueValidation.Rescue(Check,Capture,SetGameViewSize))
                        { next=EditorApplication.timeSinceStartup+AcademyRescueValidation.PollDelay;break; }
                        Capture("gameplay-1920.png");
                        ValidateVictoryRecord();
                        UnityEngine.Object.FindAnyObjectByType<GameplayUI>().ExitToMainMenu();MainMenuUI.Instance.OpenEditor();
                        ValidateModel(editor);editor.ImportAudio(Path.Combine(Application.dataPath,"Audio","Music","Levels","Sara.mp3"));stage++;break;
                    case 3:
                        if(editor.ActiveChart.audioClip==null){next=EditorApplication.timeSinceStartup+.5;break;}
                        Check(editor.ActiveChart.audioClip.length>40,"Editor reads real PCM audio");
                        editor.Seek(2);stage++;break;
                    case 4:
                        var wave=UnityEngine.Object.FindAnyObjectByType<WaveformRenderer>();
                        Check(wave.HasSamples,"Waveform PCM analysis completed");
                        ValidatePointer(editor);
                        Check(UnityEngine.Object.FindAnyObjectByType<EditorTimeline>().LastVertexCount>100,"Timeline emits grid and note mesh");
                        Capture("editor-1920.png");
                        Check(editor.TestPlay(true),"Test from current cursor starts");
                        results.Add("TEST START: cursor="+editor.PlaybackTime+" songTime="+AudioManager.Instance.SongTime+" dsp="+AudioSettings.dspTime+" offset="+GameSettings.AudioOffsetSec);
                        Check(Math.Abs(AudioManager.Instance.SongTime-(editor.PlaybackTime-1+GameSettings.AudioOffsetSec))<.25,"Test Here preserves scheduled DSP origin at launch");
                        stage++;break;
                    case 5:
                        results.Add("TEST RUN: state="+GameManager.Instance.CurrentState+" songTime="+AudioManager.Instance.SongTime+" playing="+AudioManager.Instance.IsPlaying+" dsp="+AudioSettings.dspTime);
                        Check(AudioManager.Instance.SongTime>1,"Test transport starts at original chart time");
                        UnityEngine.Object.FindAnyObjectByType<GameplayUI>().ExitToMainMenu();
                        Check(GameManager.Instance.CurrentState==GameState.Editor&&Math.Abs(editor.PlaybackTime-2)<.1,"Return from test preserves editor cursor");
                        Check(editor.ActiveChart.notes.Count>0,"Return from test preserves chart");
                        SetGameViewSize(1280,720);stage++;break;
                    case 6:Capture("editor-1280.png");SetGameViewSize(1366,768);stage++;break;
                    case 7:Capture("editor-1366.png");SetGameViewSize(1600,900);stage++;break;
                    case 8:Capture("editor-1600.png");Finish(errors.Count==0,"Runtime smoke completed.");break;
                    case 20:
                        if(CandyBonziValidation.Run(Check,Capture,SetGameViewSize)) { if(SessionState.GetString("RWValidationFolder","")=="campaign-validation")stage=21;else { MainMenuUI.Instance.OpenExtraContent();stage=10; } }
                        break;
                    case 21:
                        if(CampaignValidation.Run(Check,Capture,SetGameViewSize)){ MainMenuUI.Instance.OpenExtraContent();stage=10; }
                        break;
                    case 22:
                        if(BonziDialogueValidation.Run(Check,Capture,SetGameViewSize))Finish(errors.Count==0,"Bonzi dialogue smoke completed.");
                        break;
                    case 23:
                        if(GameplayReadabilityValidation.Run(Check,Capture,SetGameViewSize))Finish(errors.Count==0,"Gameplay readability smoke completed.");
                        break;
                    case 24:
                        if(FiveKeyMigrationValidation.Run(Check,Capture,SetGameViewSize))
                        {if(new[]{"loop-desktop-validation","bonzi-xp-validation","malware-validation","osu-bonzi-validation","comfort-validation","tutorial-validation","sara-complete-validation"}.Contains(SessionState.GetString("RWValidationFolder","")))stage=25;else Finish(errors.Count==0,"Five-key gameplay and SOC migration completed.");}
                        break;
                    case 25:
                        if(EnvironmentPresentationValidation.Run(Check,Capture,SetGameViewSize))
                        {if(new[]{"tutorial-validation","sara-complete-validation"}.Contains(SessionState.GetString("RWValidationFolder","")))stage=32;else if(new[]{"malware-validation","osu-bonzi-validation","comfort-validation","tutorial-validation","sara-complete-validation"}.Contains(SessionState.GetString("RWValidationFolder","")))stage=27;else if(SessionState.GetString("RWValidationFolder","")=="bonzi-xp-validation")stage=26;else Finish(errors.Count==0,"Forward city loop and reactive Bonzi desktop completed.");}
                        break;
                    case 42:
                        if(BetaPolishValidation.Run(Check,Capture,SetGameViewSize)){if(BetaCoreRun)Finish(errors.Count==0,"Beta core regression completed.");else stage=39;}
                        break;
                    case 43:
                        if(OnlineParticipantsValidation.Run(Check,Capture,SetGameViewSize))stage=42;
                        break;
                    case 32:
                        if(GuidedTutorialValidation.Run(Check,Capture,SetGameViewSize))Finish(errors.Count==0,"Guided tutorial, player prototype, clean HUD and configurable keyboard/gamepad validated.");
                        break;
                    case 33:
                        if(GuidedTutorialValidation.SongOnly(Check,Capture,SetGameViewSize))Finish(errors.Count==0,"Player prototype and complete Sara excerpt validated.");
                        break;
                    case 37:
                        if(ScrollCalibrationValidation.Run(Check,Capture,SetGameViewSize))stage=(BetaRun||SessionState.GetString("RWValidationFolder","")=="uniform-spacing-validation")?38:35;
                        break;
                    case 39:
                        if(BossLearningValidation.Run(Check,Capture,SetGameViewSize))stage=BetaRun?41:35;
                        break;
                    case 41:
                        if(SpeedWheelValidation.Run(Check,Capture,SetGameViewSize)){if(BetaRun)stage=37;else Finish(errors.Count==0,"8x reading speed and in-game mouse wheel mixing validated.");}
                        break;
                    case 38:
                        if(BottomLaneValidation.Run(Check,Capture,SetGameViewSize))stage=35;
                        break;
                    case 35:
                        if(ChromaChartValidation.Run(Check,Capture,SetGameViewSize)){if(BetaRun){ChartEditorManager.Instance.CreateNewChart("Tutorial fixture",CampaignCatalog.CreateChart("Sara").audioClipPath,126);ChartEditorManager.Instance.ImportAudio(CampaignCatalog.CreateChart("Sara").audioClipPath);stage=32;}else Finish(errors.Count==0,"Exact Chroma five-key conversion, original audio and all 341 judgments validated.");}
                        break;
                    case 36:
                        if(BottomLaneValidation.Run(Check,Capture,SetGameViewSize))stage=35;
                        break;
                    case 29:
                        if(RhythmComfortValidation.Run(Check,Capture,SetGameViewSize))Finish(errors.Count==0,"Comfortable patterns, measured alignment and explicit malware effects validated.");
                        break;
                    case 28:
                        if(OsuBossChartValidation.Run(Check,Capture,SetGameViewSize))
                        {if(SessionState.GetString("RWValidationFolder","")=="comfort-validation")stage=29;else Finish(errors.Count==0,"Source-timed osu boss adaptation and complete autoplay validated.");}
                        break;
                    case 27:
                        if(MalwareEffectsValidation.Run(Check,Capture,SetGameViewSize))
                        {if(new[]{"osu-bonzi-validation","comfort-validation","tutorial-validation","sara-complete-validation"}.Contains(SessionState.GetString("RWValidationFolder","")))stage=28;else Finish(errors.Count==0,"Malware gameplay consequences, mitigation, presentation and SOC rescue completed.");}
                        break;
                    case 26:
                        if(BonziXPValidation.Run(Check,Capture,SetGameViewSize))Finish(errors.Count==0,"Expanded Bonzi animations, framing and XP presentation completed.");
                        break;
                    case 10:
                        AcademyRescueValidation.Academy(Check);Capture("academy-1920.png");
                        var academy=UnityEngine.Object.FindAnyObjectByType<CyberAcademyUI>();academy.Answer(academy.CurrentQuestion.correctIndex);Capture("academy-feedback-1920.png");
                        SetGameViewSize(1280,720);stage++;break;
                    case 11:Capture("academy-1280.png");SetGameViewSize(1366,768);stage++;break;
                    case 12:Capture("academy-1366.png");SetGameViewSize(1600,900);stage++;break;
                    case 13:
                        Capture("academy-1600.png");UnityEngine.Object.FindAnyObjectByType<ExtraContentUI>().SwitchTab(0);stage++;break;
                    case 14:
                        Capture("threat-library-1600.png");SetGameViewSize(1920,1080);MainMenuUI.Instance.OpenLevelSelect();stage=1;break;
                }
            }
            catch(Exception ex){Finish(false,ex.ToString());}
        }
        static int stageCapture;
        static readonly List<NoteController> stageNotes=new List<NoteController>();
        static readonly List<float> stageProgress=new List<float>();
        static bool CaptureEpicStageSequence()
        {
            if(stageCapture==4)return true;
            if(stageCapture==0&&stageNotes.Count==0)
            {
                var lanes=LaneController.Instance;
                Check(lanes.UsesEpicStage&&BackgroundManager.Instance.StageArtwork!=null,"User PNG loaded as permanent stage artwork");
                Check(BackgroundManager.Instance.StageArtwork.texture.width==1920&&BackgroundManager.Instance.StageArtwork.texture.height==1080,"Stage keeps original PNG resolution");
                if(SessionState.GetString("RWValidationFolder","")=="fluid-validation")
                {
                    var fluid=UnityEngine.Object.FindAnyObjectByType<FluidBackdrop>();
                    Check(fluid!=null && fluid.AnimationMaterial.shader.isSupported,"Fluid shader imported and supported by GPU");
                    Check(fluid.ArtworkMaterial.GetFloat("_StageMode")==1,"PNG uses non-destructive black transparency and brighter road material");
                    BackgroundManager.Instance.UpdateDimOverlay(.35f); // Controlled screenshot fixture; never writes PlayerPrefs.
                }
                for(int lane=0;lane<7;lane++)
                {
                    Check(Vector3.Distance(lanes.GetHitboxPosition(lane),EpicStageLayout.ReceptorPosition(lane))<.001f,"Receptor "+CyberUI.LaneNames[lane]+" aligned to artwork");
                    Check(Vector3.Distance(lanes.EvaluateLanePosition(lane,0),lanes.GetSpawnPosition())<.001f&&Vector3.Distance(lanes.EvaluateLanePosition(lane,1),lanes.GetHitboxPosition(lane))<.001f,"Route "+CyberUI.LaneNames[lane]+" keeps exact spawn/impact endpoints");
                    float previous=0;
                    for(int i=0;i<=100;i++)
                    {
                        float scale=lanes.EvaluateLaneScale(lane,i/100f).x;
                        if(scale+.0001f<previous)throw new Exception("Non-monotonic stage perspective scale");previous=scale;
                        var p=lanes.EvaluateLanePosition(lane,i/100f);var end=lanes.GetHitboxPosition(lane);
                        CheckRoadPoint(p,end,lanes.GetSpawnPosition(),lane);
                    }
                }
                Check(true,"Seven routes stay on central road/side exits and grow continuously");
                var presenter=UnityEngine.Object.FindAnyObjectByType<EpicStagePresenter>();
                Check(presenter.BossAnchor!=null&&presenter.BossAnchor.position.y>presenter.EmissionOrigin.position.y,"Empty boss anchor reserved behind emission origin");
                NoteSpawner.Instance.StopAndClear();
                for(int lane=0;lane<7;lane++)CreateStageNote(lane,.82f,0);
                CreateStageNote(6,.07f,0);CreateStageNote(6,.4f,0);
                CreateStageNote(1,.85f,1.3);
                var hold=stageNotes.Last().GetComponent<LineRenderer>();
                Check(hold.positionCount==3,"Hold body includes exact road exit corner");
                return false; // Let Destroy finish clearing real gameplay notes.
            }
            for(int i=0;i<stageNotes.Count;i++)
            {
                stageNotes[i].Data.timestamp=AudioManager.Instance.SongTime+1.5*(1-stageProgress[i]);
                stageNotes[i].UpdatePosition(AudioManager.Instance.SongTime);
            }
            GameManager.Instance.RegisterHit(stageCapture%3==0?HitRating.Perfect:stageCapture%3==1?HitRating.Good:HitRating.Miss,stageCapture%7);
            var terminal=UnityEngine.Object.FindObjectsByType<TerminalFeedback>().First(t=>t.transform.name.StartsWith("Hitbox_Lane_"+(stageCapture%7)+"_"));
            var glow=terminal.transform.Find("ImpactGlow").GetComponent<SpriteRenderer>();
            Check(glow.color.a>.8f,"Immediate judgement glow rendered at receptor");
            string[] files={"stage-1920.png","stage-1280.png","stage-1366.png","stage-1600.png"};
            Capture(files[stageCapture]);
            stageCapture++;
            if(stageCapture<4)
            {
                int[] widths={1920,1280,1366,1600},heights={1080,720,768,900};
                SetGameViewSize(widths[stageCapture],heights[stageCapture]);return false;
            }
            foreach(var note in stageNotes)UnityEngine.Object.Destroy(note.gameObject);stageNotes.Clear();
            BackgroundManager.Instance.UpdateDimOverlay(GameSettings.BackgroundDim);
            SetGameViewSize(1920,1080);return false;
        }
        static void CheckRoadPoint(Vector3 point,Vector3 end,Vector3 origin,int lane)
        {
            if(lane==6)
            {
                // The drawn SPACE bar center is 10px left of the road opening.
                float depth=Mathf.InverseLerp(origin.y,end.y,point.y);
                if(Mathf.Abs(point.x-Mathf.Lerp(origin.x,end.x,depth))>.001f)throw new Exception("Central note left its road");
                return;
            }
            bool trunk=Mathf.Abs(point.x-origin.x)<.001f,branch=Mathf.Abs(point.y-end.y)<.001f;
            if(!trunk&&!branch)throw new Exception("Stage note left its road");
        }
        static void CreateStageNote(int lane,float progress,double duration)
        {
            var go=new GameObject("StageValidationNote");go.AddComponent<SpriteRenderer>();var note=go.AddComponent<NoteController>();
            note.Initialize(new NoteData{laneIndex=lane,timestamp=AudioManager.Instance.SongTime+1.5*(1-progress),speedMultiplier=1,duration=duration},1.5);
            stageNotes.Add(note);stageProgress.Add(progress);
        }
        static void ValidateModel(ChartEditorManager editor)
        {
            editor.OnStatus+=status=>results.Add("EDITOR STATUS: "+status);
            editor.CreateNewChart("Validation","",120);editor.ActiveChart.offsetMs=125;editor.SetBeatSnap(BeatSnap.Snap_1_3);
            Check(Math.Abs(editor.Quantize(.31)-.2916666667)<.0001,"Ternary snap includes 125ms offset");
            editor.AddOrReplaceNote(1,0);editor.AddOrReplaceNote(3,6,1,2);
            editor.History.Undo();Check(editor.ActiveChart.notes.Count==1,"Undo hold creation");
            editor.History.Redo();Check(editor.ActiveChart.notes.Count==2,"Redo hold creation");
            editor.Selection.SelectMany(editor.ActiveChart.notes);editor.Selection.Copy();editor.Selection.Paste(editor.History,6);
            Check(editor.ActiveChart.notes.Count==4&&editor.Selection.Count==2,"Multi-selection copy and paste relative timing");
            editor.ApplyProperties(n=>{n.speedMultiplier=1.25f;n.laneIndex=3;});editor.History.Undo();
            Check(editor.Selection.Notes.Any(n=>n.laneIndex==6),"Undo grouped property change");
            editor.Selection.Delete(editor.History);Check(editor.ActiveChart.notes.Count==2,"Delete selected group");
            editor.History.Undo();Check(editor.ActiveChart.notes.Count==4,"Undo group deletion");
            string chartPath=Path.Combine(Output,"validation-chart.json");
            Check(editor.SaveToPath(chartPath,true),"Chart save with additive IDs");
            var roundtrip=ChartData.FromJson(File.ReadAllText(chartPath));
            Check(roundtrip.StableId==editor.ActiveChart.StableId&&roundtrip.notes.Count==4&&roundtrip.notes[1].duration==2,"JSON roundtrip retains ID, timestamps and holds");
            Check(!editor.SaveToPath(chartPath),"Overwrite refused without confirmation");
            Check(editor.LoadChartFromJson(chartPath),"Open saved chart");
            string scoresPath=Path.Combine(Output,"validation-scores.json");if(File.Exists(scoresPath))File.Delete(scoresPath);
            var provider=new LocalLeaderboardProvider(scoresPath);Check(provider.GetScores("test","Normal").Count==0,"Leaderboard empty state");
            provider.Submit(new LeaderboardScore{chartId="test",difficultyId="Normal",playerName="Validation",score=42,accuracy=98.5f,maxCombo=10,rank="S",timestamp=DateTime.UtcNow.ToString("o")});
            Check(new LocalLeaderboardProvider(scoresPath).GetScores("test","Normal")[0].accuracy==98.5f,"Leaderboard persists accuracy and reloads");
            Check(provider.GetScores("test","Hard").Count==0,"Leaderboard isolated by difficulty ID");
            var legacy=ChartData.FromJson("{\"songTitle\":\"Legacy\",\"artist\":\"Artist\",\"bpm\":120,\"notes\":[]}");
            string legacyId=legacy.StableId;legacy.songTitle="Renamed";
            Check(legacy.StableId==legacyId,"Legacy chart identity survives rename after import");
            var dense=ScriptableObject.CreateInstance<ChartData>();for(int i=0;i<10000;i++)dense.notes.Add(new NoteData{timestamp=i*.01,laneIndex=i%7});
            var index=new EditorNoteIndex();var watch=System.Diagnostics.Stopwatch.StartNew();index.Rebuild(dense);
            var window=new HashSet<NoteData>();index.Query(20,21,window);watch.Stop();
            Check(window.Count<=102&&window.Count>=100,"10,000-note chart query is viewport bounded");
            results.Add("MODEL BENCHMARK: build index + 1s query = "+watch.Elapsed.TotalMilliseconds.ToString("F2")+" ms, visible="+window.Count);
            UnityEngine.Object.Destroy(dense);UnityEngine.Object.Destroy(legacy);
        }
        static void ValidateLeaderboardPresentation()
        {
            var original=Leaderboards.Provider;
            var chart=ScriptableObject.CreateInstance<ChartData>();chart.chartId="rw-validation-ui";chart.difficultyId="Normal";
            string file=Path.Combine(Output,"leaderboard-ui-fixture.json");if(File.Exists(file))File.Delete(file);
            var panel=UnityEngine.Object.FindAnyObjectByType<LeaderboardPanelUI>();
            try
            {
                var provider=new LocalLeaderboardProvider(file);Leaderboards.Provider=provider;
                for(int i=0;i<15;i++)provider.Submit(new LeaderboardScore{chartId=chart.chartId,difficultyId="Normal",playerName="Validation "+(i+1),score=1284400-i*12345,accuracy=98.42f-i*.5f,maxCombo=436-i*5,rank=i<2?"S":"A",timestamp=DateTime.UtcNow.ToString("o")});
                panel.Refresh(chart);Capture("leaderboard-fixture-1920.png");
                Check(panel.GetComponentsInChildren<LeaderboardEntryUI>().Length==9,"Leaderboard renders bounded pool for 15 fixture scores");
                var scroll=panel.GetComponentInChildren<UnityEngine.UI.ScrollRect>();scroll.verticalNormalizedPosition=0;
                // ScrollRect normally emits this in LateUpdate; this synchronous
                // capture runs before that frame, so drive its real listener now.
                scroll.onValueChanged.Invoke(Vector2.zero);
                Canvas.ForceUpdateCanvases();Capture("leaderboard-scroll-fixture-1920.png");
                Check(panel.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Any(t=>t.text=="15"),"Leaderboard scroll exposes final fixture record");
            }
            finally
            {
                Leaderboards.Provider=original;chart.chartId="rw-official-0";panel.Refresh(chart);UnityEngine.Object.Destroy(chart);
            }
        }
        static void ValidateVictoryRecord()
        {
            var gm=GameManager.Instance;var chart=ChartManager.Instance.SelectedChart;
            string scoreKey=$"HighScore_{chart.songTitle}_{chart.difficulty}",rankKey=$"HighRank_{chart.songTitle}_{chart.difficulty}",comboKey=$"MaxCombo_{chart.songTitle}_{chart.difficulty}";
            bool hasScore=PlayerPrefs.HasKey(scoreKey),hasRank=PlayerPrefs.HasKey(rankKey),hasCombo=PlayerPrefs.HasKey(comboKey);
            int oldScore=PlayerPrefs.GetInt(scoreKey),oldCombo=PlayerPrefs.GetInt(comboKey);string oldRank=PlayerPrefs.GetString(rankKey);
            var originalProvider=Leaderboards.Provider;
            string path=Path.Combine(Output,"victory-record-fixture.json");if(File.Exists(path))File.Delete(path);
            try
            {
                Leaderboards.Provider=new LocalLeaderboardProvider(path);gm.ResetStats();
                gm.RegisterHit(HitRating.Perfect,0);gm.RegisterHit(HitRating.Good,1);gm.RegisterHit(HitRating.Miss,2);
                Check(gm.CurrentScore==1520&&gm.MaxCombo==2&&gm.CurrentHealth==85,"Existing score, combo and damage rules preserved");
                gm.TriggerVictory();var scores=Leaderboards.Provider.GetScores(chart.StableId,chart.DifficultyId);
                Check(scores.Count==1&&scores[0].score==1520&&scores[0].accuracy==50&&scores[0].maxCombo==2,"Victory submits actual gameplay result to provider");
                UnityEngine.Object.FindAnyObjectByType<LeaderboardPanelUI>(FindObjectsInactive.Include).Refresh(chart);
            }
            finally
            {
                Leaderboards.Provider=originalProvider;
                if(hasScore)PlayerPrefs.SetInt(scoreKey,oldScore);else PlayerPrefs.DeleteKey(scoreKey);
                if(hasRank)PlayerPrefs.SetString(rankKey,oldRank);else PlayerPrefs.DeleteKey(rankKey);
                if(hasCombo)PlayerPrefs.SetInt(comboKey,oldCombo);else PlayerPrefs.DeleteKey(comboKey);PlayerPrefs.Save();
            }
        }
        static void ValidatePointer(ChartEditorManager editor)
        {
            var timeline=UnityEngine.Object.FindAnyObjectByType<EditorTimeline>();var rect=(RectTransform)timeline.transform;
            Vector3[] corners=new Vector3[4];rect.GetWorldCorners(corners);
            float x=corners[0].x+(float)((4-timeline.WindowStart)/timeline.WindowSpan)*rect.rect.width;
            float y=corners[0].y+rect.rect.height*.76f*(1-.5f/7);
            var e=new PointerEventData(EventSystem.current){position=new Vector2(x,y),button=PointerEventData.InputButton.Left};
            int count=editor.ActiveChart.notes.Count;timeline.OnPointerDown(e);timeline.OnPointerUp(e);
            Check(editor.ActiveChart.notes.Count==count+1&&editor.ActiveChart.notes.Any(n=>n.laneIndex==0&&Math.Abs(n.timestamp-4)<.2),"Pointer click adds note in A track with snap");
            var added=editor.Selection.Notes.First();double original=added.timestamp;
            timeline.OnPointerDown(e);timeline.OnBeginDrag(e);
            var move=new PointerEventData(EventSystem.current){position=new Vector2(x+rect.rect.width*(float)(.5/timeline.WindowSpan),y-rect.rect.height*.76f/7),button=PointerEventData.InputButton.Left};
            timeline.OnDrag(move);timeline.OnEndDrag(move);
            Check(added.laneIndex==1&&added.timestamp>original+.4,"Pointer drag changes note time and lane");
            editor.History.Undo();Check(added.laneIndex==0&&Math.Abs(added.timestamp-original)<.001,"Undo pointer drag restores time and lane");
            var hold=editor.ActiveChart.notes.First(n=>n.isHoldNote&&n.laneIndex==6);
            double oldDuration=hold.duration;
            float tailX=corners[0].x+(float)((hold.endTimestamp-timeline.WindowStart)/timeline.WindowSpan)*rect.rect.width;
            float holdY=corners[0].y+rect.rect.height*.76f*(1-3.5f/7);
            var tail=new PointerEventData(EventSystem.current){position=new Vector2(tailX,holdY),button=PointerEventData.InputButton.Left};
            timeline.OnPointerDown(tail);timeline.OnBeginDrag(tail);
            tail.position+=new Vector2(rect.rect.width*(float)(1/timeline.WindowSpan),0);timeline.OnDrag(tail);timeline.OnEndDrag(tail);
            Check(hold.duration>oldDuration+.8,"Pointer tail drag resizes hold");
            editor.History.Undo();Check(Math.Abs(hold.duration-oldDuration)<.001,"Undo hold resize");
            timeline.Zoom(.5f);Check(Math.Abs(timeline.WindowSpan-6)<.001,"Horizontal zoom changes visible window");
            timeline.SetWindow(0,12);
        }
        static void Capture(string name)
        {
            // Batch Mode does not draw the Game View framebuffer. Render the real
            // scene and Canvas into a URP render request at the target resolution.
            var cam=Camera.main;var canvas=UnityEngine.Object.FindAnyObjectByType<Canvas>();
            var target=new RenderTexture(SessionState.GetInt("RWCaptureWidth",1920),SessionState.GetInt("RWCaptureHeight",1080),24,RenderTextureFormat.ARGB32);target.Create();
            var oldTarget=cam.targetTexture;var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;
            int oldSorting=canvas.sortingOrder;
            var previous=RenderTexture.active;
            cam.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=cam.nearClipPlane+.1f;
            canvas.sortingOrder=10000; // Match ScreenSpaceOverlay: UI draws above world sprites.
            Canvas.ForceUpdateCanvases();
            foreach(var graphic in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsSortMode.None))
                graphic.Rebuild(UnityEngine.UI.CanvasUpdate.PreRender);
            var timeline=UnityEngine.Object.FindAnyObjectByType<EditorTimeline>();
            if(timeline!=null)results.Add("TIMELINE: verts="+timeline.LastVertexCount+" cull="+timeline.canvasRenderer.cull+" alpha="+timeline.canvasRenderer.GetAlpha()+" bounds="+timeline.rectTransform.rect);
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=target});
            RenderTexture.active=target;var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
            if(name=="stage-1920.png" && SessionState.GetString("RWValidationFolder","")=="fluid-validation")
            {
                var material=UnityEngine.Object.FindAnyObjectByType<FluidBackdrop>().AnimationMaterial;
                float savedTime=material.GetFloat("_VisualTime"),savedBeat=material.GetFloat("_Beat");
                try
                {
                    var road=cam.WorldToViewportPoint(EpicStageLayout.PixelToWorld(new Vector2(1050,750)));
                    Color roadColor=texture.GetPixel((int)(road.x*target.width),(int)(road.y*target.height));
                    Check(roadColor.grayscale>.18f,"Road surface remains clearly brighter at default 35 percent dim");
                    var artwork=BackgroundManager.Instance.transform.Find("EpicStageArtwork").GetComponent<SpriteRenderer>();
                    Color behind;
                    try
                    {
                        artwork.enabled=false;
                        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=target});
                        RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
                        behind=texture.GetPixel((int)(road.x*target.width),(int)(road.y*target.height));
                    }
                    finally { artwork.enabled=true; }
                    Check(Mathf.Abs(behind.grayscale-roadColor.grayscale)>.08f,"Asphalt remains opaque and distinct from the flowing background");
                    material.SetFloat("_Beat",0);material.SetFloat("_VisualTime",0);
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=target});
                    RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
                    var before=texture.GetPixels();File.WriteAllBytes(Path.Combine(Output,"fluid-time-00.png"),texture.EncodeToPNG());
                    material.SetFloat("_VisualTime",12);
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=target});
                    RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
                    var after=texture.GetPixels();float delta=0;int count=0;
                    // Empty upper stage area: excludes HUD and gameplay fixtures.
                    for(int y=target.height/2;y<target.height*4/5;y+=12)for(int x=target.width/8;x<target.width*7/8;x+=12)
                    { int index=y*target.width+x;delta+=Mathf.Abs(before[index].r-after[index].r)+Mathf.Abs(before[index].g-after[index].g)+Mathf.Abs(before[index].b-after[index].b);count++; }
                    Check(delta/count>.015f,"GPU pixels change across 12 seconds of fluid animation");
                    results.Add("FLUID PIXELS: mean RGB delta="+(delta/count).ToString("F4")+" road luminance="+roadColor.grayscale.ToString("F3"));
                    File.WriteAllBytes(Path.Combine(Output,"fluid-time-12.png"),texture.EncodeToPNG());
                }
                finally { material.SetFloat("_VisualTime",savedTime);material.SetFloat("_Beat",savedBeat); }
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=target});
                RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
            }
            if(new[]{"arena-validation","loop-desktop-validation","bonzi-xp-validation","malware-validation","osu-bonzi-validation","comfort-validation","tutorial-validation","sara-complete-validation"}.Contains(SessionState.GetString("RWValidationFolder","")))
            {
                float visible=0;for(int y=10;y<texture.height;y+=80)for(int x=10;x<texture.width;x+=80)visible+=texture.GetPixel(x,y).grayscale;
                Check(visible>1,"Native render contains visible content: "+name);
            }
            File.WriteAllBytes(Path.Combine(Output,name),texture.EncodeToPNG());
            results.Add("CAPTURE: "+name+" "+texture.width+"x"+texture.height);
            cam.targetTexture=oldTarget;canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.sortingOrder=oldSorting;RenderTexture.active=previous;
            target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(texture);Canvas.ForceUpdateCanvases();
        }
        static void SetGameViewSize(int width,int height)
        {
            SessionState.SetInt("RWCaptureWidth",width);SessionState.SetInt("RWCaptureHeight",height);
            var assembly=typeof(UnityEditor.Editor).Assembly;
            var sizesType=assembly.GetType("UnityEditor.GameViewSizes");var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes=singleton.GetProperty("instance").GetValue(null);var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new object[]{GameViewSizeGroupType.Standalone});
            var sizeType=assembly.GetType("UnityEditor.GameViewSize");var sizeEnum=assembly.GetType("UnityEditor.GameViewSizeType");
            var size=Activator.CreateInstance(sizeType,new[]{Enum.ToObject(sizeEnum,1),(object)width,height,"RW validation"});
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
            int index=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null)-1;
            var gameView=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
            gameView.GetType().GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(gameView,index);
            gameView.Repaint();
        }
        static void Finish(bool passed,string message)
        {
            if(originalInputSettings!=null)UnityEngine.InputSystem.InputSystem.settings=originalInputSettings;
            if(validationInputSettings!=null)UnityEngine.Object.Destroy(validationInputSettings);
            BetaPolishValidation.Restore();
            GuidedTutorialValidation.Restore();
            ChromaChartValidation.Restore();
            BossLearningValidation.Restore();
            SpeedWheelValidation.Restore();
            ScrollCalibrationValidation.Restore();
            BottomLaneValidation.Clear();
            RhythmComfortValidation.Restore();
            OsuBossChartValidation.Restore();
            MalwareEffectsValidation.Restore();
            FiveKeyMigrationValidation.Restore();
            AcademyRescueValidation.Restore();
            SessionState.SetBool("RWVisualValidation",false);EditorApplication.update-=Tick;
            Application.logMessageReceived-=Log;File.WriteAllText(Path.Combine(Output,"runtime-report.txt"),(passed?"PASS":"FAIL")+"\n"+message+"\n"+string.Join("\n",results)+"\nERRORS\n"+string.Join("\n",errors)+"\nEDITOR INFRASTRUCTURE ISSUES\n"+string.Join("\n",editorIssues));
            EditorApplication.Exit(passed?0:1);
        }
    }
}
#endif

