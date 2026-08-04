using UnityEngine;

namespace NeonPulse
{
    /// <summary>Generates a simple original metronomic workout beat when no music asset is present.</summary>
    public static class RhythmAudioSynth
    {
        private const int SampleRate = 44100;

        /// <summary>Creates the full PCM clip once; no audio-thread callback or per-frame allocation is used.</summary>
        public static AudioClip Create(float bpm, int songBeats)
        {
            float secondsPerBeat = 60f / Mathf.Max(1f, bpm);
            float duration = (songBeats + 2) * secondsPerBeat;
            int sampleCount = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[sampleCount];
            int samplesPerBeat = Mathf.Max(1, Mathf.RoundToInt(secondsPerBeat * SampleRate));
            int samplesPerHalfBeat = Mathf.Max(1, samplesPerBeat / 2);
            uint noiseState = 0x9E3779B9u;

            for (int sample = 0; sample < sampleCount; sample++)
            {
                int beatSample = sample % samplesPerBeat;
                float beatPhase = beatSample / (float)SampleRate;
                float kick = 0f;
                if (beatPhase < 0.16f)
                {
                    float envelope = Mathf.Exp(-beatPhase * 22f);
                    float frequency = Mathf.Lerp(115f, 46f, beatPhase / 0.16f);
                    kick = Mathf.Sin(2f * Mathf.PI * frequency * beatPhase) * envelope * 0.72f;
                }

                int halfBeatSample = sample % samplesPerHalfBeat;
                float hatTime = halfBeatSample / (float)SampleRate;
                float hat = 0f;
                if (hatTime < 0.035f)
                {
                    noiseState = noiseState * 1664525u + 1013904223u;
                    float noise = ((noiseState >> 9) / 8388607f) * 2f - 1f;
                    hat = noise * Mathf.Exp(-hatTime * 95f) * 0.16f;
                }

                int beatIndex = sample / samplesPerBeat;
                float accent = beatIndex % 4 == 0 ? 1f : 0.72f;
                samples[sample] = Mathf.Clamp((kick + hat) * accent, -0.92f, 0.92f);
            }

            AudioClip clip = AudioClip.Create("Neon Pulse Procedural Beat", sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
