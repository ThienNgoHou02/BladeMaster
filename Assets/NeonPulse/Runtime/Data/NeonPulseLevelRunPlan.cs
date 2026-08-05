using System.Collections.Generic;
using UnityEngine;

namespace NeonPulse
{
    /// <summary>One precomputed pooled traveller spawn. It is built once at run start and never allocates in Update.</summary>
    public struct PlannedGameplayEvent
    {
        public BeatmapEvent Event;
        public float TargetTime;
        public float SpawnTime;
        public bool IsObstacle;
        public bool UseSlashVisual;
    }

    public struct PlannedRhythmTileEvent
    {
        public RhythmTileEvent Event;
        public float TargetTime;
        public float SpawnTime;
    }

    /// <summary>Converts a level asset into absolute-time spawn events while preserving the existing object pools.</summary>
    public sealed class NeonPulseLevelRunPlan
    {
        private const float MinimumEventSpacing = 0.78f;
        private const float MaximumEventSpacing = 1.25f;

        private readonly List<PlannedGameplayEvent> targetEvents = new List<PlannedGameplayEvent>(48);
        private readonly List<PlannedGameplayEvent> obstacleEvents = new List<PlannedGameplayEvent>(24);
        private readonly List<PlannedRhythmTileEvent> rhythmTileEvents = new List<PlannedRhythmTileEvent>(48);
        private readonly List<float> phaseStartTimes = new List<float>(8);
        private readonly List<float> phaseEndTimes = new List<float>(8);
        private readonly List<NeonPulseLevelPhase> phases = new List<NeonPulseLevelPhase>(8);

        public IReadOnlyList<PlannedGameplayEvent> TargetEvents => targetEvents;
        public IReadOnlyList<PlannedGameplayEvent> ObstacleEvents => obstacleEvents;
        public IReadOnlyList<PlannedRhythmTileEvent> RhythmTileEvents => rhythmTileEvents;
        public float Duration { get; private set; }
        public int PhaseCount => phases.Count;

        public static NeonPulseLevelRunPlan Build(NeonPulseLevelDefinition level, NeonPulseGameConfig config, ref uint randomState)
        {
            NeonPulseLevelRunPlan plan = new NeonPulseLevelRunPlan();
            if (level == null || level.Phases.Count == 0)
            {
                plan.BuildLegacy(config);
                return plan;
            }

            float cursor = 0f;
            for (int index = 0; index < level.Phases.Count; index++)
            {
                NeonPulseLevelPhase phase = level.Phases[index];
                if (phase == null)
                {
                    continue;
                }

                plan.phases.Add(phase);
                plan.phaseStartTimes.Add(cursor);
                float endTime = cursor + phase.DurationSeconds;
                plan.phaseEndTimes.Add(endTime);
                plan.AddRandomPhaseEvents(phase, cursor, endTime, config, ref randomState);
                cursor = endTime + (index < level.Phases.Count - 1 ? level.PhaseTransitionRestSeconds : 0f);
            }

            plan.Duration = cursor;
            return plan;
        }

        public int GetPhaseIndex(float time)
        {
            for (int index = 0; index < phaseEndTimes.Count; index++)
            {
                if (time <= phaseEndTimes[index])
                {
                    return index;
                }
            }

            return phaseEndTimes.Count - 1;
        }

        public bool TryGetPhase(float time, out NeonPulseLevelPhase phase, out float normalizedProgress)
        {
            int index = GetPhaseIndex(time);
            if (index < 0 || index >= phases.Count)
            {
                phase = null;
                normalizedProgress = 0f;
                return false;
            }

            phase = phases[index];
            float duration = Mathf.Max(0.01f, phaseEndTimes[index] - phaseStartTimes[index]);
            normalizedProgress = Mathf.Clamp01((time - phaseStartTimes[index]) / duration);
            return time >= phaseStartTimes[index] && time <= phaseEndTimes[index];
        }

        private void AddRandomPhaseEvents(
            NeonPulseLevelPhase phase,
            float phaseStartTime,
            float phaseEndTime,
            NeonPulseGameConfig config,
            ref uint randomState)
        {
            float distance = Mathf.Max(0.1f, config.Rhythm.SpawnZ - config.Rhythm.HitZ);
            float travelDuration = distance / phase.FlySpeed;
            float spacing = Mathf.Clamp(travelDuration * 0.52f, MinimumEventSpacing, MaximumEventSpacing);
            float targetTime = phaseStartTime + travelDuration;

            while (targetTime <= phaseEndTime)
            {
                float spawnTime = targetTime - travelDuration;
                switch (phase.Action)
                {
                    case LevelPhaseAction.RhythmTiles:
                        AddRhythmTilePair(targetTime, spawnTime, ref randomState);
                        break;
                    case LevelPhaseAction.PunchObjects:
                        AddTarget(targetTime, spawnTime, false, ref randomState);
                        break;
                    case LevelPhaseAction.SlashObjects:
                        AddTarget(targetTime, spawnTime, true, ref randomState);
                        break;
                    case LevelPhaseAction.DodgeWalls:
                        AddDodgeWall(targetTime, spawnTime, ref randomState);
                        break;
                }

                targetTime += spacing;
            }
        }

