using System;
using UnityEngine;

namespace NeonPulse
{
    /// <summary>Shared judgement timings used by gameplay and its visual cues.</summary>
    public static class GameplayTiming
    {
        public const float HoldWindowLead = 0.45f;
        public const float HoldInputGrace = 0.18f;
        public const float HoldWindowTrail = 0.5f;
    }

    public enum AccuracyGrade
    {
        Perfect,
        Great,
        Good,
        Miss
    }

    public readonly struct ScoreSnapshot
    {
        public readonly int Score;
        public readonly int Combo;
        public readonly int MaxCombo;
        public readonly int Perfect;
        public readonly int Great;
        public readonly int Good;
        public readonly int Miss;

        public ScoreSnapshot(int score, int combo, int maxCombo, int perfect, int great, int good, int miss)
        {
            Score = score;
            Combo = combo;
            MaxCombo = maxCombo;
            Perfect = perfect;
            Great = great;
            Good = good;
            Miss = miss;
        }
    }

    /// <summary>Owns accuracy grading, score and combo with no dependency on presentation.</summary>
    public sealed class RhythmScore
    {
        public const float PerfectWindow = 0.08f;
        public const float GreatWindow = 0.16f;
        public const float GoodWindow = 0.28f;

        private int score;
        private int combo;
        private int maxCombo;
        private int perfect;
        private int great;
        private int good;
        private int miss;

        public event Action<ScoreSnapshot> Changed;
        public event Action<AccuracyGrade, GameplayAction> Judged;

        public ScoreSnapshot Snapshot => new ScoreSnapshot(score, combo, maxCombo, perfect, great, good, miss);

        /// <summary>Grades a valid action based on its absolute timing error.</summary>
        public AccuracyGrade RegisterHit(float absoluteError, GameplayAction action)
        {
            AccuracyGrade grade;
            if (absoluteError <= PerfectWindow)
            {
                grade = AccuracyGrade.Perfect;
                score += 1000 + combo * 10;
                perfect++;
            }
            else if (absoluteError <= GreatWindow)
            {
                grade = AccuracyGrade.Great;
                score += 750 + combo * 7;
                great++;
            }
            else
            {
                grade = AccuracyGrade.Good;
                score += 500 + combo * 5;
                good++;
            }

            combo++;
            if (combo > maxCombo)
            {
                maxCombo = combo;
            }

            Notify(grade, action);
            return grade;
        }

        /// <summary>Registers a missed chart event and breaks the combo.</summary>
        public void RegisterMiss(GameplayAction action)
        {
            combo = 0;
            miss++;
            Notify(AccuracyGrade.Miss, action);
        }

        /// <summary>Clears all run statistics for a restart.</summary>
        public void Reset()
        {
            score = 0;
            combo = 0;
            maxCombo = 0;
            perfect = 0;
            great = 0;
            good = 0;
            miss = 0;
            Changed?.Invoke(Snapshot);
        }

        private void Notify(AccuracyGrade grade, GameplayAction action)
        {
            Judged?.Invoke(grade, action);
            Changed?.Invoke(Snapshot);
        }
    }
}
