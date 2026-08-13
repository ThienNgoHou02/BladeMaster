using UnityEngine;

namespace NeonPulse
{
    /// <summary>Shared beat calculations used by runtime scheduling and phase music export.</summary>
    public static class NeonPulsePhaseBeatTiming
    {
        private const float TimingEpsilon = 0.0001f;

        public static float GetTravelDurationSeconds(NeonPulseLevelPhase phase, RhythmSettings rhythm)
        {
            float travelDistance = Mathf.Max(0.1f, rhythm.SpawnZ - rhythm.HitZ);
            return travelDistance / Mathf.Max(0.01f, phase.FlySpeed);
        }

        /// <summary>Returns the first whole local beat that leaves enough time for the object to travel.</summary>
        public static int GetFirstActionBeat(NeonPulseLevelPhase phase, RhythmSettings rhythm)
        {
            float travelBeats = GetTravelDurationSeconds(phase, rhythm) / rhythm.SecondsPerBeat;
            return Mathf.Max(1, Mathf.CeilToInt(travelBeats - TimingEpsilon));
        }

        /// <summary>
        /// Converts the authored interval to whole beats and preserves the no-overlap rule for hold actions.
        /// </summary>
        public static int GetActionIntervalBeats(
            NeonPulseLevelPhase phase,
            LevelPhaseAction resolvedAction,
            RhythmSettings rhythm)
        {
            int nearestAuthoredBeat = Mathf.Max(
                1,
                Mathf.RoundToInt(phase.SpawnIntervalSeconds / rhythm.SecondsPerBeat));
            if (resolvedAction == LevelPhaseAction.DodgeWalls || resolvedAction == LevelPhaseAction.LegDrawUp)
            {
                float holdDuration = resolvedAction == LevelPhaseAction.LegDrawUp
                    ? phase.HoldDurationSeconds
                    : rhythm.HoldWindowTrail;
                float minimumSafeSpacingSeconds = rhythm.HoldWindowLead + holdDuration + 0.12f;
                int minimumSafeBeat = Mathf.Max(
                    1,
                    Mathf.CeilToInt(minimumSafeSpacingSeconds / rhythm.SecondsPerBeat - TimingEpsilon));
                return Mathf.Max(nearestAuthoredBeat, minimumSafeBeat);
            }

            return nearestAuthoredBeat;
        }
    }
}
