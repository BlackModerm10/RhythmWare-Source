#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using RhythmWare.Core;
using RhythmWare.Rhythm;
using RhythmWare.UI;
using RhythmWare.Boss;

namespace RhythmWare.EditorTools
{
    public static class CandyBonziValidation
    {
        static int step;
        static ChartData original,boss;
        static Sprite pausedSprite;
        static double pausedTime;
        static string originalJson;
        public static bool Run(Action<bool,string> check,Action<string> capture,Action<int,int> size)
        {
            var menu=MainMenuUI.Instance;var selector=UnityEngine.Object.FindAnyObjectByType<LevelSelectUI>(FindObjectsInactive.Include);
            var editor=ChartEditorManager.Instance;var gm=GameManager.Instance;var audio=AudioManager.Instance;
            var encounter=BonziStageController.Instance;
            switch(step)
            {
                case 0:
                    check(UnityEngine.Object.FindObjectsByType<CandyMenuButton>(FindObjectsSortMode.None).Length==5,"Candy menu preserves all five interactive options");
                    var graphic=UnityEngine.Object.FindAnyObjectByType<CandyMenuGraphic>();
                    Canvas.ForceUpdateCanvases();graphic.Rebuild(CanvasUpdate.PreRender);
                    var mesh=graphic.canvasRenderer.GetMesh();
                    check(mesh!=null&&mesh.vertexCount>100,"Candy backdrop submits visible decorative geometry to CanvasRenderer");
                    menu.FocusMenu(3);check(EventSystem.current.currentSelectedGameObject.GetComponent<CandyMenuButton>()!=null,"Keyboard focus stays on an interactive menu button");
                    menu.FocusMenu(0);capture("candy-menu-1920.png");size(1280,720);step++;return false;
                case 1:capture("candy-menu-1280.png");size(1366,768);step++;return false;
                case 2:capture("candy-menu-1366.png");size(1600,900);step++;return false;
                case 3:
                    capture("candy-menu-1600.png");size(1920,1080);menu.OpenEditor();
                    check(!editor.Dirty,"Unused startup document opens without a save prompt");
                    var blank=editor.ActiveChart;blank.difficultyId="Guard Fixture";editor.MetadataChanged();
                    var ui=UnityEngine.Object.FindAnyObjectByType<ChartEditorUI>();ui.OpenLibrary();
                    var modal=GameObject.Find("Modal");check(modal!=null,"Metadata-only changes require confirmation before opening the library");
                    modal.GetComponentsInChildren<Button>().First(b=>b.GetComponentInChildren<TMPro.TextMeshProUGUI>().text=="CANCELAR").onClick.Invoke();
                    check(editor.ActiveChart==blank&&blank.difficultyId=="Guard Fixture"&&editor.Dirty,"Cancel keeps the existing document and its unsaved metadata");
                    string fixture=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"docs","candy-bonzi-validation","editor-guard-fixture.json");
                    editor.SaveToPath(fixture,true);ui.OpenLibrary();step++;return false;
                case 4:
                    check(UnityEngine.Object.FindAnyObjectByType<ChartLibraryUI>().IsOpen,"Editor exposes map library directly in its toolbar");
                    var entries=selector.RefreshCatalog();check(entries.Count>=5&&entries.Any(e=>e.chartData.bossId==BonziEncounter.Id),"Library includes three campaign maps and custom JSON charts");
                    capture("map-library-1920.png");size(1280,720);step++;return false;
                case 5:
                    capture("map-library-1280.png");size(1920,1080);
                    UnityEngine.Object.FindAnyObjectByType<ChartLibraryUI>().Close();
                    menu.OpenLevelSelect();step++;return false;
                case 6:
                    original=selector.RefreshCatalog().First().chartData;originalJson=original.ToJson();
                    var row=UnityEngine.Object.FindObjectsByType<SongSelectItemUI>(FindObjectsSortMode.None).OrderByDescending(r=>r.transform.localPosition.y).First();
                    var data=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Right,position=new Vector2(300,650)};
                    ExecuteEvents.Execute(row.gameObject,data,ExecuteEvents.pointerClickHandler);
                    check(GameObject.Find("MapContext")!=null,"Real right-click event on a song row opens its context menu");capture("song-context-1920.png");
                    GameObject.Find("VER / EDITAR MAPA").GetComponent<Button>().onClick.Invoke();
                    check(gm.CurrentState==GameState.Editor&&editor.ActiveChart!=original,"Context action opens a separate editable chart instance");
                    check(editor.ActiveChart.notes.Count==original.notes.Count&&editor.ActiveChart.bpm==original.bpm,"Catalog opening preserves notes and metadata");
                    check(editor.ActiveChart.StableId!=original.StableId&&editor.CurrentPath==null,"Official editing forks identity and requires Save As");
                    double timestamp=original.notes[0].timestamp;editor.ActiveChart.notes[0].timestamp+=.125;
                    check(original.notes[0].timestamp==timestamp&&original.ToJson()==originalJson,"Editing cloned note never changes official source data");
                    editor.ActiveChart.notes[0].timestamp-=.125;
                    capture("official-map-editor-1920.png");
                    boss=selector.RefreshCatalog().First(e=>e.chartData.bossId==BonziEncounter.Id).chartData;
                    editor.OpenCatalogChart(boss);step++;return false;
                case 7:
                    if(editor.ActiveChart.audioClip==null)return false;
                    boss.audioClip=editor.ActiveChart.audioClip;
                    check(boss.notes.Count>60&&boss.notes.Any(n=>n.isHoldNote)&&boss.notes.Select(n=>n.laneIndex).Distinct().Count()==7,"Bonzi chart has a complete seven-lane encounter and holds");
                    var roundTrip=ChartData.FromJson(boss.ToJson());check(roundTrip.bossId==BonziEncounter.Id,"Boss identity survives editor JSON save and reload");UnityEngine.Object.Destroy(roundTrip);
                    check(Resources.Load<Material>("Boss/BonziKey").shader.isSupported,"Bonzi transparency shader is supported by the GPU");
                    StartBoss(3);step++;return false;
                case 8:
                    check(encounter.IsEncounter&&encounter.Phase==1&&GameObject.Find("BossStageAnchor").GetComponent<SpriteRenderer>().sprite!=null,"Bonzi actor and first phase appear on the calibrated stage");
                    capture("bonzi-phase-1-1920.png");
                    float before=encounter.Integrity;gm.RegisterHit(HitRating.Good,0);check(encounter.Integrity<before,"Successful gameplay judgements damage the boss core");
                    float afterHit=encounter.Integrity;gm.RegisterHit(HitRating.Miss,0);check(Mathf.Approximately(encounter.Integrity,afterHit),"Miss does not damage the boss");
                    audio.PauseSong();gm.SetState(GameState.Paused);pausedSprite=GameObject.Find("BossStageAnchor").GetComponent<SpriteRenderer>().sprite;pausedTime=audio.SongTime;step++;return false;
                case 9:
                    check(audio.SongTime==pausedTime&&GameObject.Find("BossStageAnchor").GetComponent<SpriteRenderer>().sprite==pausedSprite,"Boss animation and audio remain frozen through pause");
                    StartBoss(boss.audioClip.length*.40);step++;return false;
                case 10:
                    ValidateAdwarePerspective(check);
                    check(encounter.Phase==2,"Encounter transitions to adware phase during the middle third");capture("bonzi-phase-2-1920.png");audio.PauseSong();gm.SetState(GameState.Paused);size(1280,720);step++;return false;
                case 11:
                    capture("bonzi-stage-1280.png");size(1920,1080);StartBoss(boss.audioClip.length*.72);step++;return false;
                case 12:
                    check(encounter.Phase==3,"Encounter transitions to trojan phase during the final third");capture("bonzi-phase-3-1920.png");
                    // Exercise the actual end-of-song gate without writing real
                    // leaderboards: test mode suppresses persistent results.
                    NoteSpawner.Instance.StopAndClear();audio.PlaySong(boss.audioClip,1,boss.audioClip.length-.2);step++;return false;
                case 13:
                    check(gm.CurrentState==GameState.GameOver,"Surviving the song with a live boss cannot grant victory");
                    StartBoss(3);for(int i=0;i<boss.notes.Sum(n=>n.isHoldNote?2:1);i++)gm.RegisterHit(HitRating.Perfect,i%7);
                    check(encounter.IsContained,"Sufficient successful hits reduce Bonzi core to zero");
                    NoteSpawner.Instance.StopAndClear();audio.PlaySong(boss.audioClip,1,boss.audioClip.length-.2);step++;return false;
                case 14:
                    check(gm.CurrentState==GameState.Victory,"Contained boss and completed song grant victory");
                    UnityEngine.Object.FindAnyObjectByType<GameplayUI>().ExitToMainMenu();
                    GameplayUI.IsTestPlayFromEditor=false;menu.BackToMainMenu();menu.OpenLevelSelect();
                    var ordinary=selector.RefreshCatalog().First().chartData;
                    NoteSpawner.Instance.LoadChart(ordinary);check(!encounter.IsEncounter,"Normal charts clear the boss actor and encounter rule");
                    check(original.ToJson()==originalJson,"Legacy chart data remains unchanged after editor and boss flows");
                    return true;
            }
            return false;
        }
        static void StartBoss(double time)
        {
            var copy=UnityEngine.Object.Instantiate(boss);copy.notes=boss.notes.Where(n=>n.timestamp>=time).ToList();
            GameplayUI.IsTestPlayFromEditor=true;ChartManager.Instance.SelectChart(copy);NoteSpawner.Instance.LoadChart(copy);
            AudioManager.Instance.PlaySong(boss.audioClip,1,time);GameManager.Instance.StartGame();
        }
        static void ValidateAdwarePerspective(Action<bool,string> check)
        {
            var malware=ScriptableObject.CreateInstance<RhythmWare.Malware.AdwareData>();
            var go=new GameObject("AdwarePerspectiveFixture",typeof(SpriteRenderer));var note=go.AddComponent<NoteController>();
            var data=new NoteData{laneIndex=0,malwareData=malware,speedMultiplier=1,timestamp=AudioManager.Instance.SongTime+1.4};note.Initialize(data,1.5);
            try
            {
                foreach(float progress in new[]{.1f,.5f,.9f})
                {
                    double time=AudioManager.Instance.SongTime;data.timestamp=time+1.5*(1-progress);note.UpdatePosition(time);
                    float baseline=LaneController.Instance.EvaluateLaneScale(0,progress).x;
                    check(go.transform.localScale.x<baseline*1.5f&&go.transform.localScale.x>baseline*.6f,"Adware keeps projected depth scale at progress "+progress);
                }
            }
            finally { go.SetActive(false);UnityEngine.Object.Destroy(go);UnityEngine.Object.Destroy(malware); }
        }
    }
}
#endif
