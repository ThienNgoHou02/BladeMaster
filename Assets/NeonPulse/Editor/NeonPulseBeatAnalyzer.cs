using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NeonPulse.EditorTools
{
    /// <summary>Offline beat-grid analyzer. Audio decoding and allocations only happen in the Editor.</summary>
    public static class NeonPulseBeatAnalyzer
    {
        private const int AnalysisWindowSize = 1024;
        private const int AnalysisHopSize = 512;

        private struct AnalysisResult
        {
            public readonly List<DetectedMusicBeat> Beats;
            public readonly float Bpm;

            public AnalysisResult(List<DetectedMusicBeat> beats, float bpm)
            {
                Beats = beats;
                Bpm = bpm;
            }
        }

        public static void AnalyzeAndApply(
            NeonPulseLevelDefinition level,
            MusicBeatMap beatMap,
            int phaseIndex)
        {
            if (level == null || beatMap == null || beatMap.MusicClip == null)
            {
                EditorUtility.DisplayDialog("Thiếu file nhạc", "Hãy kéo AudioClip vào phase trước.", "OK");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Phân tích beat", "Đọc dữ liệu âm thanh...", 0.05f);
                AudioClip clip = beatMap.MusicClip;
                float[] samples = ReadDecodedSamples(clip, out int sampleFrames, out int channels, out int frequency);

                EditorUtility.DisplayProgressBar("Phân tích beat", "Tìm tempo và lưới beat...", 0.45f);
                AnalysisResult result = Analyze(
                    samples,
                    sampleFrames,
                    channels,
                    frequency,
                    beatMap.AnalysisSettings);
                if (result.Beats.Count < 2)
                {
                    throw new InvalidOperationException("Không tìm thấy đủ beat. Hãy dùng file nhạc có nhịp trống rõ hơn.");
                }

                Undo.RecordObject(level, "Analyze Phase Music Beats");
                beatMap.SetAnalysis(result.Beats, result.Bpm);
                EditorUtility.SetDirty(level);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog(
                    "Phân tích hoàn tất",
                    "Phase " + (phaseIndex + 1) + ": đã bake " + result.Beats.Count +
                    " beat — BPM ước tính: " + result.Bpm.ToString("0.0") + ".",
                    "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Không thể phân tích beat", exception.Message, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static void ClearAnalysis(NeonPulseLevelDefinition level, MusicBeatMap beatMap)
        {
            if (level == null || beatMap == null)
            {
                return;
            }

            Undo.RecordObject(level, "Clear Music Beat Analysis");
            beatMap.ClearAnalysis();
            EditorUtility.SetDirty(level);
        }

        private static AnalysisResult Analyze(
            float[] interleavedSamples,
            int sampleFrames,
            int channels,
            int frequency,
            BeatAnalysisSettings settings)
        {
            int frameCount = Mathf.Max(0, (sampleFrames - AnalysisWindowSize) / AnalysisHopSize);
            if (frameCount < 8)
            {
                throw new InvalidOperationException("AudioClip quá ngắn để phân tích beat.");
            }

            float[] novelty = new float[frameCount];
            float baseline = 0f;
            float previousMono = 0f;
            float maximumNovelty = 0f;
            for (int frame = 0; frame < frameCount; frame++)
            {
                int firstSampleFrame = frame * AnalysisHopSize;
                float energy = 0f;
                float highFrequencyEnergy = 0f;
                for (int sampleOffset = 0; sampleOffset < AnalysisWindowSize; sampleOffset++)
                {
                    int interleavedIndex = (firstSampleFrame + sampleOffset) * channels;
                    float mono = 0f;
                    for (int channel = 0; channel < channels; channel++)
                    {
                        mono += interleavedSamples[interleavedIndex + channel];
                    }

                    mono /= channels;
                    energy += mono * mono;
                    highFrequencyEnergy += Mathf.Abs(mono - previousMono);
                    previousMono = mono;
                }

                float rms = Mathf.Sqrt(energy / AnalysisWindowSize);
                float transientEnergy = highFrequencyEnergy / AnalysisWindowSize;
                float envelope = rms * 0.72f + transientEnergy * 0.28f;
                if (frame == 0)
                {
                    baseline = envelope;
                }

                float onset = Mathf.Max(0f, envelope - baseline * 0.9f);
                baseline = Mathf.Lerp(baseline, envelope, envelope > baseline ? 0.08f : 0.025f);
                novelty[frame] = onset;
                maximumNovelty = Mathf.Max(maximumNovelty, onset);
            }

            if (maximumNovelty <= 0.000001f)
            {
                throw new InvalidOperationException("AudioClip không có transient đủ rõ để nhận diện beat.");
            }

            float analysisFramesPerSecond = frequency / (float)AnalysisHopSize;
            int minimumLag = Mathf.Max(2, Mathf.FloorToInt(60f * analysisFramesPerSecond / settings.MaximumBpm));
            int maximumLag = Mathf.Max(minimumLag + 1,
                Mathf.CeilToInt(60f * analysisFramesPerSecond / settings.MinimumBpm));
            maximumLag = Mathf.Min(maximumLag, frameCount / 3);

            int bestLag = minimumLag;
            float bestCorrelation = float.MinValue;
            for (int lag = minimumLag; lag <= maximumLag; lag++)
            {
                float correlation = 0f;
                float leftEnergy = 0f;
                float rightEnergy = 0f;
                for (int frame = lag; frame < frameCount; frame++)
                {
                    float left = novelty[frame];
                    float right = novelty[frame - lag];
                    correlation += left * right;
                    leftEnergy += left * left;
                    rightEnergy += right * right;
                }

                correlation /= Mathf.Sqrt(leftEnergy * rightEnergy) + 0.000001f;
                if (correlation > bestCorrelation)
                {
                    bestCorrelation = correlation;
                    bestLag = lag;
                }
            }

            int bestPhase = 0;
            float bestPhaseScore = float.MinValue;
            for (int phase = 0; phase < bestLag; phase++)
            {
                float score = 0f;
                for (int frame = phase; frame < frameCount; frame += bestLag)
                {
                    score += novelty[frame];
                }

                if (score > bestPhaseScore)
                {
                    bestPhaseScore = score;
                    bestPhase = phase;
                }
            }

            int snapRadius = Mathf.Max(1, Mathf.RoundToInt(bestLag * 0.12f));
            int estimatedBeatCount = Mathf.CeilToInt(frameCount / (float)bestLag) + 1;
            List<DetectedMusicBeat> beats = new List<DetectedMusicBeat>(estimatedBeatCount);
            int lastSnappedFrame = -1;
            for (float expectedFrame = bestPhase; expectedFrame < frameCount; expectedFrame += bestLag)
            {
                int center = Mathf.RoundToInt(expectedFrame);
                int from = Mathf.Max(0, center - snapRadius);
                int to = Mathf.Min(frameCount - 1, center + snapRadius);
                int snappedFrame = from;
                float strongestOnset = novelty[from];
                for (int frame = from + 1; frame <= to; frame++)
                {
                    if (novelty[frame] > strongestOnset)
                    {
                        strongestOnset = novelty[frame];
                        snappedFrame = frame;
                    }
                }

                if (snappedFrame <= lastSnappedFrame)
                {
                    continue;
                }

                float beatTime = snappedFrame * AnalysisHopSize / (float)frequency;
                beats.Add(new DetectedMusicBeat(beatTime, Mathf.Clamp01(strongestOnset / maximumNovelty)));
                lastSnappedFrame = snappedFrame;
            }

            float estimatedBpm = 60f * analysisFramesPerSecond / bestLag;
            return new AnalysisResult(beats, estimatedBpm);
        }

        private static float[] ReadDecodedSamples(
            AudioClip sourceClip,
            out int sampleFrames,
            out int channels,
            out int frequency)
        {
            string assetPath = AssetDatabase.GetAssetPath(sourceClip);
            AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            AudioImporterSampleSettings originalSettings = default;
            bool changedImporter = false;

            try
            {
                if (importer != null)
                {
                    originalSettings = importer.defaultSampleSettings;
                    AudioImporterSampleSettings readableSettings = originalSettings;
                    if (readableSettings.loadType != AudioClipLoadType.DecompressOnLoad ||
                        readableSettings.compressionFormat != AudioCompressionFormat.PCM)
                    {
                        readableSettings.loadType = AudioClipLoadType.DecompressOnLoad;
                        readableSettings.compressionFormat = AudioCompressionFormat.PCM;
                        importer.defaultSampleSettings = readableSettings;
                        importer.SaveAndReimport();
                        changedImporter = true;
                        sourceClip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                    }
                }

                if (sourceClip == null || !sourceClip.LoadAudioData())
                {
                    throw new InvalidOperationException("Unity không decode được AudioClip để phân tích.");
                }

                sampleFrames = sourceClip.samples;
                channels = sourceClip.channels;
                frequency = sourceClip.frequency;
                float[] samples = new float[sampleFrames * channels];
                if (!sourceClip.GetData(samples, 0))
                {
                    throw new InvalidOperationException("Không đọc được PCM. Kiểm tra import settings của AudioClip.");
                }

                return samples;
            }
            finally
            {
                if (changedImporter && importer != null)
                {
                    importer.defaultSampleSettings = originalSettings;
                    importer.SaveAndReimport();
                }
            }
        }
    }
}
