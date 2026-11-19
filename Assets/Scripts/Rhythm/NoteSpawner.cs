using System.Collections.Generic;
using UnityEngine;
using RhythmWare.Core;

namespace RhythmWare.Rhythm
{
    public class NoteSpawner : MonoBehaviour
    {
        public static NoteSpawner Instance { get; private set; }
        public event System.Action<ChartData> OnChartLoaded;
        public event System.Action<NoteData> OnNoteSpawned;
        public event System.Action<NoteData,HitRating> OnNoteResolved;
        public void NotifyResolved(NoteData note,HitRating rating)=>OnNoteResolved?.Invoke(note,rating);
        public double PlayableDuration=>currentChart!=null&&currentChart.playableEndSeconds>0?System.Math.Min(currentChart.playableEndSeconds,AudioManager.Instance.Duration):AudioManager.Instance.Duration;
        public bool QueueTrainingNotes(System.Collections.Generic.IEnumerable<NoteData> notes)
        {
            if(GameManager.Instance==null||!GameManager.Instance.TrainingSafety)return false;
            pendingNotes.AddRange(notes);pendingNotes.Sort((a,b)=>a.timestamp.CompareTo(b.timestamp));return true;
        }

        [Header("Prefabs & Setup")]
        [SerializeField] private GameObject notePrefab;
        [SerializeField] private double scrollTimeSec = 1.5;
        [SerializeField] private TimingWindow timingWindow = new TimingWindow();

        private ChartData currentChart;
        private List<NoteData> pendingNotes = new List<NoteData>();
        private List<NoteController> activeNotes = new List<NoteController>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        bool inputSubscribed;
        void OnEnable()
        {
            if (!inputSubscribed && InputManager.Instance != null)
            {
                InputManager.Instance.OnLanePressedDsp += HandleLanePressedDsp;
                InputManager.Instance.OnLaneReleasedDsp += HandleLaneReleasedDsp;
                inputSubscribed=true;
            }
        }
        private void Start(){GameSettings.OnNoteSpeedChanged+=SpeedChanged;OnEnable();}

        private void OnDestroy()
        {
            GameSettings.OnNoteSpeedChanged-=SpeedChanged;
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnLanePressedDsp -= HandleLanePressedDsp;
                InputManager.Instance.OnLaneReleasedDsp -= HandleLaneReleasedDsp;
            }
        }

        public void Configure(GameObject prefab) { notePrefab=prefab; }

