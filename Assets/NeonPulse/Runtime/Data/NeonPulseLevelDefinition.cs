using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace NeonPulse
{
    [Serializable]
    public sealed class MusicGenerationSettings
    {
        [SerializeField, Min(1)] private int beatsPerBar = 4;
        [SerializeField] private string genre = "Cyberpunk EDM, synthwave, electronic workout music";
        [SerializeField] private string mood = "Energetic, futuristic, motivational and powerful";
        [SerializeField] private string musicalKey = "E minor";
        [SerializeField, Range(10f, 120f)] private float maximumSegmentDurationSeconds = 30f;
        [SerializeField, TextArea(2, 5)] private string additionalPrompt =
            "Use punchy drums, a clear rhythmic pulse and strong transitions between gameplay phases.";
        [SerializeField] private bool instrumentalOnly = true;

        public int BeatsPerBar => Mathf.Max(1, beatsPerBar);
        public string Genre => genre;
        public string Mood => mood;
        public string MusicalKey => string.IsNullOrWhiteSpace(musicalKey) ? "E minor" : musicalKey;
        public float MaximumSegmentDurationSeconds => Mathf.Max(10f, maximumSegmentDurationSeconds);
        public string AdditionalPrompt => additionalPrompt;
        public bool InstrumentalOnly => instrumentalOnly;
    }

    /// <summary>High-level activity selected for one level phase. Add new activities here, then teach the run planner how to spawn them.</summary>
    public enum LevelPhaseAction
    {
        RhythmTiles,
        PunchObjects,
        SlashObjects,
        DodgeWalls,
        RandomMixed,
        OverheadClap,
        LegDrawUp
    }

    [Serializable]
    public sealed class NeonPulseLevelPhase
    {
        private const float DefaultSpawnIntervalSeconds = 1f;

        [SerializeField] private LevelPhaseAction action = LevelPhaseAction.PunchObjects;
        [SerializeField, Min(1f)] private float durationSeconds = 12f;
        [SerializeField, Min(1f)] private float flySpeed = 12f;
        [SerializeField, Min(0.1f)] private float spawnIntervalSeconds = DefaultSpawnIntervalSeconds;
        [SerializeField, Range(1, 2)] private int objectsPerWave = 1;
        [SerializeField, Min(1f)] private float holdDurationSeconds = 1.2f;
        [FormerlySerializedAs("musicClip")]
        [SerializeField, HideInInspector] private AudioClip legacyMusicClip;
        [SerializeField] private MusicBeatMap musicBeatMap = new MusicBeatMap();

        public string DisplayName => GetDisplayName(action);
        public LevelPhaseAction Action => action;
        public float DurationSeconds => durationSeconds;
        public float FlySpeed => flySpeed;
        public float SpawnIntervalSeconds => spawnIntervalSeconds > 0f
            ? spawnIntervalSeconds
            : DefaultSpawnIntervalSeconds;
        public int ObjectsPerWave => Mathf.Clamp(objectsPerWave, 1, 2);
        public float HoldDurationSeconds => Mathf.Max(1f, holdDurationSeconds);
        public MusicBeatMap MusicBeatMap => musicBeatMap;
        public AudioClip MusicClip => musicBeatMap != null && musicBeatMap.MusicClip != null
            ? musicBeatMap.MusicClip
            : legacyMusicClip;
        public bool HasMusicTrack => MusicClip != null;
        public bool HasAnalyzedMusic => musicBeatMap != null && musicBeatMap.HasCurrentAnalysis;

        public NeonPulseLevelPhase()
        {
        }

        public NeonPulseLevelPhase(
            LevelPhaseAction phaseAction,
            float duration,
            float speed,
            float spawnInterval = DefaultSpawnIntervalSeconds,
            int waveSize = 1)
        {
            action = phaseAction;
            durationSeconds = duration;
            flySpeed = speed;
            spawnIntervalSeconds = spawnInterval;
            objectsPerWave = waveSize;
        }

#if UNITY_EDITOR
        public void MigrateLegacyMusicClip()
        {
            if (legacyMusicClip == null)
            {
                return;
            }

            musicBeatMap = musicBeatMap ?? new MusicBeatMap();
            if (musicBeatMap.MusicClip == null)
            {
                musicBeatMap.SetMusicClip(legacyMusicClip);
            }

            legacyMusicClip = null;
        }
#endif

        public static string GetDisplayName(LevelPhaseAction value)
        {
            switch (value)
            {
                case LevelPhaseAction.RhythmTiles: return "Dậm chân theo gạch";
                case LevelPhaseAction.PunchObjects: return "Đấm vật thể";
                case LevelPhaseAction.SlashObjects: return "Chém vật thể";
                case LevelPhaseAction.DodgeWalls: return "Né tường";
                case LevelPhaseAction.RandomMixed: return "Tổng hợp ngẫu nhiên";
                case LevelPhaseAction.OverheadClap: return "Vỗ tay trên đầu";
                case LevelPhaseAction.LegDrawUp: return "Co một chân lên";
                default: return "Phase chưa xác định";
            }
        }
    }

    /// <summary>Authorable level asset. Runtime generates randomized targets from these phase settings on every run.</summary>
    [CreateAssetMenu(fileName = "NeonPulseLevel", menuName = "Neon Pulse/Level Definition")]
    public sealed class NeonPulseLevelDefinition : ScriptableObject
    {
        [SerializeField] private string levelName = "Level 01";
        [SerializeField, Range(0f, 5f)] private float phaseTransitionRestSeconds = 1.25f;
        [SerializeField] private MusicGenerationSettings musicGeneration = new MusicGenerationSettings();
        [SerializeField] private List<NeonPulseLevelPhase> phases = new List<NeonPulseLevelPhase>
        {
            new NeonPulseLevelPhase(LevelPhaseAction.RhythmTiles, 14f, 10f),
            new NeonPulseLevelPhase(LevelPhaseAction.PunchObjects, 14f, 12f),
            new NeonPulseLevelPhase(LevelPhaseAction.OverheadClap, 14f, 12f),
            new NeonPulseLevelPhase(LevelPhaseAction.SlashObjects, 14f, 14f),
            new NeonPulseLevelPhase(LevelPhaseAction.DodgeWalls, 12f, 16f)
        };

        public string LevelName => string.IsNullOrWhiteSpace(levelName) ? name : levelName;
        public float PhaseTransitionRestSeconds => phaseTransitionRestSeconds;
        public MusicGenerationSettings MusicGeneration => musicGeneration;
        public IReadOnlyList<NeonPulseLevelPhase> Phases => phases;
        public bool HasAuthoredPhaseMusic
        {
            get
            {
                if (phases == null)
                {
                    return false;
                }

                for (int index = 0; index < phases.Count; index++)
                {
                    if (phases[index] != null && phases[index].MusicClip != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool HasAnyAuthoredMusic => HasAuthoredPhaseMusic;

        public bool ValidateDefinition(out string message)
        {
            if (phases == null || phases.Count == 0)
            {
                message = "Level phải có ít nhất một phase.";
                return false;
            }

            bool hasRandomMixedPhase = false;
            bool hasConcreteAction = false;
            for (int index = 0; index < phases.Count; index++)
            {
                NeonPulseLevelPhase phase = phases[index];
                if (phase == null || phase.DurationSeconds <= 0f || phase.FlySpeed <= 0f ||
                    phase.SpawnIntervalSeconds <= 0f)
                {
                    message = "Phase " + (index + 1) +
                              " cần có thời lượng, tốc độ bay và khoảng spawn lớn hơn 0.";
                    return false;
                }

                if (phase.Action == LevelPhaseAction.RandomMixed)
                {
                    hasRandomMixedPhase = true;
                }
                else
                {
                    hasConcreteAction = true;
                }
            }

            if (hasRandomMixedPhase && !hasConcreteAction)
            {
                message = "Action Tổng hợp ngẫu nhiên cần ít nhất một phase action cụ thể khác trong Level.";
                return false;
            }

            for (int index = 0; index < phases.Count; index++)
            {
                NeonPulseLevelPhase phase = phases[index];
                if (phase == null || !phase.HasMusicTrack)
                {
                    message = "Phase " + (index + 1) + " chưa có file nhạc.";
                    return false;
                }

                if (!phase.HasAnalyzedMusic)
                {
                    message = "Nhạc của phase " + (index + 1) +
                              " chưa có beat map hoặc đã thay đổi. Hãy bấm PHÂN TÍCH BEAT.";
                    return false;
                }
            }

            message = "Level hợp lệ. Mỗi phase sẽ phát clip riêng và spawn theo beat map của clip đó.";
            return true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (phases == null)
            {
                return;
            }

            for (int index = 0; index < phases.Count; index++)
            {
                phases[index]?.MigrateLegacyMusicClip();
            }
        }
#endif
    }
}
