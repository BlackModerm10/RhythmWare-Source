#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RhythmWare.Core;
using RhythmWare.Rhythm;
using RhythmWare.UI;
using RhythmWare.Malware;
using RhythmWare.Boss;

namespace RhythmWare.EditorTools
{
    public static class EnvironmentPresentationValidation
    {
        static int step;
        static bool reduced;
        static double paused;
        static readonly List<NoteController> notes=new List<NoteController>();
        public static bool Run(Action<bool,string> check,Action<string> capture,Action<int,int> size)
        {
            var gm=GameManager.Instance;var backdrop=UnityEngine.Object.FindAnyObjectByType<FluidBackdrop>();
            var boss=BonziStageController.Instance;var desk=backdrop.Desktop;
            switch(step)
            {
                case 0:
                    reduced=VisualPreferences.ReducedEffects;VisualPreferences.ReducedEffects=false;size(1920,1080);Stage("Sara");step++;return false;
                case 1:
                    backdrop.RenderAt(0);check(backdrop.City.Visible&&!desk.Visible,"Sara uses the advancing comic street instead of a boss desktop");
                    check(backdrop.City.BuildingVertexCount==0,"Procedural rectangular buildings are removed from the illustrated city");
                    ValidateGPU(check,backdrop);backdrop.RenderAt(0);capture("sara-street-1920.png");size(640,360);step++;return false;
                case 2:
                    if(UnityEditor.SessionState.GetString("RWValidationFolder","")=="loop-desktop-validation")for(int i=0;i<240;i++){backdrop.RenderAt(i/10.0);capture("city-loop-"+i.ToString("D3")+".png");}
                    check(notes.All(n=>n.SpriteRenderer.color.r==1),"Moving environment does not tint the original character artwork");
                    Stage("Jack");size(1280,720);step++;return false;
                case 3:
                    backdrop.RenderAt(2);check(backdrop.Theme=="data-rush"&&backdrop.City.Visible&&!desk.Visible,"Jack retains the same comic city with its faster street loop");capture("jack-street-1280.png");
                    paused=backdrop.VisualClock;gm.SetState(GameState.Paused);step++;return false;
                case 4:
                    check(backdrop.VisualClock==paused,"Pause freezes the forward street clock");
                    VisualPreferences.ReducedEffects=true;gm.SetState(GameState.Playing);step++;return false;
                case 5:
                    check(backdrop.City.RenderedTime==0&&backdrop.VisualClock==paused,"Reduced effects freezes buildings and removes advancing road markers");
                    VisualPreferences.ReducedEffects=false;Stage("Dinner");size(1920,1080);step++;return false;
                case 6:
                    backdrop.RenderAt(1);check(desk.Visible&&!backdrop.City.Visible,"Bonzi replaces the normal city with its own Windows-era environment");
                    check(notes.All(n=>n.SpriteRenderer.sharedMaterial.mainTexture==n.SpriteRenderer.sprite.texture),"Each character outline keeps its own source frame texture instead of a neighbour's art");
                    check(desk.TaskbarVisible&&desk.transform.Find("BonziDesktopTaskbar").GetComponent<MeshRenderer>().sortingOrder>-78,"Windows taskbar is visible in front of the road but below the game HUD");
                    check(UnityEngine.Object.FindObjectsByType<Canvas>().Length==1,"Desktop scenery introduces no extra gameplay Canvas or UI input targets");
                    Launch(boss,ScriptableObject.CreateInstance<SpywareData>());boss.RefreshPresentation(20);backdrop.RenderAt(1);var before=desk.CursorPosition;backdrop.RenderAt(1.6);
                    check(desk.AttackSerial==1&&desk.LastThreat=="Spyware"&&Vector2.Distance(before,desk.CursorPosition)>100,"Actual Spyware launch moves Bonzi's system cursor toward documents");capture("bonzi-desktop-spyware-1920.png");step++;return false;
                case 7:
                    Launch(boss,ScriptableObject.CreateInstance<AdwareData>());boss.RefreshPresentation(AudioManager.Instance.Duration*.5);backdrop.RenderAt(3);
                    check(desk.Phase==2&&desk.LastThreat=="Adware"&&desk.AttackSerial==2,"Adware phase opens its desktop offer windows through boss events");capture("bonzi-desktop-adware-1920.png");
                    int counters=desk.CounterSerial;gm.RegisterHit(HitRating.Perfect,0);check(desk.CounterSerial==counters+1,"Successful interception counters Bonzi in the desktop environment");
                    size(1280,720);step++;return false;
                case 8:
                    Launch(boss,ScriptableObject.CreateInstance<TrojanData>());boss.RefreshPresentation(AudioManager.Instance.Duration*.8);backdrop.RenderAt(5);
                    check(desk.Phase==3&&desk.LastThreat=="Trojan"&&desk.AttackSerial==3,"Trojan phase switches to the untrusted-update security warning");capture("bonzi-desktop-trojan-1280.png");
                    typeof(BonziStageController).GetMethod("Overclock",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(boss,new object[]{20,true,false});
                    backdrop.RenderAt(5.1);capture("bonzi-desktop-overclock-1280.png");
                    for(int i=0;i<20;i++)gm.RegisterHit(HitRating.Perfect,i%5);boss.RefreshPresentation(20);backdrop.RenderAt(6);
                    check(desk.Contained&&boss.IsContained,"Containing the real boss changes desktop status and removes its attack windows");capture("bonzi-desktop-contained-1280.png");
                    Stage("Sara");step++;return false;
                case 9:
                    backdrop.RenderAt(0);check(backdrop.City.Visible&&!desk.Visible&&desk.AttackSerial==0&&!desk.Contained,"Returning to a normal song resets and hides the entire boss environment");
                    Clear();AudioManager.Instance.StopSong();NoteSpawner.Instance.StopAndClear();gm.SetState(GameState.MainMenu);backdrop.RenderAt(0);
                    check(!backdrop.City.Visible&&!desk.Visible&&!desk.TaskbarVisible,"Both environments disappear from menus and educational screens");
                    VisualPreferences.ReducedEffects=reduced;return true;
            }
            return false;
        }
        static void Stage(string key)
        {
            Clear();var chart=CampaignCatalog.CreateChart(key);chart.audioClip=ChartEditorManager.Instance.ActiveChart.audioClip;
            var spyware=chart.notes.Select(n=>n.malwareData).FirstOrDefault(m=>m is SpywareData);
            chart.notes=Enumerable.Range(0,20).Select(i=>new NoteData{timestamp=1000+i,laneIndex=i%5}).ToList();
            GameplayUI.IsTestPlayFromEditor=true;ChartManager.Instance.SelectChart(chart);NoteSpawner.Instance.LoadChart(chart);
            AudioManager.Instance.PlaySong(chart.audioClip,1,20);GameManager.Instance.StartGame();AudioManager.Instance.PauseSong();NoteSpawner.Instance.StopAndClear();
            int[] lanes=key=="Sara"?new[]{0,2}:new[]{0,1,2,3,4};
            foreach(int lane in lanes)
            {
                var go=new GameObject("EnvironmentFixtureNote");go.AddComponent<SpriteRenderer>();var note=go.AddComponent<NoteController>();
                MalwareData malware=key=="Sara"?null:lane==1?ScriptableObject.CreateInstance<AdwareData>():lane==3?ScriptableObject.CreateInstance<TrojanData>():lane==2&&key=="Dinner"?spyware:null;
                note.Initialize(new NoteData{laneIndex=lane,malwareData=malware,timestamp=AudioManager.Instance.SongTime+1},2);note.UpdatePosition(note.TargetTimestamp-note.TravelDuration*.2);notes.Add(note);
            }
        }
        static void Launch(BonziStageController boss,MalwareData malware)
        {typeof(BonziStageController).GetMethod("Launch",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(boss,new object[]{new NoteData{malwareData=malware,laneIndex=0}});}
        static void Clear(){foreach(var n in notes)if(n!=null)UnityEngine.Object.Destroy(n.gameObject);notes.Clear();}
        static void ValidateGPU(Action<bool,string> check,FluidBackdrop backdrop)
        {
            var camera=Camera.main;var target=new RenderTexture(320,180,24);target.Create();var old=camera.targetTexture;var active=RenderTexture.active;
            var texture=new Texture2D(320,180,TextureFormat.RGB24,false);
            Color[] Pixels(double t){backdrop.RenderAt(t);camera.targetTexture=target;RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,320,180),0,0);texture.Apply();return texture.GetPixels();}
            float Difference(Color[] a,Color[] b){float sum=0;for(int i=0;i<a.Length;i+=3)sum+=Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b);return sum/(a.Length/3);}
            try
            {
                var start=Pixels(0);var moving=Pixels(1);check(Difference(start,moving)>.0001f,"Illustrated parallax and street lights still animate after removing the box towers");
                check(Difference(start,Pixels(24))<.0001f,"Sara's entire illustrated street closes a seamless 24-second loop");
                var before=Pixels(23.999);var after=Pixels(24.001);check(Difference(before,after)<.005f,"Building recycling crosses its 24-second boundary without a visible pop");
                check(Difference(start,Pixels(480))<.0001f,"City geometry, road lights and wallpaper close the full 480-second loop");
            }
            finally{camera.targetTexture=old;RenderTexture.active=active;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(texture);}
        }
    }
}
#endif
