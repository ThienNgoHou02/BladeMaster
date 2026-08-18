using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace NeonPulse
{
    [Serializable]
    public struct DetectedMusicBeat
    {
        [Min(0f)] public float Time;
        [Range(0f, 1f)] public float Strength;

        public DetectedMusicBeat(float time, float strength)
        {
            Time = time;
            Strength = strength;
        }
    }

    [Serializable]
    public sealed class BeatAnalysisSettings
    {
        [SerializeField, Range(70f, 160f)] private float minimumBpm = 85f;
        [SerializeField, Range(90f, 220f)] private float maximumBpm = 180f;
        [SerializeField, Range(-0.25f, 0.25f)] private float timingOffsetSeconds;

        public float MinimumBpm => Mathf.Min(minimumBpm, maximumBpm - 1f);
        public float MaximumBpm => Mathf.Max(maximumBpm, minimumBpm + 1f);
        public float TimingOffsetSeconds => timingOffsetSeconds;
    }

    /// <summary>
    /// Beat timestamps are baked in the Editor. Runtime only reads this data and never analyzes audio.
    /// </summary>
    [Serializable]
    public sealed class MusicBeatMap
    {
        [SerializeField] private AudioClip musicClip;
        [FormerlySerializedAs("fitPhasesToMusic")]
        [SerializeField] private bool useMusicLengthAsPhaseDuration = true;
        [SerializeField] private BeatAnalysisSettings analysisSettings = new BeatAnalysisSettings();
        [SerializeField, HideInInspector] private List<DetectedMusicBeat> detectedBeats =
            new List<DetectedMusicBeat>(256);
        [SerializeField, HideInInspector] private float estimatedBpm;
        [SerializeField, HideInInspector] private AudioClip analyzedClip;
        [SerializeField, HideInInspector] private int analyzedSampleCount;
        [SerializeField, HideInInspector] private int analyzedFrequency;
        [SerializeField, HideInInspector] private int analyzedChannels;

        public AudioClip MusicClip => musicClip;
        public bool UseMusicLengthAsPhaseDuration => useMusicLengthAsPhaseDuration;
        public BeatAnalysisSettings AnalysisSettings => analysisSettings;
        public IReadOnlyList<DetectedMusicBeat> DetectedBeats => detectedBeats;
        public float EstimatedBpm => estimatedBpm;
        public bool HasCurrentAnalysis => musicClip != null && analyzedClip == musicClip &&
                                          detectedBeats != null && detectedBeats.Count > 0 &&
                                          analyzedSampleCount == musicClip.samples &&
                                          analyzedFrequency == musicClip.frequency &&
                                          analyzedChannels == musicClip.channels;

#if UNITY_EDITOR
        public void SetMusicClip(AudioClip clip)
        {
            if (musicClip == clip)
            {
                return;
            }

            musicClip = clip;
            ClearAnalysis();
        }

        public void SetAnalysis(IReadOnlyList<DetectedMusicBeat> beats, float bpm)
        {
            detectedBeats = detectedBeats ?? new List<DetectedMusicBeat>(Mathf.Max(32, beats.Count));
            detectedBeats.Clear();
            for (int index = 0; index < beats.Count; index++)
            {
                detectedBeats.Add(beats[index]);
            }

            estimatedBpm = Mathf.Max(0f, bpm);
            analyzedClip = musicClip;
            analyzedSampleCount = musicClip != null ? musicClip.samples : 0;
            analyzedFrequency = musicClip != null ? musicClip.frequency : 0;
            analyzedChannels = musicClip != null ? musicClip.channels : 0;
        }

        public void ClearAnalysis()
        {
            detectedBeats?.Clear();
            estimatedBpm = 0f;
            analyzedClip = null;
            analyzedSampleCount = 0;
            analyzedFrequency = 0;
            analyzedChannels = 0;
        }
#endif
    }
}
