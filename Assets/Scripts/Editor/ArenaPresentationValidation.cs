#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using RhythmWare.Core;
using RhythmWare.Rhythm;
using RhythmWare.UI;

namespace RhythmWare.EditorTools
{
    public static class ArenaPresentationValidation
    {
        public static void Check(Action<bool,string> check,List<NoteController> notes)
        {
            check(Resources.Load<Texture2D>("Presentation/CandyCityArena")!=null,"Approved-reference city is imported as a separate production texture");
            check(notes.All(n=>n.SpriteRenderer.sharedMaterial.shader.name=="RhythmWare/CharacterInk"),"All four original animated characters receive a silhouette outline");
            var stage=UnityEngine.Object.FindAnyObjectByType<EpicStagePresenter>();
            Rect visible=stage.BossVisiblePixelBounds;
            check(visible.height>190&&visible.yMin>=EpicStagePresenter.BossViewportPixel.yMin-4&&visible.yMax<=EpicStagePresenter.BossViewportPixel.yMax+4,"Boss visible pixels fit the dedicated portrait area without header clipping");
            check(Mathf.Abs(stage.BossGroundPixel.y-EpicStageLayout.OriginPixel.y)<4,"Bonzi's foot pivot stays grounded at the highway horizon");
            for(int lane=0;lane<5;lane++)
            {
                bool smooth=true,guide=true;var line=stage.GetComponentsInChildren<LineRenderer>().First(l=>l.name=="Approach_"+LaneLayout.Names[lane]);
                for(int i=1;i<100;i++)
                {
                    float p=i/100f;var a=EpicStageLayout.RoutePixel(lane,p)-EpicStageLayout.RoutePixel(lane,p-.001f);var b=EpicStageLayout.RoutePixel(lane,p+.001f)-EpicStageLayout.RoutePixel(lane,p);
                    if(a.sqrMagnitude>1e-7f&&b.sqrMagnitude>1e-7f)smooth&=Vector2.Dot(a.normalized,b.normalized)>.99f;
                }
                for(int i=0;i<65;i++){float t=i/64f;guide&=Vector3.Distance(line.GetPosition(i),LaneController.Instance.EvaluateLanePosition(lane,EpicStageLayout.UnprojectProgress(t)))<.0001f;}
                check(smooth&&guide,"Continuous guide and note curve coincide for "+LaneLayout.Names[lane]);
            }
            var hold=notes.First(n=>n.Data.isHoldNote);var ribbon=hold.GetComponent<LineRenderer>();
            check(ribbon.positionCount==49,"Hold ribbon samples the curved route without a corner shortcut");
            CheckHUD(check);
        }
        public static void CheckHUD(Action<bool,string> check)
        {
            Canvas.ForceUpdateCanvases();var hud=UnityEngine.Object.FindAnyObjectByType<GameplayHUD>();
            var panels=hud.GetComponentsInChildren<CombatPanelGraphic>();
            check(panels.Count(p=>p.Meter)==2,"Firewall and overclock use independent continuous comic meters");
            var meter=panels.First(p=>p.name=="FirewallFill");hud.Health(20,100);check(Mathf.Abs(meter.Fill-.2f)<.001f,"Continuous firewall fill represents exactly twenty percent");hud.Health(GameManager.Instance.CurrentHealth,100);
            var bad=new List<string>();foreach(var label in hud.GetComponentsInChildren<TextMeshProUGUI>()){label.ForceMeshUpdate();if(label.preferredHeight>label.rectTransform.rect.height+3||label.preferredWidth>label.rectTransform.rect.width+3)bad.Add(label.text);}
            check(bad.Count==0,"Comic HUD labels fit their allocated rectangles "+string.Join(" | ",bad));
            var bg=BackgroundManager.Instance;float saved=GameSettings.BackgroundDim;
            bg.UpdateDimOverlay(.35f);check(Mathf.Abs(bg.EffectiveBackgroundDim-.1225f)<.0001f,"Default slider strength keeps the illustrated city luminous");
            bg.UpdateDimOverlay(1);check(bg.EffectiveBackgroundDim==1,"Maximum background attenuation remains fully dark");bg.UpdateDimOverlay(saved);
        }
    }
}
#endif
