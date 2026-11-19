using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace RhythmWare.Core
{
    [Serializable]
    public class LeaderboardScore
    {
        public string playerName, chartId, difficultyId, rank, timestamp;
        public int score, maxCombo, place;
        public float accuracy;
    }
    public interface ILeaderboardProvider
    {
        IReadOnlyList<LeaderboardScore> GetScores(string chartId, string difficultyId);
        void Submit(LeaderboardScore score);
    }
    public static class Leaderboards
    {
        public static ILeaderboardProvider Provider { get; set; } = new LocalLeaderboardProvider();
    }
    // Independent of views and PlayerPrefs. Can be replaced with an online provider.
    public sealed class LocalLeaderboardProvider : ILeaderboardProvider
    {
        [Serializable] class Store { public List<LeaderboardScore> entries = new List<LeaderboardScore>(); }
        readonly string filePath;
        Store store;
        public LocalLeaderboardProvider(string path = null)
        {
            filePath = path ?? Path.Combine(ProfileStorage.DataRoot, "scores-v1.json");
            try { store = File.Exists(filePath) ? JsonUtility.FromJson<Store>(File.ReadAllText(filePath)) : new Store(); }
            catch (Exception ex) { Debug.LogWarning("Scores: " + ex.Message); }
            if (store == null || store.entries == null) store = new Store();
        }
        public IReadOnlyList<LeaderboardScore> GetScores(string chartId, string difficultyId) =>
            store.entries.Where(e => e.chartId == chartId && e.difficultyId == difficultyId)
                .OrderByDescending(e => e.score).ThenByDescending(e => e.accuracy).Take(50).ToList();
        public void Submit(LeaderboardScore entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.chartId)) return;
            store.entries.Add(entry);
            // Bounded per chart/difficulty; no fictitious seed scores.
            var keep = store.entries.Where(e => e.chartId == entry.chartId && e.difficultyId == entry.difficultyId)
                .OrderByDescending(e => e.score).ThenByDescending(e => e.accuracy).Take(50).ToList();
            store.entries.RemoveAll(e => e.chartId == entry.chartId && e.difficultyId == entry.difficultyId && !keep.Contains(e));
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            string temp = filePath + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(store, true));
            File.Copy(temp, filePath, true); File.Delete(temp);
        }
    }
}
