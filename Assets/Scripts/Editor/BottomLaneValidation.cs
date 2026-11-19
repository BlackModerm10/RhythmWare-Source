#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RhythmWare.Core;
using RhythmWare.Rhythm;
using RhythmWare.Malware;
using RhythmWare.UI;

namespace RhythmWare.EditorTools
{
    public static class BottomLaneValidation
    {
        static int step;static readonly List<NoteController> fixtures=new List<NoteController>();
        public static void Clear(){foreach(var n in fixtures)if(n!=null)UnityEngine.Object.Destroy(n.gameObject);fixtures.Clear();}
        public static bool Run(Action<bool,string> check,Action<string> capture,Action<int,int> size)
        {
            switch(step)
            {
                case 0:
                    Stage("Jack");Geometry(check);size(1920,1080);step++;return false;
                case 1:
                    Present();capture("bottom-lanes-normal-1920.png");size(1280,720);step++;return false;
                case 2:
                    Present();Geometry(check);capture("bottom-lanes-normal-1280.png");size(1920,1080);
                    GameManager.Instance.SetState(GameState.Playing);MalwareEffectManager.Instance.TriggerSpywareGlitch();GameManager.Instance.SetState(GameState.Paused);step++;return false;
                case 3:
                    Present();capture("bottom-lanes-spyware-1920.png");
                    check(EpicStageLayout.ReceptorPixel(0).y-80>596,"Infection status cards stay above the full receptor row");
                    GameManager.Instance.SetState(GameState.Playing);MalwareEffectManager.Instance.TriggerAdwarePopups();MalwareEffectManager.Instance.TriggerTrojanPayload();GameManager.Instance.SetState(GameState.Paused);step++;return false;
                case 4:
                    Present();capture("bottom-lanes-three-infections-1920.png");Stage("Dinner");step++;return false;
                case 5:
                    Present();var boss=UnityEngine.Object.FindAnyObjectByType<EpicStagePresenter>();
                    check(boss.BossVisiblePixelBounds.height>190&&Mathf.Abs(boss.BossGroundPixel.y-EpicStageLayout.OriginPixel.y)<4,"Bonzi remains framed and grounded above the new five-column track");
                    capture("bottom-lanes-bonzi-1920.png");size(1280,720);step++;return false;
                case 6:
                    Present();capture("bottom-lanes-bonzi-1280.png");size(1920,1080);
                    var feedback=LaneController.Instance.GetComponentsInChildren<TerminalFeedback>().First();typeof(TerminalFeedback).GetMethod("Press",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(feedback,new object[]{0});
                    capture("bottom-lanes-key-feedback-1920.png");Clear();MalwareEffectManager.Instance.ResetInfections();AudioManager.Instance.StopSong();NoteSpawner.Instance.StopAndClear();GameManager.Instance.SetState(GameState.MainMenu);return true;
            }
            return false;
        }
        static void Geometry(Action<bool,string> check)
        {
            var lanes=LaneController.Instance;float previous=float.NegativeInfinity;
            foreach(int lane in LaneLayout.VisualOrder)
            {
                Vector2 hit=EpicStageLayout.ReceptorPixel(lane);check(hit.x>previous&&hit.y==EpicStageLayout.HitLineY,"Receptor "+LaneLayout.Names[lane]+" is in keyboard order on the shared hit line");previous=hit.x;
                Vector2 start=EpicStageLayout.RoutePixel(lane,0),end=EpicStageLayout.RoutePixel(lane,1);bool straight=true,ordered=true,arrival=true;
                int row=Array.IndexOf(LaneLayout.VisualOrder,lane);
                for(int i=0;i<=100;i++)
                {
                    float p=i/100f,q=EpicStageLayout.ProjectProgress(p);Vector2 point=EpicStageLayout.RoutePixel(lane,q),delta=end-start;
                    straight&=Mathf.Abs((point.x-start.x)*delta.y-(point.y-start.y)*delta.x)<.2f;
                    float half=EpicStageLayout.RoadHalfWidth(point.y);ordered&=point.x>=960+half*EpicStageLayout.LaneEdges[row]&&point.x<=960+half*EpicStageLayout.LaneEdges[row+1];
                    arrival&=Mathf.Abs(EpicStageLayout.UnprojectProgress(q)-p)<.00001f;
                }
                check(straight&&ordered&&arrival,"Straight route, column bounds and perspective inverse agree for "+LaneLayout.Names[lane]);
                check(Vector3.Distance(lanes.EvaluateLanePosition(lane,1),lanes.GetHitboxPosition(lane))<.0001f,"Note and receptor centers meet exactly for "+LaneLayout.Names[lane]);
            }
            for(int i=0;i<4;i++){int a=LaneLayout.VisualOrder[i],b=LaneLayout.VisualOrder[i+1];check(EpicStageLayout.ReceptorPixel(b).x-EpicStageLayout.ReceptorPixel(a).x>(EpicStageLayout.ReceptorSize(a).x+EpicStageLayout.ReceptorSize(b).x)*.53f,"Adjacent receivers remain separate while pulsing: "+i);}
            check(lanes.EvaluateLaneScale(0,0)==lanes.EvaluateLaneScale(0,1),"Notes keep the same readable size from entry to receptor");
            check(UnityEngine.Object.FindAnyObjectByType<FiveLaneStageVisual>().GetComponentsInChildren<MeshFilter>().Count(m=>m.name.StartsWith("Column_"))==5,"Five independent column surfaces replace the side exits");
        }
        static void Stage(string key)
        {
            Clear();var chart=CampaignCatalog.CreateChart(key);chart.audioClip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/"+GameContentPaths.PortableAudio(chart.audioClipPath));
            GameplayUI.IsTestPlayFromEditor=true;ChartManager.Instance.SelectChart(chart);NoteSpawner.Instance.LoadChart(chart);AudioManager.Instance.PlaySong(chart.audioClip,1,20);AudioManager.Instance.PauseSong();NoteSpawner.Instance.StopAndClear();GameManager.Instance.StartGame();GameManager.Instance.SetState(GameState.Paused);
            var palette=ChartData.GetDefaultMalwareCatalog();float[] progress={.80f,.56f,.80f,.43f,.70f};
            for(int lane=0;lane<5;lane++)
            {
                var go=new GameObject("BottomLanePreview_"+lane);go.AddComponent<SpriteRenderer>();var n=go.AddComponent<NoteController>();
                n.Initialize(new NoteData{laneIndex=lane,timestamp=22,duration=lane==4?.8:0,malwareData=lane==1?palette["Spyware"]:lane==3?palette["Trojan"]:null},2.2);
                n.UpdatePosition(n.TargetTimestamp-n.TravelDuration*(1-progress[lane]));fixtures.Add(n);
            }
        }
        static void Present()
        {
            var late=typeof(NotePresentation).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);foreach(var n in fixtures)if(n!=null)late.Invoke(n.GetComponent<NotePresentation>(),null);
            UnityEngine.Object.FindAnyObjectByType<FluidBackdrop>().RenderAt(3);RhythmWare.Boss.BonziStageController.Instance.RefreshPresentation(20);Canvas.ForceUpdateCanvases();
        }
    }
}
#endif
