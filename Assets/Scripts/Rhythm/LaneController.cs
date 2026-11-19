using System.Collections;
using UnityEngine;
using RhythmWare.Core;

namespace RhythmWare.Rhythm
{
    public class LaneController : MonoBehaviour
    {
        public static LaneController Instance { get; private set; }

        [Header("Spawn Origin (Top Central Trunk Opening)")]
        [SerializeField] private Transform spawnOrigin;

        [Header("Hitbox Destinations (0-1: Left, 2-3: Right, 4: SPACE)")]
        [SerializeField] private Transform[] laneHitboxes = new Transform[LaneLayout.Count];

        [Header("Perspective Scaling")]
        [SerializeField] private float minSpawnScale = 0.35f;
        [SerializeField] private float maxHitboxScale = 0.95f;

        private Vector3[] baseScales = new Vector3[LaneLayout.Count];
        private Color[] baseColors = new Color[LaneLayout.Count];
        private bool visualsInitialized;
        public bool UsesEpicStage { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Configure(Transform origin,Transform[] destinations,bool useEpicStage=false)
        { spawnOrigin=origin;laneHitboxes=destinations;UsesEpicStage=useEpicStage; }

        public void SetReceptorRestScale(int lane,Vector3 scale)
        {
            baseScales[lane]=scale;
            if(laneHitboxes[lane]!=null)laneHitboxes[lane].localScale=scale;
        }

        private void Start()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnLanePressed += HandleLanePressedVisualFeedback;
                InputManager.Instance.OnBindingsChanged += RefreshKeyLabels;
            }

            for (int i = 0; i < laneHitboxes.Length; i++)
            {
                if (laneHitboxes[i] != null)
                {
                    baseScales[i] = laneHitboxes[i].localScale;
                    SpriteRenderer sr = laneHitboxes[i].GetComponent<SpriteRenderer>();
                    if (sr != null) baseColors[i] = sr.color;
                }
            }
            visualsInitialized=true;RefreshKeyLabels();
        }

        private void OnDisable()
        {
            if(!visualsInitialized)return;
            StopAllCoroutines();scrambleCoroutine=null;infectCoroutine=null;
            for(int i=0;i<laneHitboxes.Length;i++)
            {
                var hb=laneHitboxes[i];if(hb==null)continue;
                hb.localScale=baseScales[i];var sr=hb.GetComponent<SpriteRenderer>();if(sr!=null)sr.color=baseColors[i];
                var label=hb.GetComponentInChildren<TextMesh>();if(label!=null){label.text=InputManager.Instance.LabelForLane(i);label.color=Color.white;}
            }
        }

