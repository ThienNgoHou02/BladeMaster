using System;
using UnityEngine;

namespace NeonPulse
{
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
        private readonly RhythmSettings timing;
        private readonly ScoreSettings scoreSettings;
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
        public float GoodWindow => timing.GoodWindow;

        /// <summary>Creates a score service from editable timing and point configuration.</summary>
        public RhythmScore(RhythmSettings rhythmSettings, ScoreSettings configuredScore)
        {
            timing = rhythmSettings ?? new RhythmSettings();
            scoreSettings = configuredScore ?? new ScoreSettings();
        }

        /// <summary>Grades a valid action based on its absolute timing error.</summary>
        public AccuracyGrade RegisterHit(float absoluteError, GameplayAction action)
        {
            AccuracyGrade grade;
            if (absoluteError <= timing.PerfectWindow)
            {
                grade = AccuracyGrade.Perfect;
                score += scoreSettings.PerfectPoints + combo * scoreSettings.PerfectComboBonus;
                perfect++;
            }
            else if (absoluteError <= timing.GreatWindow)
            {
                grade = AccuracyGrade.Great;
                score += scoreSettings.GreatPoints + combo * scoreSettings.GreatComboBonus;
                great++;
            }
            else
            {
                grade = AccuracyGrade.Good;
                score += scoreSettings.GoodPoints + combo * scoreSettings.GoodComboBonus;
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
