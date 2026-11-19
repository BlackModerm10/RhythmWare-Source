using RhythmWare.Core;
using System.Collections.Generic;
using UnityEngine;
using RhythmWare.Malware;

namespace RhythmWare.Rhythm
{
    public class ChartManager : MonoBehaviour
    {
        public static ChartManager Instance { get; private set; }

        [Header("Levels Catalog")]
        [SerializeField] private List<ChartData> availableCharts = new List<ChartData>();

        public ChartData SelectedChart { get; private set; }
        public ChartData CurrentChart => SelectedChart;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public List<ChartData> GetAvailableCharts() => availableCharts;

        public void SelectChart(int index)
        {
            if (availableCharts != null && index >= 0 && index < availableCharts.Count)
            {
                SelectedChart = availableCharts[index];
            }
        }

        public void SelectChart(ChartData chart)
        {
            SelectedChart = chart;
        }

        /// <summary>
        /// Generates a test chart with Spyware and base notes for initial rhythm testing.
        /// </summary>
        public ChartData GenerateTestChart(MalwareData baseMalware, SpywareData spyware)
        {
            ChartData testChart = ScriptableObject.CreateInstance<ChartData>();
            testChart.songTitle = "Test Song (120 BPM)";
            testChart.artist = "RhythmWare Engine";
            testChart.bpm = 120f;

            float secPerBeat = 60f / 120f;
            double currentTime = 2.0; // Start after 2s delay

            // Generate 32 beats across five lanes
            for (int i = 0; i < 32; i++)
            {
                int lane = i % LaneLayout.Count;
                MalwareData mData = (i % 4 == 3 && spyware != null) ? spyware : baseMalware;

                testChart.notes.Add(new NoteData
                {
                    timestamp = currentTime,
                    laneIndex = lane,
                    malwareData = mData,
                    speedMultiplier = 1.0f
                });

                currentTime += secPerBeat;
            }

            return testChart;
        }
    }
}