        private void OnDestroy()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnLanePressed -= HandleLanePressedVisualFeedback;
                InputManager.Instance.OnBindingsChanged -= RefreshKeyLabels;
            }
        }

        public void RefreshKeyLabels()
        {
            if(InputManager.Instance==null)return;
            for(int i=0;i<laneHitboxes.Length;i++){if(laneHitboxes[i]==null)continue;var label=laneHitboxes[i].GetComponentInChildren<TextMesh>();if(label==null)continue;label.text=InputManager.Instance.LabelForLane(i);if(UsesEpicStage)FitKeyLabel(i,label);}
        }
        public void FitKeyLabel(int lane,TextMesh label)
        {
            if(Camera.main==null)return;
            var scale=RhythmWare.UI.EpicStageLayout.ReceptorScale(lane);
            label.transform.localScale=new Vector3(1/scale.x,1/scale.y,1);
            float unit=RhythmWare.UI.EpicStageLayout.WorldRect(Camera.main).height/RhythmWare.UI.EpicStageLayout.ReferenceHeight;
            label.characterSize=.115f;
            var bounds=label.GetComponent<MeshRenderer>().bounds.size;
            var size=RhythmWare.UI.EpicStageLayout.ReceptorSize(lane);
            float factor=Mathf.Min(size.x*unit*.70f/Mathf.Max(.0001f,bounds.x),size.y*unit*.62f/Mathf.Max(.0001f,bounds.y));
            label.characterSize*=factor;
        }
        private void HandleLanePressedVisualFeedback(int laneIndex)
        {
            if (!gameObject.activeInHierarchy) return;
            if (laneIndex >= 0 && laneIndex < laneHitboxes.Length && laneHitboxes[laneIndex] != null)
            {
                StartCoroutine(PulseHitboxRoutine(laneIndex));
            }
        }

        private IEnumerator PulseHitboxRoutine(int laneIndex)
        {
            Transform hb = laneHitboxes[laneIndex];
            if (hb == null) yield break;

            SpriteRenderer sr = hb.GetComponent<SpriteRenderer>();
            Vector3 targetScale = baseScales[laneIndex] * (UsesEpicStage?1.06f:1.35f);
            Color flashColor = Color.white;

            float duration = 0.12f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                hb.localScale = Vector3.Lerp(targetScale, baseScales[laneIndex], t);
                if (sr != null) sr.color = Color.Lerp(flashColor, baseColors[laneIndex], t);
                yield return null;
            }

            hb.localScale = baseScales[laneIndex];
            if (sr != null) sr.color = baseColors[laneIndex];
        }

        private Coroutine scrambleCoroutine;
        private Coroutine infectCoroutine;

        public void ScrambleKeyLabels(float duration)
        {
            if (!gameObject.activeInHierarchy) return;
            if (scrambleCoroutine != null) StopCoroutine(scrambleCoroutine);
            scrambleCoroutine = StartCoroutine(ScrambleRoutine(duration));
        }

        private IEnumerator ScrambleRoutine(float duration)
        {
            string[] originalLabels = new string[5];for(int i=0;i<5;i++)originalLabels[i]=InputManager.Instance.LabelForLane(i);
            string[] glitchChars = new string[] { "?", "*", "#", "@", "%", "!", "&", "$", "0", "1" };

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += 0.1f;
                for (int i = 0; i < laneHitboxes.Length; i++)
                {
                    if (laneHitboxes[i] == null) continue;
                    var tm = laneHitboxes[i].GetComponentInChildren<TextMesh>();
                    if (tm != null)
                    {
                        tm.text = glitchChars[Random.Range(0, glitchChars.Length)];
                        tm.color = new Color(0.3f, 0.6f, 1f);
                    }
                }
                yield return new WaitForSeconds(0.1f);
            }

            for (int i = 0; i < laneHitboxes.Length; i++)
            {
                if (laneHitboxes[i] == null) continue;
                var tm = laneHitboxes[i].GetComponentInChildren<TextMesh>();
                if (tm != null && i < originalLabels.Length)
                {
                    tm.text = originalLabels[i];
                    tm.color = Color.white;
                }
            }
            scrambleCoroutine = null;
        }

        public void InfectHitboxes(float duration)
        {
            if (!gameObject.activeInHierarchy) return;
            if (infectCoroutine != null) StopCoroutine(infectCoroutine);
            infectCoroutine = StartCoroutine(InfectRoutine(duration));
        }

        private IEnumerator InfectRoutine(float duration)
        {
            float elapsed = 0f;
            Color toxicGreen = new Color(0.1f, 1.0f, 0.3f, 1.0f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float pulse = (Mathf.Sin(elapsed * 12f) + 1f) * 0.5f;

                for (int i = 0; i < laneHitboxes.Length; i++)
                {
                    if (laneHitboxes[i] == null) continue;
                    var sr = laneHitboxes[i].GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.color = Color.Lerp(baseColors[i], toxicGreen, pulse * 0.85f);
                    }
                }
                yield return null;
            }

            for (int i = 0; i < laneHitboxes.Length; i++)
            {
                if (laneHitboxes[i] == null) continue;
                var sr = laneHitboxes[i].GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = baseColors[i];
            }
            infectCoroutine = null;
        }

        public Vector3 GetSpawnPosition()
        {
            return spawnOrigin != null ? spawnOrigin.position : new Vector3(0f, 4.0f, 0f);
        }

        public Vector3 GetHitboxPosition(int laneIndex)
        {
            if (laneHitboxes != null && laneIndex >= 0 && laneIndex < laneHitboxes.Length && laneHitboxes[laneIndex] != null)
            {
                return laneHitboxes[laneIndex].position;
            }
            return transform.position;
        }

        Vector3 ApproachPoint(int lane,float worldY)
        {
            Vector3 origin=GetSpawnPosition();
            float depth=Mathf.InverseLerp(origin.y,GetHitboxPosition(LaneLayout.Center).y,worldY);
            float pixelY=Mathf.Lerp(RhythmWare.UI.EpicStageLayout.OriginPixel.y,990,depth);
            float unit=(origin.y-GetHitboxPosition(LaneLayout.Center).y)/(990-RhythmWare.UI.EpicStageLayout.OriginPixel.y);
            float x=origin.x+RhythmWare.UI.EpicStageLayout.ApproachSlot(lane)*RhythmWare.UI.EpicStageLayout.RoadHalfWidth(pixelY)*unit;
            return new Vector3(x,worldY,origin.z);
        }

        public Vector3 GetLaneSpawnPosition(int lane) => UsesEpicStage?RhythmWare.UI.EpicStageLayout.PixelToWorld(RhythmWare.UI.EpicStageLayout.RoutePixel(lane,0)):GetSpawnPosition();
        public Vector3 GetLaneCornerPosition(int lane) => UsesEpicStage?RhythmWare.UI.EpicStageLayout.PixelToWorld(RhythmWare.UI.EpicStageLayout.RoutePixel(lane,.65f)):
            new Vector3(GetSpawnPosition().x,GetHitboxPosition(lane).y,GetSpawnPosition().z);
        float CornerFraction(int lane)
        {
            if(UsesEpicStage)return .65f;
            Vector3 start=GetLaneSpawnPosition(lane),corner=GetLaneCornerPosition(lane),end=GetHitboxPosition(lane);
            float trunk=Vector3.Distance(start,corner),total=trunk+Vector3.Distance(corner,end);
            return total>0?trunk/total:1;
        }
        public float GetLaneCornerProgress(int lane) => RhythmWare.UI.EpicStageLayout.UnprojectProgress(CornerFraction(lane));

        public Vector3 ConstrainVisualOffset(int lane,Vector3 route,Vector3 displaced)
        {
            if(!UsesEpicStage)return displaced;
            // Malware can still wobble, but cannot cross the centre or steal
            // its neighbour's approach. The SPACE route is always centred.
            float allowance=lane==LaneLayout.Center?0:EpicStageLayoutUnit()*6;
            displaced.x=Mathf.Clamp(displaced.x,route.x-allowance,route.x+allowance);
            return displaced;
        }
        static float EpicStageLayoutUnit()=>RhythmWare.UI.EpicStageLayout.WorldRect(Camera.main).height/RhythmWare.UI.EpicStageLayout.ReferenceHeight;

        /// <summary>
        /// Travel down the assigned road approach, then through its side exit.
        /// The calibrated stage separates both hands from the horizon; legacy
        /// boards retain their original central-trunk movement.
        /// </summary>
        public Vector3 EvaluateLanePosition(int laneIndex, float progress)
        {
            Vector3 start = GetSpawnPosition();
            Vector3 end = GetHitboxPosition(laneIndex);
            float t = Mathf.Clamp01(progress);

            if(UsesEpicStage)
            {
                float projected=RhythmWare.UI.EpicStageLayout.ProjectProgress(progress);
                return RhythmWare.UI.EpicStageLayout.PixelToWorld(RhythmWare.UI.EpicStageLayout.RoutePixel(laneIndex,projected));
            }

            // Center Special (Lane 6) moves straight down the central trunk
            if (laneIndex==LaneLayout.Center)
            {
                return Vector3.Lerp(start, end, t);
            }

            // Side Branches (0-2 Left, 3-5 Right)
            Vector3 junctionPoint = new Vector3(start.x, end.y, start.z);

            // First half of path (0.0 to 0.5): Move down central trunk
            if (t < 0.5f)
            {
                float localT = t / 0.5f;
                // Smooth ease in down the trunk
                float smoothT = localT * localT;
                return Vector3.Lerp(start, junctionPoint, smoothT);
            }
            // Second half of path (0.5 to 1.0): Turn sideways into branch channel towards target box
            else
            {
                float localT = (t - 0.5f) / 0.5f;
                return Vector3.Lerp(junctionPoint, end, localT);
            }
        }

        public Vector3 EvaluateLaneScale(float progress)
        { return EvaluateLaneScale(LaneLayout.Center,progress); }

        public Vector3 EvaluateLaneScale(int lane,float progress)
        {
            if(UsesEpicStage)
            {
                float unit=RhythmWare.UI.EpicStageLayout.WorldRect(Camera.main).height/RhythmWare.UI.EpicStageLayout.ReferenceHeight;
                // Depth must not change the size of the musical timing cue.
                return Vector3.one*RhythmWare.UI.EpicStageLayout.NoteSizePixels*unit;
            }
            float scale = Mathf.Lerp(minSpawnScale, maxHitboxScale, Mathf.Clamp01(progress));
            return Vector3.one * scale;
        }

        private void OnDrawGizmos()
        {
            Vector3 origin = GetSpawnPosition();
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(origin, 0.3f);

            if (laneHitboxes == null) return;

            for (int i = 0; i < laneHitboxes.Length; i++)
            {
                if (laneHitboxes[i] != null)
                {
                    Gizmos.color = RhythmWare.UI.EpicStageLayout.LaneColor(i);
                    Vector3 start=GetLaneSpawnPosition(i);
                    Vector3 end = laneHitboxes[i].position;
                    if (i==LaneLayout.Center)
                    {
                        Gizmos.DrawLine(start, end);
                    }
                    else
                    {
                        Vector3 j = GetLaneCornerPosition(i);
                        Gizmos.DrawLine(start, j);
                        Gizmos.DrawLine(j, end);
                    }
                    Gizmos.DrawWireCube(end, Vector3.one * 0.4f);
                }
            }
        }
    }
}
