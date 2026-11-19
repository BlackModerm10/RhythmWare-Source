#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using TMPro;
using RhythmWare.Core;
using RhythmWare.Rhythm;
using RhythmWare.UI;
using RhythmWare.Education;
using RhythmWare.Malware;
using RhythmWare.Boss;

namespace RhythmWare.EditorTools
{
    public static class FiveKeyMigrationValidation
    {
        static int step;
        static ChartData sara,boss;
        static string output,legacyPath,legacyText;
        static double frozenClock;
        static readonly List<NoteController> notes=new List<NoteController>();
        static readonly List<float> progress=new List<float>();
        static bool reduced;
        static bool initialized;
        public static void Restore(){if(initialized)VisualPreferences.ReducedEffects=reduced;}
        public static bool Run(Action<bool,string> check,Action<string> capture,Action<int,int> size)
        {
            var gm=GameManager.Instance;var editor=ChartEditorManager.Instance;var lanes=LaneController.Instance;
            switch(step)
            {
                case 0:
                    output=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"docs",UnityEditor.SessionState.GetString("RWValidationFolder","five-key-validation"));Directory.CreateDirectory(output);
                    string fixturePath=Path.Combine(output,"soc-fixture.json");if(File.Exists(fixturePath))File.Delete(fixturePath);
                    SocLearningProgress.SetValidationPath(fixturePath);reduced=VisualPreferences.ReducedEffects;initialized=true;VisualPreferences.ReducedEffects=false;
                    ValidateData(check);sara=CampaignCatalog.CreateChart("Sara");boss=CampaignCatalog.CreateChart("Dinner");
                    MainMenuUI.Instance.OpenEditor();editor.OpenCatalogChart(sara);step++;return false;
                case 1:
                    if(editor.ActiveChart.audioClip==null)return false;
                    check(editor.ActiveChart.audioClip.length>150,"Campaign resolves and loads the original Sara audio in the relocated project");
                    ValidateEditor(check,editor);capture("editor-five-keys-1920.png");
                    editor.OpenCatalogChart(sara);step++;return false;
                case 2:
                    if(editor.ActiveChart.audioClip==null)return false;
                    var chart=UnityEngine.Object.Instantiate(boss);chart.audioClip=editor.ActiveChart.audioClip;chart.notes=new List<NoteData>();
                    GameplayUI.IsTestPlayFromEditor=true;ChartManager.Instance.SelectChart(chart);NoteSpawner.Instance.LoadChart(chart);
                    AudioManager.Instance.PlaySong(chart.audioClip,1,20);gm.StartGame();AudioManager.Instance.PauseSong();
                    ValidateRoutes(check);ValidateInput(check);
                    for(int lane=0;lane<LaneLayout.Count;lane++)Spawn(lane,lane==4?.72f:lane%2==0?.79f:.84f,lane==1?ScriptableObject.CreateInstance<AdwareData>():lane==3?ScriptableObject.CreateInstance<TrojanData>():lane==2?boss.notes.First(n=>n.malwareData is SpywareData).malwareData:null,lane==1?.5:0);
                    step++;return false;
                case 3:
                    check(UnityEngine.Object.FindAnyObjectByType<FiveLaneStageVisual>()!=null&&UnityEngine.Object.FindAnyObjectByType<FiveLaneStageVisual>().GetComponentsInChildren<MeshFilter>().Length==5,"Independent road consists of one highway and four side-exit surfaces");
                    check(UnityEngine.Object.FindAnyObjectByType<FluidBackdrop>().Theme=="bonzi-core","Chart selects a visible independent background theme");
                    check(notes.All(n=>n.UsesCharacterAnimation&&n.SpriteRenderer.color.r==1&&n.SpriteRenderer.color.g==1&&n.SpriteRenderer.color.b==1),"Original enemy animations and RGB remain intact");
                    check(BonziStageController.Instance.IsEncounter,"Bonzi is active on the new five-key board");
                    ArenaPresentationValidation.Check(check,notes);capture("bonzi-five-keys-1920.png");CheckGPU(check);UnityEngine.Object.FindAnyObjectByType<GameplayHUD>().Overclock(20,true);size(1280,720);step++;return false;
                case 4:
                    for(int i=0;i<notes.Count;i++)notes[i].UpdatePosition(notes[i].TargetTimestamp-notes[i].TravelDuration*(1-progress[i]));
                    ValidateRoutes(check);ArenaPresentationValidation.CheckHUD(check);capture("bonzi-five-keys-1280.png");
                    var hud=UnityEngine.Object.FindAnyObjectByType<GameplayHUD>();hud.Health(20,100);hud.Overclock(20,true);RhythmWare.Malware.MalwareEffectManager.Instance.ShowAlertBanner("SPYWARE INFILTRADO / RADAR ALTERADO",CyberUI.Magenta,1);Canvas.ForceUpdateCanvases();capture("arena-low-firewall-1280.png");hud.Health(gm.CurrentHealth,100);hud.Overclock(0,false);
                    var backdrop=UnityEngine.Object.FindAnyObjectByType<FluidBackdrop>();frozenClock=backdrop.VisualClock;gm.SetState(GameState.Paused);step++;return false;
                case 5:
                    check(UnityEngine.Object.FindAnyObjectByType<FluidBackdrop>().VisualClock==frozenClock,"Pause freezes the visual loop clock");
                    VisualPreferences.ReducedEffects=true;gm.SetState(GameState.Playing);
                    notes[0].OnHit(HitRating.Perfect);int hitScore=gm.CurrentScore;
                    var hitbox=UnityEngine.Object.FindObjectsByType<TerminalFeedback>().First(f=>Math.Abs(f.transform.position.x-lanes.GetHitboxPosition(0).x)<.001&&Math.Abs(f.transform.position.y-lanes.GetHitboxPosition(0).y)<.001);
                    check(hitbox.transform.Find("ImpactGlow").GetComponent<SpriteRenderer>().color.a>.5f,"Correct hits keep their receptor glow on the new stage");
                    float expectedHealth=gm.CurrentHealth-15*notes[1].Data.malwareData.missDamageMultiplier;
                    notes[1].OnHit(HitRating.Miss);check(notes[1].IsMissed&&gm.CurrentScore==hitScore&&Math.Abs(gm.CurrentHealth-expectedHealth)<.01,"A Miss is penalized instead of granting a malware neutralization reward");
                    step++;return false;
                case 6:
                    check(UnityEngine.Object.FindAnyObjectByType<FluidBackdrop>().VisualClock==frozenClock,"Reduced effects disables background motion");
                    VisualPreferences.ReducedEffects=false;Clear();size(1920,1080);gm.SetState(GameState.MainMenu);MainMenuUI.Instance.OpenExtraContent();step++;return false;
                case 7:
                    var academy=UnityEngine.Object.FindAnyObjectByType<CyberAcademyUI>();
                    check(academy!=null&&UnityEngine.Object.FindAnyObjectByType<ExtraContentUI>().MalwareCount==5,"SOC exposes exactly five relevant threat topics");
                    academy.SelectModule("Todos");academy.RestartQuiz();academy.SelectModule("Spyware");check(academy.VisibleCount==4,"Each educational module has four distinct cases");
                    var q=academy.CurrentQuestion;int wrong=Enumerable.Range(0,3).First(i=>i!=q.correctIndex);academy.Answer(wrong);
                    check(SocLearningProgress.Data.attempts.Last().selectedOption==wrong&&!SocLearningProgress.Data.attempts.Last().correct,"Wrong answers retain their canonical option and learning feedback");
                    int count=SocLearningProgress.Data.attempts.Count;academy.RestartQuiz();check(SocLearningProgress.Data.attempts.Count==count,"Repeating practice preserves attempt history");
                    q=academy.CurrentQuestion;int shown=Enumerable.Range(0,3).First(i=>academy.QuestionCard.CanonicalOption(i)==q.correctIndex);academy.QuestionCard.Options[shown].onClick.Invoke();
                    check(SocLearningProgress.Data.attempts.Last().correct&&!SocLearningProgress.Data.attempts.Last().firstAttempt,"Shuffled answer buttons map back correctly without rewriting first-attempt results");
                    capture("soc-feedback-1920.png");
                    foreach(var question in CyberQuestionBank.GetAllQuestions()){academy.SelectModule(question.relatedThreat);academy.QuestionCard.Bind(question,"VALIDACIÓN DE TEXTO");for(int option=0;option<3;option++){academy.QuestionCard.Reveal(option);Canvas.ForceUpdateCanvases();CheckText(check,academy.QuestionCard.Options[0].transform.parent.parent.gameObject,"Case "+question.id+" option "+option);}}
                    SocLearningProgress.SetValidationPath(Path.Combine(output,"soc-fixture.json"));check(SocLearningProgress.Data.attempts.Count==count+1,"SOC history survives a disk reload");
                    academy.SelectModule("Ransomware");size(1280,720);step++;return false;
                case 8:
                    capture("soc-ransomware-1280.png");
                    MainMenuUI.Instance.BackToMainMenu();SocLearningFlow.Instance.Briefing(boss,()=>{},()=>{});step++;return false;
                case 9:
                    check(SocLearningFlow.Instance.IsOpen&&SocLearningProgress.Data.seen.Count>=8,"Ransomware and DDoS lessons are available before the boss without a skill gate");
                    capture("boss-briefing-1280.png");SocLearningFlow.Instance.Close();SocLearningFlow.Instance.Debrief(sara,()=>gm.SetState(GameState.MainMenu));
                    check(gm.CurrentState==GameState.Learning,"Debrief is independent of rhythm gameplay");SocLearningFlow.Instance.Answer(SocLearningFlow.Instance.CurrentQuestion.correctIndex);step++;return false;
                case 10:
                    capture("soc-debrief-1280.png");SocLearningFlow.Instance.Finish();
                    ChartManager.Instance.SelectChart(sara);NoteSpawner.Instance.LoadChart(sara);NoteSpawner.Instance.StopAndClear();sara.audioClip=editor.ActiveChart.audioClip;
                    AudioManager.Instance.PlaySong(sara.audioClip,1,10);gm.StartGame();GameplayUI.IsTestPlayFromEditor=false;
                    for(int i=0;i<20;i++)gm.RegisterHit(HitRating.Perfect,i%LaneLayout.Count);
                    SocLearningProgress.LastThreat="Spyware";gm.TakeDamage(1000);
                    check(gm.CurrentState==GameState.RescueQuestion&&gm.RescueQuestion.relatedThreat=="Spyware","Overclock rescue uses a relevant SOC topic on the five-key layout");
                    gm.AnswerRescue(gm.RescueQuestion.correctIndex);check(gm.CurrentState==GameState.Reviving&&Math.Abs(gm.CurrentHealth-30)<.01,"Correct rescue restores 30 percent before countdown");step++;return false;
                case 11:
                    if(gm.CurrentState==GameState.Reviving)return false;
                    check(gm.CurrentState==GameState.Playing&&gm.HasUsed2FARescue,"Three-second countdown resumes the same attempt once");
                    var saved=JsonUtility.FromJson<SocLearningProgress.Store>(File.ReadAllText(Path.Combine(output,"soc-fixture.json")));
                    check(SocLearningProgress.LastSaveSucceeded&&saved.attempts.Any(a=>a.source=="debrief")&&saved.attempts.Any(a=>a.source=="rescue"),"Debrief and rescue attempts are committed to disk at the end of the flow");
                    GameplayUI.IsTestPlayFromEditor=true;AudioManager.Instance.StopSong();NoteSpawner.Instance.StopAndClear();gm.SetState(GameState.MainMenu);VisualPreferences.ReducedEffects=reduced;
                    return true;
            }
            return false;
        }
        static void ValidateData(Action<bool,string> check)
        {
            check(LaneLayout.Count==5&&LaneLayout.Center==4&&string.Join("/",LaneLayout.Names)=="D/F/J/K/SPACE","Five-key layout is centralized and named consistently");
            for(int i=0;i<5;i++)check(InputManager.Instance.GetKeyForLane(i)==LaneLayout.DefaultKeys[i],"Default binding "+LaneLayout.Names[i]);
            check(InputManager.Instance.GetKeyForLane(5)==Key.None,"Removed sixth and seventh input slots cannot trigger gameplay");
            foreach(string key in new[]{"Sara","Jack","Dinner"}){
                var c=CampaignCatalog.CreateChart(key);check(c.laneLayout==LaneLayout.Id&&c.formatVersion==2&&c.StableId.Contains("5keys"),key+" has a new version and leaderboard identity");
                check(c.notes.All(n=>LaneLayout.Valid(n.laneIndex)),key+" contains only five-key notes");
                check(!c.notes.Any(n=>c.notes.Any(m=>m!=n&&m.laneIndex==n.laneIndex&&m.timestamp<=n.timestamp&&m.endTimestamp>=n.timestamp-.09)),key+" has no tap/hold collision after reauthoring");
                check(c.notes.All(n=>n.malwareData==null||n.malwareData is SpywareData||n.malwareData is AdwareData||n.malwareData is TrojanData),key+" limits gameplay to the three active enemy families");
                var loaded=ChartData.FromJson(c.ToJson());check(loaded.laneLayout==LaneLayout.Id&&loaded.notes.Count==c.notes.Count&&loaded.backgroundTheme==c.backgroundTheme&&loaded.approachSeconds==c.approachSeconds,key+" survives current-format JSON roundtrip");
            }
            var tutorial=CampaignCatalog.CreateChart("Sara");
            check(tutorial.notes.Count==201&&tutorial.bpm==126,"Sara has an authored complete 201-note 126 BPM tutorial map");
            check(tutorial.notes.Count(n=>n.isHoldNote)==8&&tutorial.notes.Any(n=>n.laneIndex==4),"Sara gradually teaches holds and the central lane");
            check(tutorial.notes[0].timestamp>3&&tutorial.playableEndSeconds==0&&tutorial.notes.Max(n=>n.endTimestamp)>150,"Sara spans the complete song instead of stopping after the prototype excerpt");
            check(tutorial.tutorialId=="sara-basics-5","Sara remains linked to the guided teaching and SOC topic");
            check(ChartEditorManager.Instance.MalwarePalette.Count==3,"Editor palette includes only supported active enemies");
            var bank=CyberQuestionBank.GetAllQuestions();check(bank.Count==20&&bank.Select(q=>q.objectiveId).Distinct().Count()==20,"SOC has 20 distinct learning objectives and cases");
            foreach(var threat in CyberQuestionBank.CoreThreats){check(bank.Count(q=>q.relatedThreat==threat)==4,threat+" has four cases");check(CyberQuestionBank.GetRandomQuestionForThreat(threat).relatedThreat==threat,threat+" contextual selection");}
            check(bank.Count(q=>q.correctIndex==0)==7&&bank.Count(q=>q.correctIndex==1)==7&&bank.Count(q=>q.correctIndex==2)==6,"Canonical correct positions are balanced before display shuffling");
            check(bank.All(q=>q.options.Length==3&&q.optionFeedback.Length==3&&!string.IsNullOrEmpty(q.lessonText)),"Every case teaches a lesson and explains each option");
            var legacy=ChartData.FromJson("{\"songTitle\":\"Legacy\",\"notes\":[{\"timestamp\":2,\"laneIndex\":1},{\"timestamp\":2,\"laneIndex\":2}]}");
            check(legacy.IsLegacy,"Missing layout metadata is treated as legacy even with only small lane indices");
            var converted=LegacyChartConversion.ConvertCopy(legacy,out string report);check(converted.notes.Count==1&&report.Contains("1 conflictos")&&legacy.notes.Count==2&&converted.StableId!=legacy.StableId,"Explicit legacy conversion preserves original and reports merged conflicts");
            bool rejected=false;try{ChartData.FromJson("{\"laneLayout\":\"branch-5-v1\",\"notes\":[{\"timestamp\":2,\"laneIndex\":6}]}");}catch(InvalidDataException){rejected=true;}check(rejected,"Invalid new-format lanes are rejected before spawning");
        }
        static void ValidateEditor(Action<bool,string> check,ChartEditorManager editor)
        {
            var audio=editor.ActiveChart.audioClip;editor.CreateNewChart("Five-key fixture","Validation",120);editor.SetAudioClip(audio);
            for(int lane=0;lane<5;lane++)editor.AddOrReplaceNote(4+lane,lane,1,lane==4?.7:0);
            check(editor.ActiveChart.notes.Count==5&&editor.ActiveChart.notes.Last().isHoldNote,"Editor authors taps and a central hold across exactly five tracks");
            editor.Selection.SelectMany(editor.ActiveChart.notes);editor.Selection.Copy();editor.Seek(12);editor.Selection.Paste(editor.History,12);check(editor.ActiveChart.notes.Count==10,"Copy/paste preserves five-key notes");editor.History.Undo();check(editor.ActiveChart.notes.Count==5,"Undo restores chart after paste");editor.History.Redo();check(editor.ActiveChart.notes.Count==10,"Redo restores paste");
            string path=Path.Combine(output,"new-five-key.json");check(editor.SaveToPath(path,true)&&editor.LoadChartFromJson(path),"Editor saves and reopens a versioned five-key chart");
            legacyPath=Path.Combine(output,"legacy-original.json");legacyText="{\"songTitle\":\"Legacy\",\"notes\":[{\"timestamp\":2,\"laneIndex\":1},{\"timestamp\":2,\"laneIndex\":2}]}";File.WriteAllText(legacyPath,legacyText);
            check(!editor.LoadChartFromJson(legacyPath),"Legacy map cannot enter the editor without conversion consent");
            check(editor.LoadChartFromJson(legacyPath,true)&&editor.CurrentPath==null&&editor.Dirty,"Converted map opens as an unsaved copy");
            check(!editor.SaveToPath(legacyPath,true)&&File.ReadAllText(legacyPath)==legacyText,"Even explicit save cannot overwrite the protected legacy original");
            check(editor.SaveToPath(Path.Combine(output,"converted-five-key.json"),true),"Converted copy can be saved under a new filename");
        }
        static void ValidateRoutes(Action<bool,string> check)
        {
            var l=LaneController.Instance;float centre=l.GetSpawnPosition().x;
            for(int lane=0;lane<5;lane++){
                bool side=true,grows=true;float previous=0;
                for(int i=0;i<=100;i++){float p=i/100f;var pos=l.EvaluateLanePosition(lane,p);float scale=l.EvaluateLaneScale(lane,p).x;side&=lane==4?Math.Abs(pos.x-centre)<.0001:lane<2?pos.x<centre:pos.x>centre;grows&=scale>=previous-.0001;previous=scale;}
                check(side&&grows,LaneLayout.Names[lane]+" stays on its route and grows toward the player");check(Vector3.Distance(l.EvaluateLanePosition(lane,1),l.GetHitboxPosition(lane))<.0001,LaneLayout.Names[lane]+" arrives at its receptor");
            }
        }
        static void ValidateInput(Action<bool,string> check)
        {
            int pressed=-1,released=-1;Action<int> onPress=i=>pressed=i,onRelease=i=>released=i;InputManager.Instance.OnLanePressed+=onPress;InputManager.Instance.OnLaneReleased+=onRelease;
            var method=typeof(InputManager).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            foreach(int lane in Enumerable.Range(0,5)){
                InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(LaneLayout.DefaultKeys[lane]));InputSystem.Update();method.Invoke(InputManager.Instance,null);
                InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());InputSystem.Update();method.Invoke(InputManager.Instance,null);
                check(pressed==lane&&released==lane,"Press and release events use the correct new index for "+LaneLayout.Names[lane]);
            }
            InputManager.Instance.OnLanePressed-=onPress;InputManager.Instance.OnLaneReleased-=onRelease;
        }
        static void Spawn(int lane,float p,MalwareData malware,double duration)
        {
            var go=new GameObject("FiveKeyFixture");go.AddComponent<SpriteRenderer>();var note=go.AddComponent<NoteController>();note.Initialize(new NoteData{laneIndex=lane,timestamp=AudioManager.Instance.SongTime+1,malwareData=malware,duration=duration},2);
            note.UpdatePosition(note.TargetTimestamp-note.TravelDuration*(1-p));notes.Add(note);progress.Add(p);
        }
        static void Clear(){foreach(var n in notes)if(n!=null)UnityEngine.Object.Destroy(n.gameObject);notes.Clear();progress.Clear();}
        static void CheckText(Action<bool,string> check,GameObject root,string context)
        {
            var failures=new List<string>();foreach(var text in root.GetComponentsInChildren<TextMeshProUGUI>()){text.ForceMeshUpdate();if(text.preferredHeight>text.rectTransform.rect.height+4)failures.Add(text.text.Substring(0,Math.Min(25,text.text.Length))+" "+text.preferredHeight+"/"+text.rectTransform.rect.height);}
            check(failures.Count==0,context+" fits its educational card "+string.Join("; ",failures));
        }
        static void CheckGPU(Action<bool,string> check)
        {
            var cam=Camera.main;var backdrop=UnityEngine.Object.FindAnyObjectByType<FluidBackdrop>();var material=backdrop.AnimationMaterial;
            check(material!=null&&material.shader.isSupported&&material.GetTexture("_CityTex")!=Texture2D.blackTexture,"New city art and its animated shader are available");
            var target=new RenderTexture(320,180,24);target.Create();var old=cam.targetTexture;var active=RenderTexture.active;var tex=new Texture2D(320,180,TextureFormat.RGB24,false);
            float saved=material.GetFloat("_VisualTime");string theme=backdrop.Theme;backdrop.SetTheme("candy-city");
            Color[] Pixels(float t){backdrop.RenderAt(t);cam.targetTexture=target;UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;tex.ReadPixels(new Rect(0,0,320,180),0,0);tex.Apply();return tex.GetPixels();}
            var a=Pixels(0);var b=Pixels(40);var loop=Pixels(480);float difference=0,seam=0;
            for(int y=110;y<150;y+=3)for(int x=10;x<60;x+=3){int i=y*320+x;difference+=Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].b-b[i].b);seam+=Mathf.Abs(a[i].r-loop[i].r)+Mathf.Abs(a[i].b-loop[i].b);}
            check(difference>.1f,"GPU pixels move over time in the independent background");check(seam<.02f,"The complete visual loop closes without a frame jump");
            backdrop.SetTheme(theme);backdrop.RenderAt(saved);cam.targetTexture=old;RenderTexture.active=active;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(tex);
        }
    }
}
#endif
