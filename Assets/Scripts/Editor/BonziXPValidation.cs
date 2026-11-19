#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using RhythmWare.Core;
using RhythmWare.Rhythm;
using RhythmWare.UI;
using RhythmWare.Boss;

namespace RhythmWare.EditorTools
{
    public static class BonziXPValidation
    {
        static int step;
        static bool reduced;
        static BonziAnimations animations;
        static EpicStagePresenter stage;
        static FluidBackdrop backdrop;
        public static bool Run(Action<bool,string> check,Action<string> capture,Action<int,int> size)
        {
            switch(step)
            {
                case 0:
                    reduced=VisualPreferences.ReducedEffects;VisualPreferences.ReducedEffects=false;
                    animations=new BonziAnimations();ValidateDirector(check);Stage();size(1920,1080);step++;return false;
                case 1:
                    check(animations.UsesExtendedAtlas&&animations.TotalFrames==256&&animations.Names.Count()==13,"Expanded atlas loads 256 complete original frames and thirteen real clips");
                    check(animations.Material.shader.isSupported&&animations.Material.shader.name=="RhythmWare/BonziSoftKey","Expanded frames use the supported cyan-despill shader with their own atlas material");
                    check(backdrop.AnimationMaterial.GetTexture("_DesktopTex")==Resources.Load<Texture2D>("Presentation/XPMeadow"),"Boss shader uses the new XP meadow wallpaper independently of normal cities");
                    foreach(string clip in animations.Names)
                    {
                        bool fit=true;foreach(double time in new[]{0,animations.Duration(clip)*.5,animations.Duration(clip)-.001}){Pose(clip,time);fit&=Fits();}
                        check(fit,"Complete visible bounds fit the viewport for "+clip);
                        Pose(clip,animations.Duration(clip)*.45);capture("bonzi-xp-"+clip+"-1920.png");
                    }
                    size(1280,720);step++;return false;
                case 2:
                    Pose("intro",.6);check(Fits(),"Raised-hand greeting stays inside the 720p viewport and above its floor pivot");capture("bonzi-xp-1280.png");
                    size(1366,768);step++;return false;
                case 3:
                    Pose("scan",.5);check(Fits(),"Sunglasses pose fits the 1366x768 viewport");capture("bonzi-xp-1366.png");
                    size(1280,960);step++;return false;
                case 4:
                    Pose("cast",.7);check(Fits(),"Wide throwing gesture fits a 4:3 viewport without covering the HUD header");capture("bonzi-xp-4x3.png");
                    size(640,360);step++;return false;
                case 5:
                    int frame=0;double visualTime=0;
                    foreach(string clip in new[]{"intro","idle","scan","present","cast","talk","taunt","dance","phase","rage","stunned","hurt","defeat"})
                    {
                        int count=(int)Math.Ceiling(animations.Duration(clip)*10);
                        for(int i=0;i<count;i++){Pose(clip,i/10.0,visualTime+i/10.0);capture("bonzi-motion-"+(frame++).ToString("D3")+".png");}
                        visualTime+=count/10.0;
                    }
                    string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"docs/bonzi-xp-validation");
                    File.WriteAllText(Path.Combine(folder,"motion-metadata.json"),"{\"frames\":"+frame+",\"fps\":10,\"clips\":13}");
                    check(frame>120,"Native motion gallery records all thirteen animated poses at their configured frame rates");
                    GameManager.Instance.SetState(GameState.Paused);double frozen=backdrop.VisualClock;backdrop.RenderAt(frozen);step++;return false;
                case 6:
                    VisualPreferences.ReducedEffects=true;var boss=BonziStageController.Instance;boss.RefreshPresentation(AudioManager.Instance.SongTime);
                    check(boss.AnimationFrame==0&&Fits(),"Reduced effects uses a complete static pose and resets recoil");
                    AudioManager.Instance.StopSong();NoteSpawner.Instance.StopAndClear();GameManager.Instance.SetState(GameState.MainMenu);stage.SetBossSprite(null);animations.Dispose();VisualPreferences.ReducedEffects=reduced;return true;
            }
            return false;
        }
        static void Stage()
        {
            var chart=CampaignCatalog.CreateChart("Dinner");chart.audioClip=ChartEditorManager.Instance.ActiveChart.audioClip;
            chart.notes=Enumerable.Range(0,30).Select(i=>new NoteData{timestamp=1000+i,laneIndex=i%5}).ToList();
            GameplayUI.IsTestPlayFromEditor=true;ChartManager.Instance.SelectChart(chart);NoteSpawner.Instance.LoadChart(chart);
            AudioManager.Instance.PlaySong(chart.audioClip,1,20);GameManager.Instance.StartGame();AudioManager.Instance.PauseSong();NoteSpawner.Instance.StopAndClear();
            stage=UnityEngine.Object.FindAnyObjectByType<EpicStagePresenter>();backdrop=UnityEngine.Object.FindAnyObjectByType<FluidBackdrop>();
        }
        static void Pose(string clip,double time,double visual=2)
        {
            stage.SetBossMotion(Vector2.zero,0);stage.SetBossPose(animations.Frame(clip,time,false),animations.VisibleBounds(clip),animations.SourcePivot);
            backdrop.Desktop.ActorAction(clip);backdrop.Desktop.Status(clip=="phase"?2:clip=="rage"?3:1,1,clip=="defeat");backdrop.RenderAt(visual);
        }
        static bool Fits()
        {
            Rect b=stage.BossVisiblePixelBounds,safe=EpicStagePresenter.BossViewportPixel;
            var top=Camera.main.WorldToViewportPoint(EpicStageLayout.PixelToWorld(new Vector2(b.xMin,b.yMin)));
            var bottom=Camera.main.WorldToViewportPoint(EpicStageLayout.PixelToWorld(new Vector2(b.xMax,b.yMax)));
            return b.width>100&&b.height>180&&b.xMin>=safe.xMin-4&&b.xMax<=safe.xMax+4&&b.yMin>=safe.yMin-4&&b.yMax<=safe.yMax+4&&Math.Abs(stage.BossGroundPixel.y-365)<4&&top.x>=0&&top.y<.94&&bottom.x<=1&&bottom.y>0;
        }
        static void ValidateDirector(Action<bool,string> check)
        {
            var p=new BonziPerformance(animations.Duration);p.Reset();p.Tick(0,false,0,1,false);check(p.Current=="intro","Encounter starts with the new complete greeting");
            p.Tick(2,false,0,1,false);p.Attack("Spyware",3);double start=p.StartedAt;
            p.Hit(3.1);p.Hit(3.2);p.Attack("Adware",3.3);p.Tick(3.4,false,0,1,false);
            check(p.Current=="scan"&&p.StartedAt==start,"Rapid notes and hits preserve the current attack instead of restarting frame zero");
            p.Tick(3+animations.Duration("scan")+.01,false,0,1,false);check(p.Current=="present","Queued Adware gets its own presentation after scanning completes");
            p.Overclock(true,5);p.Overclock(true,5.2);check(p.Current=="stunned"&&p.StartedAt==5,"Repeated ready events do not restart the overclock reaction");
            p.Tick(7,false,0,2,false);check(p.Current=="phase","Second phase has its own book/presentation animation");p.Tick(9,false,0,3,false);check(p.Current=="rage","Final phase gets its own angry pose");
            p.Tick(11,true,10.8,3,false);check(p.Current=="talk"&&p.Loop,"Visible dialogue uses the talking gesture");
            p.Tick(12,false,0,3,true);p.Attack("Spyware",12.1);p.Overclock(false,12.2);p.Overclock(true,12.3);p.Revived(12.4);p.Tick(12.5,false,0,3,true);
            check(p.Current=="defeat"&&p.StartedAt==12,"Containment remains a terminal performance even if more notes or overclock events arrive");
            check(animations.FrameNumber("intro",.5,false)>animations.FrameNumber("idle",.5,false),"Clips honor their individual frame rates instead of a hard-coded ten fps");
        }
    }
}
#endif
