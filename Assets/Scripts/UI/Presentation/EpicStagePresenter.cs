using UnityEngine;
using RhythmWare.Rhythm;

namespace RhythmWare.UI
{
    // Shared calibrated actor anchor; chart-specific controllers own animation.
    public sealed class EpicStagePresenter : MonoBehaviour
    {
        public Transform BossAnchor { get; private set; }
        public Transform EmissionOrigin { get; private set; }
        Transform[] receptors;
        SpriteRenderer boss;
        SpriteRenderer aura,platform;
        Rect previousRect;
        float actorHeight=200;
        Rect? poseBounds;
        Vector2 posePivot,motion;
        float squash;
        public static readonly Rect BossViewportPixel=new Rect(670,92,580,301);
        public Rect BossVisiblePixelBounds { get; private set; }
        public Vector2 BossGroundPixel { get; private set; }
        LaneReadabilityGuide guides;
        FiveLaneStageVisual road;
        public void Initialize(Transform origin,Transform[] targets)
        {
            EmissionOrigin=origin;receptors=targets;
            BossAnchor=new GameObject("BossStageAnchor").transform;BossAnchor.SetParent(transform,false);
            boss=BossAnchor.gameObject.AddComponent<SpriteRenderer>();boss.sortingOrder=2;boss.enabled=false;
            aura=Decoration("BossRadiance",NetworkVisualAssets.Glow,new Color(.75f,.35f,1,.65f),-68);
            platform=Decoration("BossLaunchPlatform",NetworkVisualAssets.Shape(12),new Color(.65f,.27f,.95f,.7f),-67);
            guides=gameObject.AddComponent<LaneReadabilityGuide>();guides.Initialize(targets);
            road=gameObject.AddComponent<FiveLaneStageVisual>();
            RefreshLayout();
        }
        SpriteRenderer Decoration(string name,Sprite sprite,Color tint,int order)
        {
            var child=new GameObject(name);child.transform.SetParent(transform,false);var sr=child.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.color=tint;sr.sortingOrder=order;sr.enabled=false;return sr;
        }
        public void SetBossSprite(Sprite sprite,float height=200)
        {
            poseBounds=null;actorHeight=height;boss.sprite=sprite;boss.enabled=sprite!=null;if(aura!=null)aura.enabled=platform.enabled=boss.enabled;FitBoss();
        }
        public void SetBossPose(Sprite sprite,Rect bounds,Vector2 pivot)
        {
            poseBounds=bounds;posePivot=pivot;boss.sprite=sprite;boss.enabled=sprite!=null;
            if(aura!=null)aura.enabled=platform.enabled=boss.enabled;FitBoss();
        }
        public void SetBossMotion(Vector2 offset,float hitSquash)
        {motion=offset;squash=hitSquash;FitBoss();}
        void LateUpdate()
        {
            // Camera shake moves the artwork and notes together; do not chase
            // transient camera translation by moving only the receptors.
            if(Camera.main!=null&&EpicStageLayout.WorldRect(Camera.main).size!=previousRect.size)RefreshLayout();
        }
        void RefreshLayout()
        {
            if(Camera.main==null)return;
            previousRect=EpicStageLayout.WorldRect(Camera.main);
            EmissionOrigin.position=EpicStageLayout.PixelToWorld(EpicStageLayout.OriginPixel);
            BossAnchor.position=EpicStageLayout.PixelToWorld(EpicStageLayout.BossPixel);
            FitBoss();
            for(int i=0;i<receptors.Length;i++)
            {
                receptors[i].position=EpicStageLayout.ReceptorPosition(i);
                var scale=EpicStageLayout.ReceptorScale(i);
                LaneController.Instance.SetReceptorRestScale(i,scale);
                var label=receptors[i].GetComponentInChildren<TextMesh>();
                if(label!=null)LaneController.Instance.FitKeyLabel(i,label);
            }
            guides.RefreshLayout();road?.RefreshLayout();
        }
        void FitBoss()
        {
            if(boss==null||boss.sprite==null||Camera.main==null)return;
            float unit=EpicStageLayout.WorldRect(Camera.main).height/EpicStageLayout.ReferenceHeight;
            if(poseBounds.HasValue)
            {
                Rect b=poseBounds.Value,viewport=BossViewportPixel;
                float above=Mathf.Max(1,posePivot.y-b.y),below=Mathf.Max(0,b.yMax-posePivot.y);
                float scale=Mathf.Min(2.35f,Mathf.Min(viewport.width/b.width,(365-viewport.y)/above));
                if(below>0)scale=Mathf.Min(scale,(viewport.yMax-365)/below);
                float sx=scale*(1+squash),sy=scale*(1-squash);
                Vector2 root=new Vector2(viewport.center.x-(b.center.x-posePivot.x)*sx,365)+motion;BossGroundPixel=root;
                BossAnchor.position=EpicStageLayout.PixelToWorld(root);
                BossAnchor.localScale=new Vector3(sx*unit*100,sy*unit*100,1);
                BossVisiblePixelBounds=new Rect(root.x+(b.x-posePivot.x)*sx,root.y+(b.y-posePivot.y)*sy,b.width*sx,b.height*sy);
            }
            else BossAnchor.localScale=Vector3.one*Mathf.Min(600*unit/boss.sprite.bounds.size.x,actorHeight*unit/boss.sprite.bounds.size.y);
            aura.transform.position=EpicStageLayout.PixelToWorld(new Vector2(960,235));aura.transform.localScale=new Vector3(470*unit,350*unit,1);
            platform.transform.position=EpicStageLayout.PixelToWorld(new Vector2(960,362));platform.transform.localScale=new Vector3(250*unit,22*unit,1);
        }
    }
}
