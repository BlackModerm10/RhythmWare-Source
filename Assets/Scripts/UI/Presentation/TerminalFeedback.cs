using UnityEngine;
using RhythmWare.Core;
using RhythmWare.Rhythm;

namespace RhythmWare.UI
{
    public sealed class TerminalFeedback : MonoBehaviour
    {
        int lane;
        SpriteRenderer ring;
        SpriteRenderer glow;
        SpriteRenderer[] fragments;
        Color color;
        float pulse=1;
        public void Initialize(int laneIndex, Color tint,bool drawShell=true)
        {
            lane=laneIndex;color=tint;
            var frame=NetworkVisualAssets.Shape(lane==LaneLayout.Center?16:14);
            ring=Child("EnergyWave",frame,Color.clear);glow=Child("ImpactGlow",frame,Color.clear);
            if(drawShell)
            {
                var shell=Child("SecurityTerminal",NetworkVisualAssets.Shape(4),new Color(.04f,.09f,.15f,.95f));
                shell.sortingOrder=-5;shell.transform.localScale=new Vector3(1.28f,1.25f,1);
            }
            fragments=new SpriteRenderer[6];for(int i=0;i<6;i++)
            {
                fragments[i]=Child("PacketFragment",NetworkVisualAssets.Shape(0),Color.clear);
                fragments[i].transform.localScale=new Vector3(.09f/transform.localScale.x,.09f/transform.localScale.y,1);
            }
        }
        SpriteRenderer Child(string name,Sprite sprite,Color tint)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);var sr=go.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.color=tint;sr.sortingOrder=8;return sr;
        }
        void OnEnable()
        {
            if(GameManager.Instance!=null)GameManager.Instance.OnNoteHit+=Hit;
            if(InputManager.Instance!=null)InputManager.Instance.OnLanePressed+=Press;
        }
        void OnDisable()
        {
            if(GameManager.Instance!=null)GameManager.Instance.OnNoteHit-=Hit;
            if(InputManager.Instance!=null)InputManager.Instance.OnLanePressed-=Press;
        }
        void Flash(Color tint) { pulse=0;color=tint;RenderPulse(); }
        void Press(int index) { if(index==lane)Flash(EpicStageLayout.LaneColor(lane)); }
        void Hit(HitRating rating,int index) { if(index==lane)Flash(rating==HitRating.Perfect?CyberUI.Cyan:rating==HitRating.Good?Color.yellow:CyberUI.Red); }
        void Update()
        {
            pulse=Mathf.Min(1,pulse+Time.deltaTime*4);RenderPulse();
        }
        void RenderPulse()
        {
            if(ring==null)return;
            float alpha=1-pulse;ring.transform.localScale=Vector3.one*(1+pulse*(lane==LaneLayout.Center?.06f:.18f));ring.color=new Color(color.r,color.g,color.b,alpha*.6f);
            glow.color=Color.Lerp(color,Color.white,alpha*.8f);glow.color=new Color(glow.color.r,glow.color.g,glow.color.b,alpha*.9f);
            for(int i=0;i<fragments.Length;i++)
            {
                float angle=i*Mathf.PI/3;fragments[i].transform.localPosition=new Vector3(Mathf.Cos(angle)/transform.localScale.x,Mathf.Sin(angle)/transform.localScale.y,-.1f)*pulse*.9f;
                fragments[i].color=new Color(color.r,color.g,color.b,VisualPreferences.ReducedEffects?0:alpha);
            }
        }
    }
}
