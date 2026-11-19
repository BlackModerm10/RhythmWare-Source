using RhythmWare.Core;
using System.Collections.Generic;
using UnityEngine;
using RhythmWare.Rhythm;
using RhythmWare.Malware;

namespace RhythmWare.UI
{
    public sealed class NotePresentation : MonoBehaviour
    {
        NoteController controller;
        SpriteRenderer outline, backing, keyFrame, keyFill;
        TextMesh laneLabel;
        MeshRenderer labelRenderer, oldBadge;
        NoteCharacterAnimations.Clip animation;
        Color routeColor;
        bool labelSized;
        static readonly Dictionary<(Texture,int),Material> inks=new Dictionary<(Texture,int),Material>();
        public bool UsesCharacterAnimation => animation != null;

        SpriteRenderer Layer(string name,int shape,Vector3 scale)
        {
            var child=new GameObject(name);child.transform.SetParent(transform,false);child.transform.localScale=scale;
            var renderer=child.AddComponent<SpriteRenderer>();renderer.sprite=NetworkVisualAssets.Shape(shape);return renderer;
        }
        public void Initialize(NoteController note)
        {
            controller=note;routeColor=EpicStageLayout.LaneColor(note.LaneIndex);
            animation=NoteCharacterAnimations.For(note.Data.malwareData);
            note.SpriteRenderer.sprite=animation!=null?animation.Frames[0]:NetworkVisualAssets.Shape(NetworkVisualAssets.Kind(note.Data.malwareData));
            if(animation!=null)
            {
                note.SpriteRenderer.color=Color.white;
                note.SpriteRenderer.flipX=false;note.SpriteRenderer.flipY=false;
            }
            backing=Layer("GroundShadow",12,new Vector3(1.23f,.18f,1));backing.transform.localPosition=new Vector3(0,-.44f,0);
            outline=Layer("SignatureOutline",note.LaneIndex==LaneLayout.Center?11:14,
                note.LaneIndex==LaneLayout.Center?Vector3.one*1.16f:new Vector3(1.16f,.92f,1));
            // SPACE has a gold diamond silhouette as well as its explicit label.
            if(note.LaneIndex==LaneLayout.Center)outline.transform.localRotation=Quaternion.Euler(0,0,45);
            float width=note.LaneIndex==LaneLayout.Center?1.30f:note.Data.malwareData!=null||note.Data.trainingObserve?1.08f:.66f;
            keyFrame=Layer("KeycapFrame",13,new Vector3(width,.32f,1));
            keyFill=Layer("KeycapFill",13,new Vector3(width-.055f,.265f,1));
            keyFrame.transform.localPosition=keyFill.transform.localPosition=new Vector3(0,-.70f,0);
            var label=new GameObject("LaneSignature");label.transform.SetParent(transform,false);
            label.transform.localPosition=new Vector3(0,-.70f,-.1f);
            laneLabel=label.AddComponent<TextMesh>();laneLabel.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            laneLabel.fontSize=48;laneLabel.characterSize=.3f;laneLabel.fontStyle=FontStyle.Bold;
            laneLabel.text=CyberUI.LaneNames[note.LaneIndex];laneLabel.anchor=TextAnchor.MiddleCenter;laneLabel.alignment=TextAlignment.Center;
            labelRenderer=label.GetComponent<MeshRenderer>();labelRenderer.sharedMaterial=laneLabel.font.material;
            oldBadge=transform.Find("BadgeText")?.GetComponent<MeshRenderer>();
            if(animation==null&&note.Data.malwareData==null)note.SpriteRenderer.color=routeColor;
            RefreshVisuals();
        }
        void LateUpdate() => RefreshVisuals();
        void RefreshVisuals()
        {
            if(controller==null||outline==null)return;
            float alpha=controller.SpriteRenderer.color.a;
            if(VisualPreferences.ReducedEffects)
            {
                alpha=Mathf.Max(.4f,alpha);
                Color visible=controller.SpriteRenderer.color;visible.a=alpha;controller.SpriteRenderer.color=visible;
            }
            if(animation!=null)
            {
                double revealAt=controller.Data.malwareData is TrojanData trojan?controller.TravelDuration*trojan.revealProgress:-1;
                controller.SpriteRenderer.sprite=animation.Frames[animation.FrameIndex(controller.AnimationElapsed,revealAt,VisualPreferences.ReducedEffects)];
                // Keep the artist's RGB colors and orientation. Spyware owns alpha.
                controller.SpriteRenderer.color=new Color(1,1,1,alpha);
                ApplyInk();
            }
            // A stable diamond shares the receptor's timing centre. The character
            // touching its outer edge is not the musical arrival cue.
            bool arriving=Mathf.Abs((float)(controller.TravelDuration-controller.AnimationElapsed))<=.06f;
            Color cue=arriving?Color.white:routeColor;
            outline.color=new Color(cue.r,cue.g,cue.b,Mathf.Max(.45f,alpha)*(arriving?.98f:.70f));
            backing.color=new Color(.38f,.12f,.70f,alpha*.9f);
            keyFrame.color=new Color(routeColor.r,routeColor.g,routeColor.b,alpha);
            keyFill.color=new Color(.025f,.035f,.075f,alpha);
            laneLabel.color=new Color(1,1,1,alpha);
            bool teaching=ChartManager.Instance?.SelectedChart?.tutorialId?.StartsWith("sara-basics")==true||TutorialGuide.Instance?.IsTraining==true;
            float travelProgress=(float)(controller.AnimationElapsed/controller.TravelDuration);
            bool identify=!teaching&&controller.Data.malwareData!=null&&travelProgress>.40f&&travelProgress<.88f;
            string tag=controller.Data.malwareData is SpywareData?"SPYWARE":controller.Data.malwareData is TrojanData?travelProgress<.5f?"REGALO":"TROYANO":"ADWARE";
            string caption=controller.Data.trainingObserve?"EJEMPLO":teaching?InputManager.Instance.LabelForLane(controller.LaneIndex):tag;
            if(laneLabel.text!=caption){laneLabel.text=caption;labelSized=false;}
            keyFrame.enabled=keyFill.enabled=labelRenderer.enabled=(teaching||identify||controller.Data.trainingObserve)&&!controller.IsHolding&&travelProgress<.90f;
            if(!labelSized&&labelRenderer.localBounds.size.y>.001f)
            {
                laneLabel.transform.localScale=Vector3.one*((teaching?.21f:.16f)/labelRenderer.localBounds.size.y);labelSized=true;
            }
            // Sort complete characters by depth, keeping distant silhouettes
            // behind incoming ones. Hold ribbons deliberately stay behind all heads.
            float progress=controller.IsHolding?1:Mathf.Clamp01((float)(controller.AnimationElapsed/controller.TravelDuration));
            int order=20+Mathf.RoundToInt(progress*600);
            backing.sortingOrder=order;outline.sortingOrder=order+1;controller.SpriteRenderer.sortingOrder=order+2;
            keyFrame.sortingOrder=order+3;keyFill.sortingOrder=order+4;labelRenderer.sortingOrder=order+5;
            if(oldBadge!=null)oldBadge.sortingOrder=order+6;
        }
        void ApplyInk()
        {
            // Keep each source frame bound explicitly. Sharing one mutable
            // texture material per hand allowed the last character to replace
            // the art of its neighbour in a sprite rendering batch.
            var sprite=controller.SpriteRenderer.sprite;if(sprite==null)return;
            int group=controller.LaneIndex==LaneLayout.Center?2:LaneLayout.IsLeft(controller.LaneIndex)?0:1;
            var key=((Texture)sprite.texture,group);
            if(!inks.TryGetValue(key,out var ink)||ink==null)
            {
                var shader=Resources.Load<Shader>("Presentation/CharacterInk");if(shader==null||!shader.isSupported)return;
                ink=new Material(shader);ink.mainTexture=sprite.texture;ink.SetColor("_OutlineColor",routeColor);inks[key]=ink;
            }
            if(controller.SpriteRenderer.sharedMaterial!=ink)controller.SpriteRenderer.sharedMaterial=ink;
        }
    }
}
