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
        public float HoldDuration;
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
        private readonly List<PlannedGameplayEvent> targetEvents = new List<PlannedGameplayEvent>(48);
        private readonly List<PlannedGameplayEvent> obstacleEvents = new List<PlannedGameplayEvent>(24);
        private readonly List<PlannedRhythmTileEvent> rhythmTileEvents = new List<PlannedRhythmTileEvent>(48);
        private readonly List<float> phaseStartTimes = new List<float>(8);
        private readonly List<float> phaseEndTimes = new List<float>(8);
        private readonly List<NeonPulseLevelPhase> phases = new List<NeonPulseLevelPhase>(8);
        private readonly List<LevelPhaseAction> randomMixedActions = new List<LevelPhaseAction>(5);
        private float legacyFlySpeed;

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

            plan.CollectRandomMixedActions(level);
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

        /// <summary>Returns the authored object speed for the active phase, or zero during rests.</summary>
        public float GetFlySpeed(float time)
        {
            if (phases.Count == 0)
            {
                return time >= 0f && time <= Duration ? legacyFlySpeed : 0f;
            }

            return TryGetPhase(time, out NeonPulseLevelPhase phase, out _)
                ? phase.FlySpeed
                : 0f;
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
            float targetTime = phaseStartTime + travelDuration;

            while (targetTime <= phaseEndTime)
            {
                float spawnTime = targetTime - travelDuration;
                LevelPhaseAction action = ResolveSpawnAction(phase.Action, ref randomState);
                if (action == LevelPhaseAction.LegDrawUp && targetTime + phase.HoldDurationSeconds > phaseEndTime)
                {
                    targetTime += phase.SpawnIntervalSeconds;
                    continue;
                }

                switch (action)
                {
                    case LevelPhaseAction.RhythmTiles:
                        AddRhythmTilePair(targetTime, spawnTime, ref randomState);
                        break;
                    case LevelPhaseAction.PunchObjects:
                        AddTargetWave(phase.ObjectsPerWave, targetTime, spawnTime, false, ref randomState);
                        break;
                    case LevelPhaseAction.SlashObjects:
                        AddTargetWave(phase.ObjectsPerWave, targetTime, spawnTime, true, ref randomState);
                        break;
                    case LevelPhaseAction.DodgeWalls:
                        AddDodgeWall(targetTime, spawnTime, ref randomState);
                        break;
                    case LevelPhaseAction.OverheadClap:
                        AddOverheadClapTarget(targetTime, spawnTime, ref randomState);
                        break;
                    case LevelPhaseAction.LegDrawUp:
                        AddLegDrawUpTile(targetTime, spawnTime, phase.HoldDurationSeconds, ref randomState);
                        break;
                }

                float spacing = phase.SpawnIntervalSeconds;
                if (action == LevelPhaseAction.DodgeWalls || action == LevelPhaseAction.LegDrawUp)
                {
                    // Do not overlap hold windows: changing direction while a wall is still active
                    // made the dodge phase feel jerky and could generate impossible inputs.
                    float holdDuration = action == LevelPhaseAction.LegDrawUp
                        ? phase.HoldDurationSeconds
                        : config.Rhythm.HoldWindowTrail;
                    spacing = Mathf.Max(spacing, config.Rhythm.HoldWindowLead + holdDuration + 0.12f);
                }

                targetTime += spacing;
            }
        }

        private void CollectRandomMixedActions(NeonPulseLevelDefinition level)
        {
            for (int index = 0; index < level.Phases.Count; index++)
            {
                NeonPulseLevelPhase phase = level.Phases[index];
                if (phase == null || phase.Action == LevelPhaseAction.RandomMixed ||
                    randomMixedActions.Contains(phase.Action))
                {
                    continue;
                }

                randomMixedActions.Add(phase.Action);
            }
        }

        private LevelPhaseAction ResolveSpawnAction(LevelPhaseAction authoredAction, ref uint randomState)
        {
            if (authoredAction != LevelPhaseAction.RandomMixed || randomMixedActions.Count == 0)
            {
                return authoredAction;
            }

            int randomIndex = NextRandomInt(ref randomState, 0, randomMixedActions.Count);
            return randomMixedActions[randomIndex];
        }

        private void AddTargetWave(
            int objectsPerWave,
            float targetTime,
            float spawnTime,
            bool useSlashVisual,
            ref uint randomState)
        {
            if (objectsPerWave <= 1)
            {
                AddRandomTarget(targetTime, spawnTime, useSlashVisual, ref randomState);
                return;
            }

            // A two-object wave is always a complementary pair so the player can hit
            // both targets at the same time without receiving an impossible pattern.
            AddTarget(targetTime, spawnTime, useSlashVisual, GameplayAction.LeftPunch,
                NextRandomInt(ref randomState, 0, 2));
            AddTarget(targetTime, spawnTime, useSlashVisual, GameplayAction.RightPunch,
                NextRandomInt(ref randomState, 2, 4));
        }

        private void AddRandomTarget(float targetTime, float spawnTime, bool useSlashVisual, ref uint randomState)
        {
            GameplayAction action = (GameplayAction)NextRandomInt(ref randomState, (int)GameplayAction.LeftPunch, (int)GameplayAction.BothPunch + 1);
            int lane = action == GameplayAction.LeftPunch ? NextRandomInt(ref randomState, 0, 2) :
                action == GameplayAction.RightPunch ? NextRandomInt(ref randomState, 2, 4) : NextRandomInt(ref randomState, 1, 3);
            AddTarget(targetTime, spawnTime, useSlashVisual, action, lane);
        }

        private void AddTarget(
            float targetTime,
            float spawnTime,
            bool useSlashVisual,
            GameplayAction action,
            int lane)
        {
            targetEvents.Add(new PlannedGameplayEvent
            {
                Event = new BeatmapEvent(0f, lane, action),
                TargetTime = targetTime,
                SpawnTime = spawnTime,
                UseSlashVisual = useSlashVisual
            });
        }

        private void AddOverheadClapTarget(float targetTime, float spawnTime, ref uint randomState)
        {
            // Hai lane trong giữ target vừa tầm hai tay nhưng vẫn đổi bên rõ ràng.
            int lane = NextRandomInt(ref randomState, 0, 2) == 0 ? 1 : 2;
            AddTarget(targetTime, spawnTime, false, GameplayAction.OverheadClap, lane);
        }

        private void AddDodgeWall(float targetTime, float spawnTime, ref uint randomState)
        {
            GameplayAction action;
            switch (NextRandomInt(ref randomState, 0, 4))
            {
                case 0: action = GameplayAction.DodgeLeft; break;
                case 1: action = GameplayAction.DodgeRight; break;
                case 2: action = GameplayAction.Duck; break;
                default: action = GameplayAction.Jump; break;
            }
            obstacleEvents.Add(new PlannedGameplayEvent
            {
                Event = new BeatmapEvent(0f, 0, action),
                TargetTime = targetTime,
                SpawnTime = spawnTime,
                IsObstacle = true
            });
        }

        private void AddLegDrawUpTile(float targetTime, float spawnTime, float maximumHoldDuration, ref uint randomState)
        {
            bool useLeftLeg = NextRandomInt(ref randomState, 0, 2) == 0;
            float holdDuration = NextRandomFloat(ref randomState, 1f, Mathf.Max(1f, maximumHoldDuration));
            obstacleEvents.Add(new PlannedGameplayEvent
            {
                Event = new BeatmapEvent(
                    0f,
                    useLeftLeg ? 0 : 3,
                    useLeftLeg ? GameplayAction.LeftLegDrawUp : GameplayAction.RightLegDrawUp),
                TargetTime = targetTime,
                SpawnTime = spawnTime,
                IsObstacle = true,
                HoldDuration = holdDuration
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
            float travelDistance = Mathf.Max(0.1f, config.Rhythm.SpawnZ - config.Rhythm.HitZ);
            legacyFlySpeed = travelDistance / Mathf.Max(0.01f, travelDuration);
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

        private static float NextRandomFloat(ref uint state, float minimumInclusive, float maximumInclusive)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            const float InverseMaximum24BitValue = 1f / 16777215f;
            float normalized = (state & 0x00FFFFFFu) * InverseMaximum24BitValue;
            return Mathf.Lerp(minimumInclusive, maximumInclusive, normalized);
        }
    }
}