        private void AddTarget(float targetTime, float spawnTime, bool useSlashVisual, ref uint randomState)
        {
            GameplayAction action = (GameplayAction)NextRandomInt(ref randomState, (int)GameplayAction.LeftPunch, (int)GameplayAction.BothPunch + 1);
            int lane = action == GameplayAction.LeftPunch ? NextRandomInt(ref randomState, 0, 2) :
                action == GameplayAction.RightPunch ? NextRandomInt(ref randomState, 2, 4) : NextRandomInt(ref randomState, 1, 3);
            targetEvents.Add(new PlannedGameplayEvent
            {
                Event = new BeatmapEvent(0f, lane, action),
                TargetTime = targetTime,
                SpawnTime = spawnTime,
                UseSlashVisual = useSlashVisual
            });
        }

        private void AddDodgeWall(float targetTime, float spawnTime, ref uint randomState)
        {
            GameplayAction action = NextRandomInt(ref randomState, 0, 2) == 0 ? GameplayAction.DodgeLeft : GameplayAction.DodgeRight;
            obstacleEvents.Add(new PlannedGameplayEvent
            {
                Event = new BeatmapEvent(0f, 0, action),
                TargetTime = targetTime,
                SpawnTime = spawnTime,
                IsObstacle = true
            });
        }

        private void AddRhythmTilePair(float targetTime, float spawnTime, ref uint randomState)
        {
            int leftLane = NextRandomInt(ref randomState, 0, 2);
            int rightLane = NextRandomInt(ref randomState, 2, 4);
            rhythmTileEvents.Add(new PlannedRhythmTileEvent
            {
                Event = new RhythmTileEvent(0f, leftLane, RandomTileColor(ref randomState)),
                TargetTime = targetTime,
                SpawnTime = spawnTime
            });
            rhythmTileEvents.Add(new PlannedRhythmTileEvent
            {
                Event = new RhythmTileEvent(0f, rightLane, RandomTileColor(ref randomState)),
                TargetTime = targetTime,
                SpawnTime = spawnTime
            });
        }

        private void BuildLegacy(NeonPulseGameConfig config)
        {
            float secondsPerBeat = config.Rhythm.SecondsPerBeat;
            float travelDuration = config.Rhythm.TravelBeats * secondsPerBeat;
            for (int index = 0; index < config.PunchEvents.Count; index++)
            {
                BeatmapEvent chartEvent = config.PunchEvents[index];
                float targetTime = chartEvent.Beat * secondsPerBeat;
                targetEvents.Add(new PlannedGameplayEvent
                {
                    Event = chartEvent,
                    TargetTime = targetTime,
                    SpawnTime = targetTime - travelDuration,
                    UseSlashVisual = config.GameplayMode == CombatGameplayMode.Slash
                });
            }

            for (int index = 0; index < config.ObstacleEvents.Count; index++)
            {
                BeatmapEvent chartEvent = config.ObstacleEvents[index];
                float targetTime = chartEvent.Beat * secondsPerBeat;
                obstacleEvents.Add(new PlannedGameplayEvent
                {
                    Event = chartEvent,
                    TargetTime = targetTime,
                    SpawnTime = targetTime - travelDuration,
                    IsObstacle = true
                });
            }

            for (int index = 0; index < config.RhythmTileEvents.Count; index++)
            {
                RhythmTileEvent chartEvent = config.RhythmTileEvents[index];
                float targetTime = chartEvent.Beat * secondsPerBeat;
                rhythmTileEvents.Add(new PlannedRhythmTileEvent
                {
                    Event = chartEvent,
                    TargetTime = targetTime,
                    SpawnTime = targetTime - travelDuration
                });
            }

            Duration = config.Rhythm.SongBeats * secondsPerBeat;
        }

        private static RhythmTileColor RandomTileColor(ref uint state)
        {
            return (RhythmTileColor)NextRandomInt(ref state, (int)RhythmTileColor.Cyan, (int)RhythmTileColor.Purple + 1);
        }

        private static int NextRandomInt(ref uint state, int minimumInclusive, int maximumExclusive)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            uint range = (uint)Mathf.Max(1, maximumExclusive - minimumInclusive);
            return minimumInclusive + (int)(state % range);
        }
    }
}
