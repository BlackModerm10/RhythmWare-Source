using System;
using System.Collections.Generic;
using UnityEngine;
using RhythmWare.Malware;
using RhythmWare.Core;

namespace RhythmWare.Rhythm
{
    public enum ChartDifficulty
    {
        Easy,
        Normal,
        Hard
    }

    [CreateAssetMenu(fileName = "NewChartData", menuName = "RhythmWare/Chart Data")]
    public class ChartData : ScriptableObject
    {
        public int formatVersion=2;
        public string laneLayout=LaneLayout.Id;
        public string backgroundTheme="candy-city";
        public double approachSeconds=2.0;
        public double playableEndSeconds;
        public float timingWindowScale=1f;
        [NonSerialized] public string conversionNotice="";
        public bool IsLegacy => laneLayout==LaneLayout.LegacyId;
        public string songTitle = "New Level";
        public string chartId = "";
        public string difficultyId = "";
        public string mapper = "";
        public string bossId = "";
        public double[] bossPhaseStarts = Array.Empty<double>();
        public string tutorialId = "";
        // Allow even a note at audio time zero to travel its full approach.
        public double PlaybackLeadIn => Math.Max(1,GameSettings.NoteTravelSeconds+.35+Math.Max(0,GameSettings.AudioOffsetSec));
        public string StableId
        {
            get
            {
                if (string.IsNullOrEmpty(chartId))
                {
                    // Legacy migration: deterministic identity, independent of installation folder.
                    string identity = songTitle + "|" + artist + "|" + System.IO.Path.GetFileName(audioClipPath ?? "");
                    using (var hash = System.Security.Cryptography.SHA256.Create())
                        chartId = "legacy-" + BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(identity))).Replace("-", "").ToLowerInvariant();
                }
                return chartId;
            }
        }
        public string DifficultyId => string.IsNullOrEmpty(difficultyId) ? difficulty.ToString() : difficultyId;
        public string artist = "Unknown Artist";
        public float bpm = 120f;
        public float offsetMs = 0f;
        public ChartDifficulty difficulty = ChartDifficulty.Normal;
        public AudioClip audioClip;
        public string audioClipPath = "";
        public Sprite backgroundSprite;

        [Header("Notes Chart")]
        public List<NoteData> notes = new List<NoteData>();

        public string ToJson()
        {
            ChartJsonDto dto = new ChartJsonDto
            {
                formatVersion=this.formatVersion,laneLayout=this.laneLayout,backgroundTheme=this.backgroundTheme,approachSeconds=this.approachSeconds,timingWindowScale=this.timingWindowScale,playableEndSeconds=this.playableEndSeconds,
                chartId = this.StableId,
                difficultyId = this.difficultyId,
                mapper = this.mapper,
                bossId = this.bossId,
                bossPhaseStarts = this.bossPhaseStarts,
                tutorialId = this.tutorialId,
                songTitle = this.songTitle,
                artist = this.artist,
                bpm = this.bpm,
                offsetMs = this.offsetMs,
                difficulty = this.difficulty.ToString(),
                audioClipPath = GameContentPaths.PortableAudio(this.audioClipPath),
                notes = new List<NoteJsonDto>()
            };

            foreach (var note in notes)
            {
                dto.notes.Add(new NoteJsonDto
                {
                    timestamp = note.timestamp,
                    laneIndex = note.laneIndex,
                    malwareTypeName = note.malwareData != null ? note.malwareData.malwareName : "Default",
                    speedMultiplier = note.speedMultiplier,
                    duration = note.duration
                });
            }

            return JsonUtility.ToJson(dto, true);
        }

        public static Dictionary<string, MalwareData> GetDefaultMalwareCatalog()
        {
            var dict = new Dictionary<string, MalwareData>(StringComparer.OrdinalIgnoreCase);
            AddMalwareToCatalog(dict, ScriptableObject.CreateInstance<SpywareData>());
            AddMalwareToCatalog(dict, ScriptableObject.CreateInstance<RansomwareData>());
            AddMalwareToCatalog(dict, ScriptableObject.CreateInstance<TrojanData>());
            AddMalwareToCatalog(dict, ScriptableObject.CreateInstance<AdwareData>());
            AddMalwareToCatalog(dict, ScriptableObject.CreateInstance<DDoSData>());
            AddMalwareToCatalog(dict, ScriptableObject.CreateInstance<WormData>());
            AddMalwareToCatalog(dict, ScriptableObject.CreateInstance<PhishingData>());
            AddMalwareToCatalog(dict, ScriptableObject.CreateInstance<KeyloggerData>());
            AddMalwareToCatalog(dict, ScriptableObject.CreateInstance<RootkitData>());
            return dict;
        }

        private static void AddMalwareToCatalog(Dictionary<string, MalwareData> dict, MalwareData m)
        {
            if (m == null) return;
            dict[m.malwareName] = m;
            dict[m.GetType().Name] = m;
            string simple = m.GetType().Name.Replace("Data", "");
            dict[simple] = m;
        }

        public static ChartData FromJson(string json, Dictionary<string, MalwareData> malwareCatalog = null)
        {
            ChartJsonDto dto = JsonUtility.FromJson<ChartJsonDto>(json);
            if (dto == null) return null;

            if (malwareCatalog == null)
            {
                malwareCatalog = GetDefaultMalwareCatalog();
            }

            ChartData chart = ScriptableObject.CreateInstance<ChartData>();
            chart.formatVersion=dto.formatVersion;
            chart.laneLayout=string.IsNullOrEmpty(dto.laneLayout)?LaneLayout.LegacyId:dto.laneLayout;
            if(chart.laneLayout!=LaneLayout.Id&&chart.laneLayout!=LaneLayout.LegacyId)throw new System.IO.InvalidDataException("Formato de carriles no compatible: "+chart.laneLayout);
            if(dto.formatVersion>2)throw new System.IO.InvalidDataException("Versión de mapa no compatible.");
            chart.backgroundTheme=string.IsNullOrEmpty(dto.backgroundTheme)?"candy-city":dto.backgroundTheme;
            chart.approachSeconds=dto.approachSeconds>=1&&dto.approachSeconds<=5?dto.approachSeconds:2.0;
            if(double.IsNaN(dto.playableEndSeconds)||double.IsInfinity(dto.playableEndSeconds)||dto.playableEndSeconds<0)throw new System.IO.InvalidDataException("Final de mapa inválido.");
            chart.playableEndSeconds=dto.playableEndSeconds;
            if(float.IsNaN(dto.timingWindowScale)||float.IsInfinity(dto.timingWindowScale)||dto.timingWindowScale<0||dto.timingWindowScale>2)throw new System.IO.InvalidDataException("Ventana de ritmo inválida.");
            chart.timingWindowScale=dto.timingWindowScale>0?Mathf.Max(.75f,dto.timingWindowScale):1f;
            chart.chartId = dto.chartId ?? "";
            chart.difficultyId = dto.difficultyId ?? "";
            chart.mapper = dto.mapper ?? "";
            chart.bossId = dto.bossId ?? "";
            chart.bossPhaseStarts=dto.bossPhaseStarts??Array.Empty<double>();
            if(chart.bossPhaseStarts.Length!=0)
            {
                if(chart.bossPhaseStarts.Length!=3||chart.bossPhaseStarts[0]!=0)throw new System.IO.InvalidDataException("El boss necesita tres fases desde cero.");
                for(int i=0;i<3;i++)if(double.IsNaN(chart.bossPhaseStarts[i])||double.IsInfinity(chart.bossPhaseStarts[i])||chart.bossPhaseStarts[i]<0||(i>0&&chart.bossPhaseStarts[i]<=chart.bossPhaseStarts[i-1]))
                    throw new System.IO.InvalidDataException("Inicio de fase inválido.");
            }
            chart.tutorialId = dto.tutorialId ?? "";
            chart.songTitle = dto.songTitle;
            chart.artist = dto.artist;
            chart.bpm = dto.bpm > 0 ? dto.bpm : 120f;
            chart.offsetMs = dto.offsetMs;
            chart.audioClipPath = dto.audioClipPath ?? "";

            if (Enum.TryParse<ChartDifficulty>(dto.difficulty, true, out var parsedDiff))
            {
                chart.difficulty = parsedDiff;
            }

            chart.notes = new List<NoteData>();
            if (dto.notes != null)
            {
                foreach (var noteDto in dto.notes)
                {
                    int limit=chart.IsLegacy?7:LaneLayout.Count;
                    if(noteDto.laneIndex<0||noteDto.laneIndex>=limit||double.IsNaN(noteDto.timestamp)||double.IsInfinity(noteDto.timestamp)||noteDto.timestamp<0||double.IsNaN(noteDto.duration)||double.IsInfinity(noteDto.duration)||noteDto.duration<0||noteDto.duration>3600||float.IsNaN(noteDto.speedMultiplier)||float.IsInfinity(noteDto.speedMultiplier))
                        throw new System.IO.InvalidDataException("Notas fuera de rango o no finitas.");
                    MalwareData mData = null;
                    if (malwareCatalog != null && !string.IsNullOrEmpty(noteDto.malwareTypeName) && noteDto.malwareTypeName != "Default" && noteDto.malwareTypeName != "Normal")
                    {
                        malwareCatalog.TryGetValue(noteDto.malwareTypeName, out mData);
                        if (mData == null)
                        {
                            foreach (var kvp in malwareCatalog)
                            {
                                if (noteDto.malwareTypeName.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    kvp.Key.IndexOf(noteDto.malwareTypeName, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    mData = kvp.Value;
                                    break;
                                }
                            }
                        }
                    }

                    chart.notes.Add(new NoteData
                    {
                        timestamp = noteDto.timestamp,
                        laneIndex = noteDto.laneIndex,
                        malwareData = mData,
                        speedMultiplier = noteDto.speedMultiplier > 0 ? noteDto.speedMultiplier : 1.0f,
                        duration = noteDto.duration
                    });
                }
            }

            // Keep the campaign's readable stealth profile after editor export
            // and reload, without mutating the shared editor malware palette.
            if(chart.tutorialId.StartsWith("sara-basics")||chart.bossId=="bonzi-buddy")
            {
                SpywareData readableSpyware=null;
                foreach(var note in chart.notes)if(note.malwareData is SpywareData source)
                {
                    if(readableSpyware==null){readableSpyware=Instantiate(source);readableSpyware.minOpacity=chart.tutorialId.StartsWith("sara-basics")?.65f:.40f;readableSpyware.fadeStartProgress=.4f;}
                    note.malwareData=readableSpyware;
                }
            }
            chart.SortNotes();
            chart.chartId=chart.StableId; // Migrate legacy identity before metadata can be edited.
            return chart;
        }

        public void SortNotes()
        {
            notes.Sort((a, b) => a.timestamp.CompareTo(b.timestamp));
        }
    }

    [Serializable]
    public class ChartJsonDto
    {
        public int formatVersion;
        public string laneLayout;
        public string backgroundTheme;
        public double approachSeconds;
        public double playableEndSeconds;
        public float timingWindowScale;
        public string bossId;
        public double[] bossPhaseStarts;
        public string tutorialId;
        public string chartId;
        public string difficultyId;
        public string mapper;
        public string songTitle;
        public string artist;
        public float bpm;
        public float offsetMs;
        public string difficulty;
        public string audioClipPath;
        public List<NoteJsonDto> notes;
    }

    [Serializable]
    public class NoteJsonDto
    {
        public double timestamp;
        public int laneIndex;
        public string malwareTypeName;
        public float speedMultiplier;
        public double duration;
    }
}
