using UnityEngine;
using RhythmWare.Core;
using RhythmWare.Malware;

namespace RhythmWare.Rhythm
{
    public class NoteController : MonoBehaviour
    {
        public NoteData Data { get; private set; }
        public SpriteRenderer SpriteRenderer { get; private set; }
        public int LaneIndex => Data != null ? Data.laneIndex : 0;
        public double TargetTimestamp => Data != null ? Data.timestamp : 0;

        public bool IsHit { get; private set; }
        public bool IsMissed { get; private set; }
        public bool IsHolding { get; private set; }
        public bool UsesCharacterAnimation => presentation != null && presentation.UsesCharacterAnimation;
        public double AnimationElapsed { get; private set; }
        public double TravelDuration => scrollTimeSec;
        private RhythmWare.UI.NotePresentation presentation;

        private double scrollTimeSec = 1.5; // Seconds taken to move from spawn to hitbox
        private float missToleranceSec=TimingWindow.BaseMiss;
        private LineRenderer holdLineRenderer;
        private double holdStartTime = 0.0;
        private double nextTickTime = 0.0;
        private TextMesh badgeText;
        private HitRating resolvedRating=HitRating.Perfect;

        private void OnDestroy()
        { if(Data!=null&&(IsHit||IsMissed)&&NoteSpawner.Instance!=null)NoteSpawner.Instance.NotifyResolved(Data,IsMissed?HitRating.Miss:resolvedRating); }
        private void Awake()
        {
            SpriteRenderer = GetComponent<SpriteRenderer>();
            if (SpriteRenderer == null)
            {
                SpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            EnsureBadge();
        }

        private void EnsureBadge()
        {
            if (badgeText != null) return;
            Transform bT = transform.Find("BadgeText");
            if (bT == null)
            {
                GameObject bObj = new GameObject("BadgeText");
                bObj.transform.SetParent(transform, false);
                bObj.transform.localPosition = new Vector3(0f, 0f, -0.1f);
                badgeText = bObj.AddComponent<TextMesh>();
                badgeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (badgeText.font == null) badgeText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                var mr = bObj.GetComponent<MeshRenderer>();
                if (mr != null) mr.sortingOrder = 12;
                badgeText.alignment = TextAlignment.Center;
                badgeText.anchor = TextAnchor.MiddleCenter;
                badgeText.fontSize = 22;
                badgeText.characterSize = 0.085f;
                badgeText.fontStyle = FontStyle.Bold;
            }
            else
            {
                badgeText = bT.GetComponent<TextMesh>();
            }
        }

        public void Initialize(NoteData data, double scrollTime,float missTolerance=TimingWindow.BaseMiss)
        {
            missToleranceSec=missTolerance;
            Data = data;
            if(data.malwareData!=null)RhythmWare.Education.SocLearningProgress.LastThreat=RhythmWare.Education.CyberQuestionBank.Normalize(data.malwareData.GetType().Name);
            SetScrollDuration(scrollTime);
            IsHit = false;
            IsMissed = false;
            IsHolding = false;

            EnsureBadge();
            UpdateBadgeInitial();

            if (Data.malwareData != null)
            {
                Data.malwareData.OnNoteSpawn(this);
            }

            presentation = gameObject.AddComponent<RhythmWare.UI.NotePresentation>();
            presentation.Initialize(this);
            // The artist's face replaces the old SPY/AD/BONUS text badge.
            if (UsesCharacterAnimation && badgeText != null) badgeText.gameObject.SetActive(false);

            if (data.isHoldNote)
            {
                SetupHoldVisuals();
            }

            RefreshVisuals(AudioManager.Instance != null ? AudioManager.Instance.SongTime : 0.0);
        }
        public static double TravelSecondsFor(NoteData data,double seconds)
        {return seconds;} // One timeline scale for every note and hold tail.
        public void SetScrollDuration(double seconds){scrollTimeSec=TravelSecondsFor(Data,seconds);}

        private void UpdateBadgeInitial()
        {
            if (badgeText == null) return;

            if (Data == null || Data.malwareData == null)
            {
                badgeText.text = "";
                return;
            }

            var m = Data.malwareData;
            if (m is TrojanData)
            {
                badgeText.text = "BONUS";
                badgeText.color = new Color(0.2f, 1f, 0.4f, 1f);
            }
            else if (m is SpywareData)
            {
                badgeText.text = "SPY";
                badgeText.color = new Color(0.7f, 0.3f, 1f, 1f);
            }
            else if (m is RansomwareData)
            {
                badgeText.text = "LOCK";
                badgeText.color = new Color(1f, 0.2f, 0.2f, 1f);
            }
            else if (m is AdwareData)
            {
                badgeText.text = "AD";
                badgeText.color = new Color(1f, 0.85f, 0.1f, 1f);
            }
            else if (m is DDoSData)
            {
                badgeText.text = "DDOS";
                badgeText.color = new Color(0.2f, 0.7f, 1f, 1f);
            }
            else if (m is WormData)
            {
                badgeText.text = "WORM";
                badgeText.color = new Color(0.3f, 1f, 0.3f, 1f);
            }
            else if (m is PhishingData)
            {
                badgeText.text = "PHISH";
                badgeText.color = new Color(1f, 0.6f, 0.1f, 1f);
            }
            else if (m is KeyloggerData)
            {
                badgeText.text = "KEY";
                badgeText.color = new Color(0.4f, 0.6f, 1f, 1f);
            }
            else if (m is RootkitData)
            {
                badgeText.text = "ROOT";
                badgeText.color = new Color(1f, 0.1f, 0.2f, 1f);
            }
            else
            {
                badgeText.text = $"[{m.malwareName.ToUpper()}]";
                badgeText.color = m.nodeColor;
            }
        }

        private void SetupHoldVisuals()
        {
            holdLineRenderer = gameObject.AddComponent<LineRenderer>();
            holdLineRenderer.useWorldSpace = true;
            holdLineRenderer.positionCount = 6;
            holdLineRenderer.startWidth = 0.42f;
            holdLineRenderer.endWidth = 0.22f;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader != null)
            {
                holdLineRenderer.sharedMaterial = RhythmWare.UI.NetworkVisualAssets.Material;
            }

            Color baseColor;
            if (LaneController.Instance != null && LaneController.Instance.UsesEpicStage) baseColor = RhythmWare.UI.EpicStageLayout.LaneColor(Data.laneIndex);
            else if (Data.malwareData != null) baseColor = Data.malwareData.nodeColor;
            else if (Data.laneIndex==LaneLayout.Center) baseColor = new Color(1f, 0.1f, 0.85f, 1f);
            else if (Data.laneIndex < LaneLayout.LeftCount) baseColor = new Color(0f, 0.9f, 1f, 1f);
            else baseColor = new Color(0.2f, 1f, 0.4f, 1f);

            holdLineRenderer.startColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.75f);
            holdLineRenderer.endColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.35f);
            holdLineRenderer.sortingOrder = 5;
        }

        public void UpdatePosition(double songTime)
        {UpdatePositionCore(songTime,true);}
        public void RefreshVisuals(double songTime)
        {UpdatePositionCore(songTime,false);}
        void UpdatePositionCore(double songTime,bool judge)
        {
            if (Data == null) return;
            AnimationElapsed = System.Math.Max(0, songTime - (Data.timestamp - scrollTimeSec));

            if (IsHolding)
            {
                if(judge)UpdateHoldingState(songTime);
                UpdateHoldLineVisuals(songTime);
                return;
            }

            if (IsHit || IsMissed) return;

            double timeUntilHit = Data.timestamp - songTime;
            // Progress goes from 0.0 (spawned) to 1.0 (at hitbox)
            float progress = (float)(1.0 - (timeUntilHit / scrollTimeSec));

            if (LaneController.Instance != null)
            {
                Vector3 basePos = LaneController.Instance.EvaluateLanePosition(Data.laneIndex, progress);
                Vector3 routePosition = basePos;
                Vector3 baseScale = LaneController.Instance.EvaluateLaneScale(Data.laneIndex,progress);

                if (Data.malwareData != null)
                {
                    // Trojan disguise reveal
                    if (Data.malwareData is TrojanData && progress >= 0.5f)
                    {
                        if (badgeText != null && badgeText.text != "TROJAN")
                        {
                            badgeText.text = "TROJAN";
                            badgeText.color = new Color(1f, 0.1f, 0.6f, 1f);
                        }
                    }
                    // Adware bouncy pulse
                    else if (Data.malwareData is AdwareData && !UsesCharacterAnimation)
                    {
                        float pulse = 1.0f + 0.22f * Mathf.Sin(Time.time * 14f);
                        baseScale *= pulse;
                    }
                    // DDoS packet jitter
                    else if (Data.malwareData is DDoSData)
                    {
                        float jitter = (Mathf.Sin(Time.time * 45f) + Mathf.Cos(Time.time * 30f)) * 0.15f;
                        basePos.x += jitter;
                    }
                    // Worm snaking wave
                    else if (Data.malwareData is WormData)
                    {
                        float wave = Mathf.Sin(progress * Mathf.PI * 4f + Time.time * 6f) * 0.35f;
                        basePos.x += wave;
                    }
                    // Spyware stealth sync
                    else if (Data.malwareData is SpywareData)
                    {
                        if (badgeText != null && SpriteRenderer != null)
                        {
                            Color c = badgeText.color;
                            c.a = SpriteRenderer.color.a;
                            badgeText.color = c;
                        }
                    }
                }

                transform.position = LaneController.Instance.ConstrainVisualOffset(Data.laneIndex,routePosition,basePos);
                transform.localScale = baseScale;
            }

            // Trigger malware custom visual updates (e.g. Spyware fading)
            if (Data.malwareData != null)
            {
                Data.malwareData.OnNoteUpdate(this, Mathf.Clamp01(progress));
                if(LaneController.Instance!=null&&LaneController.Instance.UsesEpicStage)
                    transform.position=LaneController.Instance.ConstrainVisualOffset(Data.laneIndex,LaneController.Instance.EvaluateLanePosition(Data.laneIndex,progress),transform.position);
            }

            UpdateHoldLineVisuals(songTime);

            // Check if missed (exceeded miss tolerance window)
            if (judge && timeUntilHit < -missToleranceSec)
            {
                OnMissed();
            }
        }

        public void ReleaseAt(double songTime){if(IsHolding)UpdateHoldingState(songTime,false);}
        private void UpdateHoldingState(double songTime,bool? heldOverride=null)
        {
            bool isHeld = songTime>=Data.endTimestamp || (heldOverride??(InputManager.Instance != null && InputManager.Instance.IsLaneHeld(Data.laneIndex)));

            if (isHeld)
            {
                // Lock note head at the hitbox
                if (LaneController.Instance != null)
                {
                    transform.position = LaneController.Instance.EvaluateLanePosition(Data.laneIndex, 1.0f);
                    transform.localScale = LaneController.Instance.EvaluateLaneScale(Data.laneIndex,1.0f);
                }

                // Periodic tick score additions
                if (songTime >= nextTickTime && songTime < Data.endTimestamp)
                {
                    nextTickTime += 0.15;
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.AddScore(20);
                    }
                }

                // Completed hold duration successfully!
                if (songTime >= Data.endTimestamp)
                {
                    IsHolding = false;
                    IsHit = true;
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.RegisterHit(HitRating.Perfect, Data.laneIndex);
                    }
                    if (Data.malwareData != null)
                    {
                        MalwareEffectManager.Instance?.TriggerNeutralized(Data.malwareData,Data.laneIndex);
                    }
                    Destroy(gameObject);
                }
            }
            else
            {
                // Player released early
                IsHolding = false;
                double heldDuration = songTime - holdStartTime;
                if (heldDuration >= Data.duration * 0.7)
                {
                    IsHit = true;
                    resolvedRating=HitRating.Good;
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.RegisterHit(HitRating.Good, Data.laneIndex);
                    }
                    if (Data.malwareData != null)
                    {
                        MalwareEffectManager.Instance?.TriggerNeutralized(Data.malwareData,Data.laneIndex);
                    }
                }
                else
                {
                    IsMissed = true;
                    if (Data.malwareData != null)Data.malwareData.ExecuteMissPenalty(this);
                    else GameManager.Instance?.RegisterHit(HitRating.Miss, Data.laneIndex);
                }
                Destroy(gameObject);
            }
        }

        private void UpdateHoldLineVisuals(double songTime)
        {
            if (holdLineRenderer == null || LaneController.Instance == null) return;

            float headProg = IsHolding ? 1.0f : Mathf.Max(0,(float)(1.0 - ((Data.timestamp - songTime) / scrollTimeSec)));
            float tailProg = Mathf.Max(0,(float)(1.0 - ((Data.endTimestamp - songTime) / scrollTimeSec)));

            // The ribbon samples the same continuous curve as its head.
            // A three-point corner shortcut would cut through the new turns.
            int samples=LaneController.Instance.UsesEpicStage?49:6;
            holdLineRenderer.positionCount=samples;
            for (int i = 0; i < samples; i++)
            {
                float t = Mathf.Lerp(tailProg, headProg, (float)i / (samples-1));
                Vector3 pt = LaneController.Instance.EvaluateLanePosition(Data.laneIndex, t);
                holdLineRenderer.SetPosition(i, pt);
            }
            if(LaneController.Instance.UsesEpicStage)
            {
                holdLineRenderer.startWidth=LaneController.Instance.EvaluateLaneScale(Data.laneIndex,tailProg).x*.44f;
                holdLineRenderer.endWidth=LaneController.Instance.EvaluateLaneScale(Data.laneIndex,headProg).x*.44f;
            }
        }

        public void OnHit(HitRating rating,double? inputSongTime=null)
        {
            if (IsHit || IsMissed || IsHolding || Data.trainingObserve) return;
            if(rating==HitRating.Miss){OnMissed();return;}
            resolvedRating=rating;

            if (Data.isHoldNote)
            {
                IsHolding = true;
                holdStartTime = inputSongTime??(AudioManager.Instance != null ? AudioManager.Instance.SongTime : Data.timestamp);
                nextTickTime = holdStartTime + 0.15;

                if (GameManager.Instance != null)
                {
                    GameManager.Instance.RegisterHit(rating, Data.laneIndex);
                }
                return;
            }

            IsHit = true;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterHit(rating, Data.laneIndex);
            }

            if (Data.malwareData != null)
            {
                MalwareEffectManager.Instance?.TriggerNeutralized(Data.malwareData,Data.laneIndex);
            }

            Destroy(gameObject);
        }

        public void OnMissed()
        {
            if (IsHit || IsMissed || IsHolding) return;

            IsMissed = true;

            if (Data.malwareData != null)
            {
                Data.malwareData.ExecuteMissPenalty(this);
            }
            else if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterHit(HitRating.Miss, Data.laneIndex);
            }

            Destroy(gameObject);
        }
    }
}