        public void LoadChart(ChartData chart)
        {
            if(chart!=null&&chart.laneLayout!=LaneLayout.Id)throw new System.InvalidOperationException("Convierte el mapa antiguo desde el editor antes de jugar.");
            RhythmWare.Malware.MalwareEffectManager.Instance?.ResetInfections();
            currentChart = chart;
            timingWindow=TimingWindow.ForChart(chart);
            RhythmWare.Education.SocLearningProgress.LastThreat="Spyware";
            if(chart!=null)foreach(var note in chart.notes)if(note.malwareData!=null){RhythmWare.Education.SocLearningProgress.LastThreat=RhythmWare.Education.CyberQuestionBank.Normalize(note.malwareData.GetType().Name);break;}
            if(chart!=null){scrollTimeSec=GameSettings.NoteTravelSeconds;BackgroundManager.Instance?.ApplyChart(chart);}
            pendingNotes.Clear();
            ClearActiveNotes();

            if (chart != null)
            {
                chart.SortNotes();
                pendingNotes.AddRange(chart.notes);
            }
            OnChartLoaded?.Invoke(chart);
        }

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing) return;
            if (AudioManager.Instance == null || !AudioManager.Instance.IsPlaying) return;

            double currentSongTime = AudioManager.Instance.SongTime;

            // Spawn notes when currentSongTime reaches (note.timestamp - scrollTimeSec)
            for(int i=0;i<pendingNotes.Count;)
            {
                if(pendingNotes[i].timestamp-scrollTimeSec>currentSongTime)break;
                SpawnNote(pendingNotes[i]);pendingNotes.RemoveAt(i);
            }

            // Update all active notes
            for (int i = activeNotes.Count - 1; i >= 0; i--)
            {
                if(GameManager.Instance.CurrentState!=GameState.Playing)break;
                if (activeNotes[i] == null || (activeNotes[i].IsHit && !activeNotes[i].IsHolding) || activeNotes[i].IsMissed)
                {
                    activeNotes.RemoveAt(i);
                }
                else
                {
                    Display(activeNotes[i],currentSongTime);
                }
            }

            // Check victory condition
            if (!GameManager.Instance.TrainingSafety && GameManager.Instance.CurrentState==GameState.Playing && pendingNotes.Count == 0 && activeNotes.Count == 0 && currentSongTime > PlayableDuration - 0.02)
            {
                var boss=RhythmWare.Boss.BonziStageController.Instance;
                if(boss!=null&&boss.IsEncounter&&!boss.IsContained)GameManager.Instance.TriggerGameOver();
                else {AudioManager.Instance.StopSong();GameManager.Instance.TriggerVictory();}
            }
        }

        void Display(NoteController note,double time,bool judge=true)
        {
            note.SetScrollDuration(scrollTimeSec);
            bool visible=note.IsHolding||note.TargetTimestamp-time<=note.TravelDuration+.0001;
            if(note.gameObject.activeSelf!=visible)note.gameObject.SetActive(visible);
            if(visible){if(judge)note.UpdatePosition(time);else note.RefreshVisuals(time);}
        }
        void SpeedChanged(float value)
        {
            scrollTimeSec=GameSettings.NoteTravelSeconds;
            if(AudioManager.Instance==null)return;
            foreach(var note in activeNotes.ToArray())if(note!=null&&!note.IsHit&&!note.IsMissed)Display(note,AudioManager.Instance.SongTime,false);
        }
        private void SpawnNote(NoteData noteData)
        {
            if (notePrefab == null)
            {
                Debug.LogError("[NoteSpawner] notePrefab is not assigned!");
                return;
            }

            GameObject go = Instantiate(notePrefab, LaneController.Instance != null ? LaneController.Instance.GetSpawnPosition() : Vector3.zero, Quaternion.identity, transform);
            NoteController controller = go.GetComponent<NoteController>();
            if (controller == null)
            {
                controller = go.AddComponent<NoteController>();
            }

            go.SetActive(true);
            controller.Initialize(noteData, scrollTimeSec,timingWindow.missWindowSec);
            activeNotes.Add(controller);
            OnNoteSpawned?.Invoke(noteData);
        }

        // Kept as a direct-time entry point for deterministic editor fixtures.
        private void HandleLanePressed(int laneIndex)=>JudgeLane(laneIndex,AudioManager.Instance.SongTime);
        void HandleLanePressedDsp(int laneIndex,double dsp)
        {
            var audio=AudioManager.Instance;
            if(audio!=null&&audio.AcceptsInputDsp(dsp))JudgeLane(laneIndex,audio.SongTimeAtDsp(dsp));
        }
        void HandleLaneReleasedDsp(int laneIndex,double dsp)
        {
            var audio=AudioManager.Instance;
            if(GameManager.Instance.CurrentState!=GameState.Playing||audio==null||!audio.AcceptsInputDsp(dsp))return;
            foreach(var note in activeNotes.ToArray())if(note!=null&&note.LaneIndex==laneIndex&&note.IsHolding)note.ReleaseAt(audio.SongTimeAtDsp(dsp));
        }
        void JudgeLane(int laneIndex,double songTime)
        {
            if(GameManager.Instance==null||GameManager.Instance.CurrentState!=GameState.Playing)return;
            // Callbacks precede Update. A hitch can deliver a valid press before
            // this frame has spawned its note; make due notes available first.
            while(pendingNotes.Count>0&&pendingNotes[0].timestamp-scrollTimeSec<=songTime)
            {var data=pendingNotes[0];pendingNotes.RemoveAt(0);SpawnNote(data);}
            NoteController firstValid=null,missCandidate=null;
            foreach(var note in activeNotes)
            {
                if(note==null||note.Data.trainingObserve||note.LaneIndex!=laneIndex||note.IsHit||note.IsMissed||note.IsHolding)continue;
                double delta=songTime-note.TargetTimestamp;
                if(delta>timingWindow.missWindowSec){note.OnMissed();if(GameManager.Instance.CurrentState!=GameState.Playing)return;continue;}
                if(!timingWindow.CanHit(delta))continue;
                if(timingWindow.Evaluate(delta)!=HitRating.Miss)
                {if(firstValid==null||note.TargetTimestamp<firstValid.TargetTimestamp)firstValid=note;}
                else if(missCandidate==null||note.TargetTimestamp<missCandidate.TargetTimestamp)missCandidate=note;
            }
            // A late miss-band note must not steal a valid next note. If both
            // are valid, resolve the oldest pending note before the next one.
            var target=firstValid??missCandidate;
            if(target!=null)target.OnHit(timingWindow.Evaluate(songTime-target.TargetTimestamp),songTime);
        }

        public void ClearActiveNotes()
        {
            foreach (var note in activeNotes)
            {
                if (note != null) Destroy(note.gameObject);
            }
            activeNotes.Clear();
        }

        public void StopAndClear()
        {
            pendingNotes.Clear();
            ClearActiveNotes();
        }
    }
}

