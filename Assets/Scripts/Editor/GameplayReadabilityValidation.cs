#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RhythmWare.Core;
using RhythmWare.Rhythm;
using RhythmWare.UI;
using RhythmWare.Malware;
using RhythmWare.Boss;

namespace RhythmWare.EditorTools
{
    public static class GameplayReadabilityValidation
    {
        static int step;
        static readonly List<NoteController> notes=new List<NoteController>();
        static readonly List<float> progress=new List<float>();
        static AdwareData adware;
        static TrojanData trojan;
        static SpywareData spyware;
        static bool reduced;
        static double frozenClock, frozenElapsed;
        static Sprite frozenFrame;
        public static bool Run(Action<bool,string> check,Action<string> capture,Action<int,int> size)
        {
            var lanes=LaneController.Instance;
            switch(step++)
            {
                case 0:
                    reduced=VisualPreferences.ReducedEffects;VisualPreferences.ReducedEffects=false;
                    adware=ScriptableObject.CreateInstance<AdwareData>();trojan=ScriptableObject.CreateInstance<TrojanData>();spyware=ScriptableObject.CreateInstance<SpywareData>();
                    var chart=BonziEncounter.CreateChart();chart.notes.Clear();chart.audioClip=AudioClip.Create("Readability fixture",44100*30,1,44100,false);
                    GameplayUI.IsTestPlayFromEditor=true;ChartManager.Instance.SelectChart(chart);NoteSpawner.Instance.LoadChart(chart);
                    AudioManager.Instance.PlaySong(chart.audioClip,1,8);GameManager.Instance.StartGame();AudioManager.Instance.PauseSong();
                    BackgroundManager.Instance.UpdateDimOverlay(.35f);
                    ValidateRoutes(check);ValidateArt(check);
                    foreach(float p in new[]{.27f,.67f,.97f})for(int lane=0;lane<7;lane++)
                        Spawn(lane,p,lane%3==1?adware:lane%3==2?trojan:null,p==.67f&&lane==1?.4:0);
                    return false;
                case 1:
                    CheckHeads(check);capture("gameplay-routes-1920.png");
                    size(1280,720);return false;
                case 2:
                    RefreshNotes();ValidateRoutes(check);CheckHeads(check);capture("gameplay-routes-1280.png");
                    Clear();size(1920,1080);Spawn(2,.8f,spyware);Spawn(5,.8f,adware);Spawn(6,.8f,trojan,.45);
                    return false;
                case 3:
                    check(notes[0].SpriteRenderer.color.a<.4f,"Spyware still fades instead of losing its stealth mechanic");
                    check(notes[0].transform.Find("SignatureOutline").GetComponent<SpriteRenderer>().color.a<.4f,"Stealth also affects directional halo");
                    check(notes.All(n=>n.SpriteRenderer.color.r==1&&n.SpriteRenderer.color.g==1&&n.SpriteRenderer.color.b==1),"Original artist RGB preserved for all animated enemies");
                    check(notes.All(n=>!n.SpriteRenderer.flipX&&!n.SpriteRenderer.flipY),"All animated enemies retain their screen-bottom orientation");
                    VisualPreferences.ReducedEffects=true;return false;
                case 4:
                    check(notes.All(n=>n.transform.Find("SignatureOutline").GetComponent<SpriteRenderer>().color.a>=.35f),"Reduced effects keeps the existing minimum directional contrast");
                    var clip=NoteCharacterAnimations.For(trojan);
                    check(notes[2].SpriteRenderer.sprite==clip.Frames[clip.Definition.revealedStillFrame],"Trojan uses the revealed still frame in reduced effects");
                    capture("gameplay-reduced-effects-1920.png");VisualPreferences.ReducedEffects=false;
                    GameManager.Instance.SetState(GameState.Paused);frozenClock=AudioManager.Instance.SongTime;frozenElapsed=notes[1].AnimationElapsed;
                    return false;
                case 5:
                    // Allow the explicit reduced-effects toggle to select its
                    // normal frame before comparing paused frames across time.
                    frozenFrame=notes[1].SpriteRenderer.sprite;
                    check(frozenFrame==NoteCharacterAnimations.For(adware).Frames[NoteCharacterAnimations.For(adware).FrameIndex(frozenElapsed)],"Restoring full effects selects the frame at the frozen song time");
                    return false;
                case 6:
                    check(AudioManager.Instance.SongTime==frozenClock&&notes[1].AnimationElapsed==frozenElapsed&&notes[1].SpriteRenderer.sprite==frozenFrame,"Pause freezes the gameplay clock and sprite animation");
                    GameManager.Instance.SetState(GameState.Playing);Clear();
                    for(int lane=0;lane<7;lane++)Spawn(lane,.99f,null);
                    return false;
                case 7:
                    notes[0].OnHit(HitRating.Perfect);notes[3].OnHit(HitRating.Good);notes[6].OnHit(HitRating.Perfect);
                    var feedback=UnityEngine.Object.FindObjectsByType<TerminalFeedback>();
                    check(feedback.Count(f=>f.transform.Find("ImpactGlow").GetComponent<SpriteRenderer>().color.a>.5f)==3,"Successful hits still light up their three receptors immediately");
                    check(GameManager.Instance.PerfectHits==2&&GameManager.Instance.GoodHits==1&&GameManager.Instance.MissHits==0,"Visual changes preserve real hit scoring");
                    capture("gameplay-hit-glow-1920.png");Clear();VisualPreferences.ReducedEffects=reduced;
                    GameplayUI.IsTestPlayFromEditor=false;GameManager.Instance.SetState(GameState.MainMenu);return false;
                case 8:
                    check(!lanes.gameObject.activeInHierarchy,"Directional guides leave the screen together with the gameplay board");
                    return true;
            }
            return false;
        }
        static void Spawn(int lane,float p,MalwareData malware,double duration=0)
        {
            var go=new GameObject("ReadabilityFixture_"+CyberUI.LaneNames[lane]);go.AddComponent<SpriteRenderer>();
            var note=go.AddComponent<NoteController>();note.Initialize(new NoteData{laneIndex=lane,timestamp=AudioManager.Instance.SongTime+1,malwareData=malware,duration=duration},1.5);
            note.UpdatePosition(note.TargetTimestamp-note.TravelDuration*(1-p));notes.Add(note);progress.Add(p);
        }
        static void RefreshNotes()
        { for(int i=0;i<notes.Count;i++)notes[i].UpdatePosition(notes[i].TargetTimestamp-notes[i].TravelDuration*(1-progress[i])); }
        static void Clear()
        { foreach(var note in notes)if(note!=null)UnityEngine.Object.Destroy(note.gameObject);notes.Clear();progress.Clear(); }
        static void ValidateRoutes(Action<bool,string> check)
        {
            var lanes=LaneController.Instance;check(lanes.UsesEpicStage,"Calibrated PNG stage is active");float centre=lanes.GetSpawnPosition().x;
            for(int lane=0;lane<7;lane++)
            {
                float lastScale=0;bool side=true,monotonic=true;float maxStep=0;
                var previous=lanes.EvaluateLanePosition(lane,0);
                for(int sample=0;sample<=100;sample++)
                {
                    float p=sample/100f;var position=lanes.EvaluateLanePosition(lane,p);float scale=lanes.EvaluateLaneScale(lane,p).x;
                    side&=lane==6?Mathf.Abs(position.x-centre)<.0001f:lane<3?position.x<centre:position.x>centre;
                    monotonic&=scale>=lastScale-.0001f;lastScale=scale;
                    maxStep=Mathf.Max(maxStep,Vector3.Distance(previous,position));previous=position;
                    var displaced=lanes.ConstrainVisualOffset(lane,position,position+Vector3.right*100);
                    side&=lane==6?Mathf.Abs(displaced.x-centre)<.0001f:lane<3?displaced.x<centre:displaced.x>centre;
                }
                check(side,CyberUI.LaneNames[lane]+" stays in its correct road half, including extreme malware wobble");
                check(monotonic&&maxStep<.65f,CyberUI.LaneNames[lane]+" grows monotonically and moves continuously");
                check(Vector3.Distance(lanes.EvaluateLanePosition(lane,1),lanes.GetHitboxPosition(lane))<.0001f,CyberUI.LaneNames[lane]+" arrives exactly at its existing target");
                check(Mathf.Abs(lanes.EvaluateLaneScale(lane,1).x-lanes.EvaluateLaneScale(6,1).x)<.0001f,CyberUI.LaneNames[lane]+" has the same readable arrival size as SPACE");
                if(lane!=6)check(Vector3.Distance(lanes.EvaluateLanePosition(lane,lanes.GetLaneCornerProgress(lane)),lanes.GetLaneCornerPosition(lane))<.0001f,CyberUI.LaneNames[lane]+" shares its exact branch corner with holds and guides");
            }
            var spawns=Enumerable.Range(0,7).Select(lanes.GetLaneSpawnPosition).ToArray();
            check(spawns.Distinct().Count()==7,"Seven simultaneous notes have seven distinct approach positions");
            check(EpicStageLayout.LaneColor(0)==EpicStageLayout.LaneColor(2)&&EpicStageLayout.LaneColor(3)==EpicStageLayout.LaneColor(5)&&EpicStageLayout.LaneColor(0)!=EpicStageLayout.LaneColor(3)&&EpicStageLayout.LaneColor(6)!=EpicStageLayout.LaneColor(0),"Mint / pink / gold encode left / right / centre consistently");
        }
        static void ValidateArt(Action<bool,string> check)
        {
            MalwareData[] families={null,spyware,adware,trojan};int[] counts={9,10,9,14};double[] durations={16/30d,17/30d,16/30d,20/30d};
            for(int i=0;i<families.Length;i++)
            {
                var clip=NoteCharacterAnimations.For(families[i]);
                check(clip!=null&&clip.Frames.Length==counts[i]&&Math.Abs(clip.Duration-durations[i])<.00001,"Artist sequence "+i+" keeps every PNG and its timeline duration");
                check(clip.Frames.All(s=>s.rect.width==256&&s.rect.height==256&&s.pivot==new Vector2(128,128)&&s.pixelsPerUnit==256),"Artist sequence "+i+" preserves the complete canvas, pivot and scale");
            }
        }
        static void CheckHeads(Action<bool,string> check)
        {
            check(notes.All(n=>n!=null&&n.UsesCharacterAnimation),"Every fixture uses the user's animated enemy art");
            check(notes.Where(n=>n.LaneIndex==6).All(n=>Mathf.Abs(n.transform.position.x-LaneController.Instance.GetSpawnPosition().x)<.0001f),"SPACE enemies remain fully centred");
            check(notes.All(n=>n.transform.Find("LaneSignature").GetComponent<MeshRenderer>().bounds.size.y>n.transform.localScale.y*.19f),"Destination labels have a readable, consistent size relative to their enemy");
            foreach(var note in notes.Where(n=>n.Data.isHoldNote))
            {
                var line=note.GetComponent<LineRenderer>();var corner=LaneController.Instance.GetLaneCornerPosition(note.LaneIndex);
                float head=progress[notes.IndexOf(note)],tail=Mathf.Clamp01(head-(float)(note.Data.duration/note.TravelDuration));
                float turn=LaneController.Instance.GetLaneCornerProgress(note.LaneIndex);
                if(note.LaneIndex!=6&&tail<turn&&head>turn)
                    check(line.positionCount==3&&Vector3.Distance(line.GetPosition(1),corner)<.0001f,"Hold ribbon follows the new offset approach corner without cutting across the road");
                check(line.sortingOrder<note.SpriteRenderer.sortingOrder,"Hold ribbons remain behind enemy silhouettes");
            }
        }
    }
}
#endif
