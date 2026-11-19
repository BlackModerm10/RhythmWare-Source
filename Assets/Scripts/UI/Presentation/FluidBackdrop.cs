using UnityEngine;
using RhythmWare.Core;

namespace RhythmWare.UI
{
    public sealed class FluidBackdrop : MonoBehaviour
    {
        public Material AnimationMaterial { get; private set; }
        public Material ArtworkMaterial => AnimationMaterial;
        public double VisualClock { get; private set; }
        public string Theme { get; private set; }="candy-city";
        public bool IsBossDesktop=>Theme=="bonzi-core"||Theme=="bonzi-desktop";
        public InfiniteCityEnvironment City { get; private set; }
        public BonziDesktopEnvironment Desktop { get; private set; }
        SpriteRenderer surface;
        Texture2D white;
        Sprite sprite;
        Texture original;
        public bool Initialize(SpriteRenderer unused)
        {
            var shader=Resources.Load<Shader>("Presentation/CandyCity");if(shader==null||!shader.isSupported)return false;
            AnimationMaterial=new Material(shader);original=Resources.Load<Texture2D>("Presentation/CandyCityArena");
            if(original==null)original=Resources.Load<Texture2D>("Presentation/CandyCityBackdrop");
            AnimationMaterial.SetTexture("_CityTex",original!=null?original:Texture2D.blackTexture);
            AnimationMaterial.SetTexture("_DesktopTex",Resources.Load<Texture2D>("Presentation/XPMeadow"));
            var go=new GameObject("IndependentCityBackdrop");go.transform.SetParent(transform,false);
            surface=go.AddComponent<SpriteRenderer>();surface.sortingOrder=-99;
            white=new Texture2D(1,1,TextureFormat.RGBA32,false);white.SetPixel(0,0,Color.white);white.Apply();
            sprite=Sprite.Create(white,new Rect(0,0,1,1),new Vector2(.5f,.5f),1);surface.sprite=sprite;surface.sharedMaterial=AnimationMaterial;
            City=gameObject.AddComponent<InfiniteCityEnvironment>();City.Initialize();City.SetVisible(false);
            Desktop=gameObject.AddComponent<BonziDesktopEnvironment>();Desktop.Initialize();Desktop.SetVisible(false);
            FitToCamera();return true;
        }
        public void SetTheme(string theme,Sprite custom=null)
        {
            Theme=string.IsNullOrEmpty(theme)?"candy-city":theme;
            if(AnimationMaterial==null)return;
            AnimationMaterial.SetTexture("_CityTex",custom!=null?custom.texture:original!=null?original:Texture2D.blackTexture);
            AnimationMaterial.SetFloat("_Theme",IsBossDesktop?2:Theme=="data-rush"?1:0);
            AnimationMaterial.SetFloat("_Custom",custom!=null?1:0);
            VisualClock=0;Desktop.ResetEncounter();RenderAt(0);
        }
        public void FitToCamera()
        {
            var cam=Camera.main;if(cam==null||surface==null)return;float height=cam.orthographicSize*2;
            surface.transform.position=new Vector3(cam.transform.position.x,cam.transform.position.y,10);
            surface.transform.localScale=new Vector3(height*cam.aspect*1.08f,height*1.08f,1);
        }
        void Update()
        {
            if(AnimationMaterial==null)return;
            bool reduced=VisualPreferences.ReducedEffects;
            if(!reduced&&GameManager.Instance!=null&&GameManager.Instance.CurrentState==GameState.Playing)VisualClock+=Time.deltaTime;
            RenderAt(reduced?0:VisualClock%480);
        }
        public void RenderAt(double time)
        {
            if(AnimationMaterial==null)return;bool reduced=VisualPreferences.ReducedEffects;
            double actual=reduced?0:time;
            AnimationMaterial.SetFloat("_VisualTime",(float)actual);
            AnimationMaterial.SetFloat("_Reduced",reduced?1:0);
            AnimationMaterial.SetFloat("_DesktopPhase",Desktop.Phase);
            var state=GameManager.Instance!=null?GameManager.Instance.CurrentState:GameState.MainMenu;
            bool visible=state==GameState.Playing||state==GameState.TutorialInstruction||state==GameState.Paused||state==GameState.RescueQuestion||state==GameState.Reviving||state==GameState.GameOver;
            City.SetVisible(visible&&!IsBossDesktop);Desktop.SetVisible(visible&&IsBossDesktop);
            City.Render(actual,Theme=="data-rush",reduced);Desktop.Render(actual,reduced);
        }
        void OnDestroy()
        {
            if(AnimationMaterial!=null)Destroy(AnimationMaterial);if(sprite!=null)Destroy(sprite);if(white!=null)Destroy(white);
        }
    }
}
