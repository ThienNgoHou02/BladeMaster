using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeonPulse
{
    /// <summary>High-level activity selected for one level phase. Add new activities here, then teach the run planner how to spawn them.</summary>
    public enum LevelPhaseAction
    {
        RhythmTiles,
        PunchObjects,
        SlashObjects,
        DodgeWalls
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

        public string DisplayName => GetDisplayName(action);
        public LevelPhaseAction Action => action;
        public float DurationSeconds => durationSeconds;
        public float FlySpeed => flySpeed;
        public float SpawnIntervalSeconds => spawnIntervalSeconds > 0f
            ? spawnIntervalSeconds
            : DefaultSpawnIntervalSeconds;
        public int ObjectsPerWave => Mathf.Clamp(objectsPerWave, 1, 2);

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

        public static string GetDisplayName(LevelPhaseAction value)
        {
            switch (value)
            {
                case LevelPhaseAction.RhythmTiles: return "Dậm chân theo gạch";
                case LevelPhaseAction.PunchObjects: return "Đấm vật thể";
                case LevelPhaseAction.SlashObjects: return "Chém vật thể";
                case LevelPhaseAction.DodgeWalls: return "Né tường";
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
        [SerializeField] private List<NeonPulseLevelPhase> phases = new List<NeonPulseLevelPhase>
        {
            new NeonPulseLevelPhase(LevelPhaseAction.RhythmTiles, 14f, 10f),
            new NeonPulseLevelPhase(LevelPhaseAction.PunchObjects, 14f, 12f),
            new NeonPulseLevelPhase(LevelPhaseAction.SlashObjects, 14f, 14f),
            new NeonPulseLevelPhase(LevelPhaseAction.DodgeWalls, 12f, 16f)
        };

        public string LevelName => string.IsNullOrWhiteSpace(levelName) ? name : levelName;
        public float PhaseTransitionRestSeconds => phaseTransitionRestSeconds;
        public IReadOnlyList<NeonPulseLevelPhase> Phases => phases;

        public bool ValidateDefinition(out string message)
        {
            if (phases == null || phases.Count == 0)
            {
                message = "Level phải có ít nhất một phase.";
                return false;
            }

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
            }

            message = "Level hợp lệ. Object sẽ được random lại ở mỗi lượt chơi.";
            return true;
        }
    }
}
