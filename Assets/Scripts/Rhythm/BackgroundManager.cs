using UnityEngine;
using RhythmWare.Core;

namespace RhythmWare.Rhythm
{
    public class BackgroundManager : MonoBehaviour
    {
        public static BackgroundManager Instance { get; private set; }

        [Header("Background Elements")]
        [SerializeField] private SpriteRenderer backgroundRenderer;
        [SerializeField] private SpriteRenderer dimOverlayRenderer;
        [SerializeField] private Sprite defaultBackgroundSprite;
        private SpriteRenderer stageRenderer;
        private RhythmWare.UI.FluidBackdrop fluidBackdrop;
        public float EffectiveBackgroundDim => dimOverlayRenderer!=null?dimOverlayRenderer.color.a:0;
        public RhythmWare.UI.BonziDesktopEnvironment BossDesktop => fluidBackdrop!=null?fluidBackdrop.Desktop:null;
        public Sprite StageArtwork => stageRenderer!=null?stageRenderer.sprite:null;

        [Header("Default Colors")]
        [SerializeField] private Color defaultBackgroundColor = new Color(0.06f, 0.07f, 0.12f, 1f);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureComponents();
            fluidBackdrop=gameObject.AddComponent<RhythmWare.UI.FluidBackdrop>();
            if(!fluidBackdrop.Initialize(null)){Destroy(fluidBackdrop);fluidBackdrop=null;}
        }

        private void Start()
        {
            GameSettings.OnBackgroundDimChanged += UpdateDimOverlay;
            UpdateDimOverlay(GameSettings.BackgroundDim);
            FitToCamera();
        }

        private float lastAspect;
        private void Update() { if(Camera.main!=null && Camera.main.aspect!=lastAspect) { lastAspect=Camera.main.aspect;FitToCamera(); } }

        private void OnDestroy()
        {
            GameSettings.OnBackgroundDimChanged -= UpdateDimOverlay;
        }

        private void EnsureComponents()
        {
            if (backgroundRenderer == null)
            {
                GameObject bgObj = new GameObject("BackgroundPlane");
                bgObj.transform.SetParent(transform);
                bgObj.transform.localPosition = new Vector3(0f, 0f, 10f); // Behind game elements
                backgroundRenderer = bgObj.AddComponent<SpriteRenderer>();
                backgroundRenderer.sortingOrder = -100;
                
                if (defaultBackgroundSprite != null)
                {
                    backgroundRenderer.sprite = defaultBackgroundSprite;
                }
                else
                {
                    backgroundRenderer.sprite = CreatePlaceholderBGSprite();
                    backgroundRenderer.color = defaultBackgroundColor;
                }
            }

            if (dimOverlayRenderer == null)
            {
                GameObject dimObj = new GameObject("BackgroundDimOverlay");
                dimObj.transform.SetParent(transform);
                dimObj.transform.localPosition = new Vector3(0f, 0f, 9.5f); // In front of BG, behind board
                dimOverlayRenderer = dimObj.AddComponent<SpriteRenderer>();
                dimOverlayRenderer.sprite = CreateSolidWhiteSprite();
                dimOverlayRenderer.color = new Color(0f, 0f, 0f, GameSettings.BackgroundDim);
                dimOverlayRenderer.sortingOrder = -90;
            }
        }

        public void ApplyChart(ChartData chart)
        {
            if(chart==null)return;
            // Boss identity takes precedence over a generic/custom wallpaper.
            // Future bosses can select their own environment here.
            fluidBackdrop?.SetTheme(EnvironmentTheme(chart),chart.backgroundSprite);
        }
        static string EnvironmentTheme(ChartData chart)=>chart!=null&&chart.bossId==RhythmWare.Boss.BonziEncounter.Id?"bonzi-core":chart?.backgroundTheme;
        public void SetBackground(Sprite customSprite)
        {
            if(fluidBackdrop!=null)fluidBackdrop.SetTheme(EnvironmentTheme(ChartManager.Instance?.SelectedChart),customSprite);
            if (backgroundRenderer != null)
            {
                if (customSprite != null)
                {
                    backgroundRenderer.sprite = customSprite;
                    backgroundRenderer.color = Color.white;
                }
                else
                {
                    backgroundRenderer.sprite = defaultBackgroundSprite != null ? defaultBackgroundSprite : CreatePlaceholderBGSprite();
                    backgroundRenderer.color = defaultBackgroundColor;
                }
                FitToCamera();
            }
        }

        public void UpdateDimOverlay(float dimValue)
        {
            if (dimOverlayRenderer != null)
            {
                float strength=Mathf.Clamp01(dimValue);
                // Preserve the saved slider position and its fully dark end,
                // with finer control over the luminous illustrated arena.
                dimOverlayRenderer.color = new Color(0f,0f,0f,fluidBackdrop!=null?strength*strength:strength);
            }
        }

        public void FitToCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            float height = 2f * cam.orthographicSize;
            float width = height * cam.aspect;

            Vector3 targetScale = new Vector3(width, height, 1f);

            if (dimOverlayRenderer != null)
            {
                dimOverlayRenderer.transform.localScale = targetScale * 1.1f;
            }

            if (backgroundRenderer != null && backgroundRenderer.sprite != null)
            {
                Vector2 spriteSize = backgroundRenderer.sprite.rect.size / backgroundRenderer.sprite.pixelsPerUnit;
                if (spriteSize.x > 0 && spriteSize.y > 0)
                {
                    float scaleX = width / spriteSize.x;
                    float scaleY = height / spriteSize.y;
                    float maxScale = Mathf.Max(scaleX, scaleY);
                    backgroundRenderer.transform.localScale = new Vector3(maxScale, maxScale, 1f);
                }
            }
            if(stageRenderer!=null)
            {
                Rect rect=RhythmWare.UI.EpicStageLayout.WorldRect(cam);
                stageRenderer.transform.position=new Vector3(rect.center.x,rect.center.y,10);
                Vector2 size=stageRenderer.sprite.bounds.size;
                stageRenderer.transform.localScale=new Vector3(rect.width/size.x,rect.height/size.y,1);
            }
            if(fluidBackdrop!=null)fluidBackdrop.FitToCamera();
        }

        private Sprite CreatePlaceholderBGSprite()
        {
            Texture2D tex = new Texture2D(64, 64);
            Color darkGrid = new Color(0.05f, 0.07f, 0.12f, 1f);
            Color lineGrid = new Color(0.08f, 0.11f, 0.18f, 1f);

            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    bool isLine = (x % 16 == 0 || y % 16 == 0);
                    tex.SetPixel(x, y, isLine ? lineGrid : darkGrid);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);
        }

        private Sprite CreateSolidWhiteSprite()
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}

