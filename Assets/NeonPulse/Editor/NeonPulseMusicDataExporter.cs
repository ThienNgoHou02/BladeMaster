using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NeonPulse.EditorTools
{
    /// <summary>Exports a self-contained, vendor-neutral music brief for generative music tools.</summary>
    public static class NeonPulseMusicDataExporter
    {
        private const string SchemaVersion = "2.0";
        private const string DefaultConfigPath = "Assets/NeonPulse/Resources/NeonPulseGameConfig.asset";
        private const float BeatGridTolerance = 0.02f;

        [Serializable]
        private sealed class MusicGenerationDocument
        {
            public string schemaVersion;
            public string documentType;
            public string generatedAtUtc;
            public string usage;
            public string copyPastePrompt;
            public LevelSummary level;
            public MusicDirection musicDirection;
            public TechnicalRequirements technicalRequirements;
            public SegmentGenerationPlan segmentGenerationPlan;
            public List<PhaseMusicData> phaseTimeline;
            public List<MusicGenerationSegment> generationSegments;
            public List<string> timingWarnings;
        }

        [Serializable]
        private sealed class LevelSummary
        {
            public string name;
            public float bpm;
            public int beatsPerBar;
            public string timeSignature;
            public float secondsPerBeat;
            public float totalDurationSeconds;
            public float approximateTotalBeats;
            public int phaseCount;
            public float transitionRestSeconds;
            public string sourceTimingMode;
        }

        [Serializable]
        private sealed class MusicDirection
        {
            public string genre;
            public string mood;
            public string musicalKey;
            public bool instrumentalOnly;
            public string additionalPrompt;
            public string arrangementGoal;
        }

        [Serializable]
        private sealed class TechnicalRequirements
        {
            public bool constantTempo;
            public bool allowTempoChanges;
            public float firstDownbeatAtSecond;
            public bool allowIntroSilence;
            public int recommendedSampleRate;
            public string exportFormat;
            public string synchronizationRule;
        }

        [Serializable]
        private sealed class PhaseMusicData
        {
            public int phaseNumber;
            public string gameplayAction;
            public string gameplayDescription;
            public float startSecond;
            public float endSecond;
            public float durationSeconds;
            public float startBeat;
            public float endBeat;
            public float startBar;
            public float endBar;
            public float gameplayPulseSeconds;
            public float gameplayPulseBeats;
            public float estimatedGameplayCuesPerMinute;
            public int objectsPerWave;
            public float flySpeed;
            public float travelDurationSeconds;
            public float firstGameplayCueSecond;
            public int energyLevel;
            public string suggestedSection;
            public string musicalTreatment;
            public bool pulseAlignedToQuarterBeatGrid;
        }

        [Serializable]
        private sealed class SegmentGenerationPlan
        {
            public float configuredMaximumDurationSeconds;
            public float longestGeneratedSegmentSeconds;
            public int segmentCount;
            public bool boundariesPreferFullBars;
            public string fileNamingPattern;
            public string generationWorkflow;
            public string assemblyInstructions;
            public string continuityRules;
        }

        [Serializable]
        private sealed class MusicGenerationSegment
        {
            public int segmentNumber;
            public string suggestedFileName;
            public float globalStartSecond;
            public float globalEndSecond;
            public float exactDurationSeconds;
            public float startBeat;
            public float endBeat;
            public float approximateBarCount;
            public bool startsOnBarBoundary;
            public bool isFirstSegment;
            public bool isFinalSegment;
            public string copyPastePrompt;
            public List<SegmentTimelineSlice> timeline;
        }

        [Serializable]
        private sealed class SegmentTimelineSlice
        {
            public string type;
            public int phaseNumber;
            public string gameplayAction;
            public float relativeStartSecond;
            public float relativeEndSecond;
            public float durationSeconds;
            public int energyLevel;
            public string musicalTreatment;
        }

        public static void Export(NeonPulseLevelDefinition level, NeonPulseGameConfig config)
        {
            if (level == null || config == null)
            {
                EditorUtility.DisplayDialog("Không thể xuất", "Level hoặc Gameplay Configuration đang bị thiếu.", "OK");
                return;
            }

            string defaultDirectory = Path.Combine(Application.dataPath, "NeonPulse", "MusicGeneration");
            Directory.CreateDirectory(defaultDirectory);
            string path = EditorUtility.SaveFilePanel(
                "Xuất data cho AI Gen nhạc",
                defaultDirectory,
                SanitizeFileName(level.LevelName) + "_MusicBrief",
                "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string json = BuildJson(level, config);
            File.WriteAllText(path, json, new UTF8Encoding(false));
            AssetDatabase.Refresh();

            UnityEngine.Object exportedAsset = TryLoadExportedAsset(path);
            if (exportedAsset != null)
            {
                Selection.activeObject = exportedAsset;
                EditorGUIUtility.PingObject(exportedAsset);
            }

            EditorUtility.DisplayDialog(
                "Đã xuất data Gen nhạc",
                "File đã được tạo tại:\n" + path +
                "\n\nMỗi phần tử trong generationSegments có prompt riêng để tạo clip dưới giới hạn thời lượng.",
                "OK");
        }

        public static string BuildJson(NeonPulseLevelDefinition level, NeonPulseGameConfig config)
        {
            MusicGenerationDocument document = BuildDocument(level, config);
            return JsonUtility.ToJson(document, true);
        }

        [MenuItem("Assets/Neon Pulse/Export AI Music Data", true)]
        private static bool CanExportSelectedLevel()
        {
            return Selection.activeObject is NeonPulseLevelDefinition;
        }

        [MenuItem("Assets/Neon Pulse/Export AI Music Data", priority = 2100)]
        private static void ExportSelectedLevel()
        {
            NeonPulseLevelDefinition level = Selection.activeObject as NeonPulseLevelDefinition;
            NeonPulseGameConfig config = AssetDatabase.LoadAssetAtPath<NeonPulseGameConfig>(DefaultConfigPath);
            Export(level, config);
        }

        private static MusicGenerationDocument BuildDocument(NeonPulseLevelDefinition level, NeonPulseGameConfig config)
        {
            float bpm = Mathf.Max(1f, config.Rhythm.Bpm);
            float secondsPerBeat = 60f / bpm;
            MusicGenerationSettings settings = level.MusicGeneration ?? new MusicGenerationSettings();
            int beatsPerBar = settings.BeatsPerBar;
            List<PhaseMusicData> phases = new List<PhaseMusicData>(level.Phases.Count);
            List<string> warnings = new List<string>(level.Phases.Count + 2);
            float cursor = 0f;

            for (int index = 0; index < level.Phases.Count; index++)
            {
                NeonPulseLevelPhase phase = level.Phases[index];
                if (phase == null)
                {
                    continue;
                }

                float endSecond = cursor + phase.DurationSeconds;
                float pulseBeats = phase.SpawnIntervalSeconds / secondsPerBeat;
                float travelDistance = Mathf.Max(0.1f, config.Rhythm.SpawnZ - config.Rhythm.HitZ);
                float travelDuration = travelDistance / Mathf.Max(0.01f, phase.FlySpeed);
                bool pulseAligned = IsQuarterBeatAligned(pulseBeats);
                if (!pulseAligned)
                {
                    warnings.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "Phase {0} ({1}) has a gameplay pulse of {2:0.###} beats; it is not aligned to a 1/4-beat grid.",
                        index + 1,
                        phase.Action,
                        pulseBeats));
                }

                phases.Add(new PhaseMusicData
                {
                    phaseNumber = index + 1,
                    gameplayAction = phase.Action.ToString(),
                    gameplayDescription = NeonPulseLevelPhase.GetDisplayName(phase.Action),
                    startSecond = Round(cursor),
                    endSecond = Round(endSecond),
                    durationSeconds = Round(phase.DurationSeconds),
                    startBeat = Round(cursor / secondsPerBeat),
                    endBeat = Round(endSecond / secondsPerBeat),
                    startBar = Round(cursor / secondsPerBeat / beatsPerBar + 1f),
                    endBar = Round(endSecond / secondsPerBeat / beatsPerBar + 1f),
                    gameplayPulseSeconds = Round(phase.SpawnIntervalSeconds),
                    gameplayPulseBeats = Round(pulseBeats),
                    estimatedGameplayCuesPerMinute = Round(60f / phase.SpawnIntervalSeconds),
                    objectsPerWave = phase.ObjectsPerWave,
                    flySpeed = Round(phase.FlySpeed),
                    travelDurationSeconds = Round(travelDuration),
                    firstGameplayCueSecond = Round(cursor + travelDuration),
                    energyLevel = GetEnergyLevel(phase),
                    suggestedSection = GetSuggestedSection(phase.Action, index, level.Phases.Count),
                    musicalTreatment = GetMusicalTreatment(phase.Action),
                    pulseAlignedToQuarterBeatGrid = pulseAligned
                });

                cursor = endSecond + (index < level.Phases.Count - 1 ? level.PhaseTransitionRestSeconds : 0f);
            }

            float transitionBeats = level.PhaseTransitionRestSeconds / secondsPerBeat;
            if (level.Phases.Count > 1 && !IsQuarterBeatAligned(transitionBeats))
            {
                warnings.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "The phase transition rest is {0:0.###} beats and is not aligned to a 1/4-beat grid.",
                    transitionBeats));
            }

            MusicGenerationDocument document = new MusicGenerationDocument
            {
                schemaVersion = SchemaVersion,
                documentType = "NeonPulseAIMusicGenerationBrief",
                generatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                usage = "Upload this JSON to an AI agent. For a duration-limited music generator, generate each generationSegments item separately with its copyPastePrompt, then assemble the clips using segmentGenerationPlan.",
                level = new LevelSummary
                {
                    name = level.LevelName,
                    bpm = Round(bpm),
                    beatsPerBar = beatsPerBar,
                    timeSignature = beatsPerBar + "/4",
                    secondsPerBeat = Round(secondsPerBeat),
                    totalDurationSeconds = Round(cursor),
                    approximateTotalBeats = Round(cursor / secondsPerBeat),
                    phaseCount = phases.Count,
                    transitionRestSeconds = Round(level.PhaseTransitionRestSeconds),
                    sourceTimingMode = "seconds; use phaseTimeline timestamps as the authoritative structure"
                },
                musicDirection = new MusicDirection
                {
                    genre = settings.Genre,
                    mood = settings.Mood,
                    musicalKey = settings.MusicalKey,
                    instrumentalOnly = settings.InstrumentalOnly,
                    additionalPrompt = settings.AdditionalPrompt,
                    arrangementGoal = "Music intensity and instrumentation should follow the ordered gameplay phases. Preserve a clearly audible beat for player timing."
                },
                technicalRequirements = new TechnicalRequirements
                {
                    constantTempo = true,
                    allowTempoChanges = false,
                    firstDownbeatAtSecond = 0f,
                    allowIntroSilence = false,
                    recommendedSampleRate = 48000,
                    exportFormat = "WAV preferred; OGG allowed for the final Unity import",
                    synchronizationRule = "Keep the beat grid stable at the specified BPM. Do not add silence, pickup notes or tempo drift before the first downbeat."
                },
                phaseTimeline = phases,
                timingWarnings = warnings
            };
            document.copyPastePrompt = BuildCopyPastePrompt(document);
            document.generationSegments = BuildGenerationSegments(
                document,
                settings.MaximumSegmentDurationSeconds,
                secondsPerBeat);

            float longestSegment = 0f;
            bool allSegmentStartsAlignToBars = true;
            for (int index = 0; index < document.generationSegments.Count; index++)
            {
                longestSegment = Mathf.Max(longestSegment, document.generationSegments[index].exactDurationSeconds);
                allSegmentStartsAlignToBars &= document.generationSegments[index].startsOnBarBoundary;
            }

            document.segmentGenerationPlan = new SegmentGenerationPlan
            {
                configuredMaximumDurationSeconds = Round(settings.MaximumSegmentDurationSeconds),
                longestGeneratedSegmentSeconds = Round(longestSegment),
                segmentCount = document.generationSegments.Count,
                boundariesPreferFullBars = allSegmentStartsAlignToBars,
                fileNamingPattern = SanitizeFileName(level.LevelName) + "_Music_Segment_XX.wav",
                generationWorkflow = "Generate every item in generationSegments separately, using that item's copyPastePrompt. Keep the same BPM, musical key, sound palette and mix across all segments.",
                assemblyInstructions = "Remove any silence added by the generator, trim or time-warp each result to exactDurationSeconds, then place it at globalStartSecond on one DAW timeline. Export the assembled timeline as one WAV file starting at 0.000 seconds.",
                continuityRules = "Middle segments must not contain an intro, outro or fade. Start on the next bar downbeat and continue the harmony, groove and instrumentation of the previous segment. Only the final segment may resolve and end."
            };
            return document;
        }

        private static List<MusicGenerationSegment> BuildGenerationSegments(
            MusicGenerationDocument document,
            float configuredMaximumDuration,
            float secondsPerBeat)
        {
            float totalDuration = document.level.totalDurationSeconds;
            float maximumDuration = Mathf.Max(1f, configuredMaximumDuration);
            float secondsPerBar = secondsPerBeat * document.level.beatsPerBar;
            int maximumFullBars = Mathf.FloorToInt((maximumDuration + 0.0001f) / secondsPerBar);

            if (maximumFullBars <= 0)
            {
                return BuildFixedDurationSegments(document, maximumDuration, secondsPerBeat);
            }

            float maximumBarAlignedDuration = maximumFullBars * secondsPerBar;
            int segmentCount = Mathf.Max(1, Mathf.CeilToInt(totalDuration / maximumBarAlignedDuration));
            int totalFullBars = Mathf.FloorToInt(totalDuration / secondsPerBar);
            int baseBarsPerSegment = totalFullBars / segmentCount;
            int extraBars = totalFullBars % segmentCount;
            List<MusicGenerationSegment> segments = new List<MusicGenerationSegment>(segmentCount);
            float cursor = 0f;

            for (int index = 0; index < segmentCount; index++)
            {
                int barsInSegment = baseBarsPerSegment + (index < extraBars ? 1 : 0);
                float endSecond = index == segmentCount - 1
                    ? totalDuration
                    : cursor + barsInSegment * secondsPerBar;

                if (endSecond <= cursor + 0.001f)
                {
                    endSecond = Mathf.Min(totalDuration, cursor + maximumDuration);
                }

                segments.Add(CreateGenerationSegment(
                    document,
                    index,
                    segmentCount,
                    cursor,
                    endSecond,
                    secondsPerBeat,
                    secondsPerBar));
                cursor = endSecond;
            }

            return segments;
        }

        private static List<MusicGenerationSegment> BuildFixedDurationSegments(
            MusicGenerationDocument document,
            float maximumDuration,
            float secondsPerBeat)
        {
            int segmentCount = Mathf.Max(1, Mathf.CeilToInt(document.level.totalDurationSeconds / maximumDuration));
            float secondsPerBar = secondsPerBeat * document.level.beatsPerBar;
            List<MusicGenerationSegment> segments = new List<MusicGenerationSegment>(segmentCount);
            float cursor = 0f;
            for (int index = 0; index < segmentCount; index++)
            {
                float endSecond = Mathf.Min(document.level.totalDurationSeconds, cursor + maximumDuration);
                segments.Add(CreateGenerationSegment(
                    document,
                    index,
                    segmentCount,
                    cursor,
                    endSecond,
                    secondsPerBeat,
                    secondsPerBar));
                cursor = endSecond;
            }

            return segments;
        }

        private static MusicGenerationSegment CreateGenerationSegment(
            MusicGenerationDocument document,
            int zeroBasedIndex,
            int segmentCount,
            float startSecond,
            float endSecond,
            float secondsPerBeat,
            float secondsPerBar)
        {
            List<SegmentTimelineSlice> timeline = BuildSegmentTimeline(document.phaseTimeline, startSecond, endSecond);
            MusicGenerationSegment segment = new MusicGenerationSegment
            {
                segmentNumber = zeroBasedIndex + 1,
                suggestedFileName = SanitizeFileName(document.level.name) + "_Music_Segment_" +
                                    (zeroBasedIndex + 1).ToString("D2", CultureInfo.InvariantCulture) + ".wav",
                globalStartSecond = Round(startSecond),
                globalEndSecond = Round(endSecond),
                exactDurationSeconds = Round(endSecond - startSecond),
                startBeat = Round(startSecond / secondsPerBeat),
                endBeat = Round(endSecond / secondsPerBeat),
                approximateBarCount = Round((endSecond - startSecond) / secondsPerBar),
                startsOnBarBoundary = IsBarBoundary(startSecond, secondsPerBar),
                isFirstSegment = zeroBasedIndex == 0,
                isFinalSegment = zeroBasedIndex == segmentCount - 1,
                timeline = timeline
            };
            segment.copyPastePrompt = BuildSegmentPrompt(document, segment, segmentCount);
            return segment;
        }

        private static List<SegmentTimelineSlice> BuildSegmentTimeline(
            List<PhaseMusicData> phases,
            float segmentStart,
            float segmentEnd)
        {
            List<SegmentTimelineSlice> slices = new List<SegmentTimelineSlice>(4);
            float coveredUntil = segmentStart;
            for (int index = 0; index < phases.Count; index++)
            {
                PhaseMusicData phase = phases[index];
                if (phase.endSecond <= segmentStart || phase.startSecond >= segmentEnd)
                {
                    continue;
                }

                float intersectionStart = Mathf.Max(segmentStart, phase.startSecond);
                float intersectionEnd = Mathf.Min(segmentEnd, phase.endSecond);
                if (intersectionStart > coveredUntil + 0.001f)
                {
                    AddTransitionSlice(slices, segmentStart, coveredUntil, intersectionStart);
                }

                slices.Add(new SegmentTimelineSlice
                {
                    type = "gameplayPhase",
                    phaseNumber = phase.phaseNumber,
                    gameplayAction = phase.gameplayAction,
                    relativeStartSecond = Round(intersectionStart - segmentStart),
                    relativeEndSecond = Round(intersectionEnd - segmentStart),
                    durationSeconds = Round(intersectionEnd - intersectionStart),
                    energyLevel = phase.energyLevel,
                    musicalTreatment = phase.musicalTreatment
                });
                coveredUntil = Mathf.Max(coveredUntil, intersectionEnd);
            }

            if (coveredUntil < segmentEnd - 0.001f)
            {
                AddTransitionSlice(slices, segmentStart, coveredUntil, segmentEnd);
            }

            return slices;
        }

        private static void AddTransitionSlice(
            List<SegmentTimelineSlice> slices,
            float segmentStart,
            float startSecond,
            float endSecond)
        {
            slices.Add(new SegmentTimelineSlice
            {
                type = "transitionRest",
                phaseNumber = 0,
                gameplayAction = "TransitionRest",
                relativeStartSecond = Round(startSecond - segmentStart),
                relativeEndSecond = Round(endSecond - segmentStart),
                durationSeconds = Round(endSecond - startSecond),
                energyLevel = 4,
                musicalTreatment = "short transition fill, riser or breathing space; keep the tempo and beat grid unchanged"
            });
        }

        private static string BuildSegmentPrompt(
            MusicGenerationDocument document,
            MusicGenerationSegment segment,
            int segmentCount)
        {
            StringBuilder builder = new StringBuilder(768);
            builder.Append("Generate music segment ");
            builder.Append(segment.segmentNumber);
            builder.Append(" of ");
            builder.Append(segmentCount);
            builder.Append(" for one continuous rhythm fitness game track. Style: ");
            builder.Append(document.musicDirection.genre);
            builder.Append(". Mood: ");
            builder.Append(document.musicDirection.mood);
            builder.Append(". Musical key: ");
            builder.Append(document.musicDirection.musicalKey);
            builder.Append(". Keep exactly ");
            builder.Append(document.level.bpm.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" BPM in ");
            builder.Append(document.level.timeSignature);
            builder.Append(". Generate exactly ");
            builder.Append(segment.exactDurationSeconds.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" seconds, not the generator's default duration. No leading or trailing silence. ");

            if (segment.isFirstSegment)
            {
                builder.Append("This is the first segment: start immediately on the first downbeat with no pickup or fade-in. ");
            }
            else
            {
                builder.Append("This is a continuation: begin on the next bar downbeat, preserve the previous segment's harmony, groove, instruments and mix, with no new intro or fade-in. ");
            }

            if (segment.isFinalSegment)
            {
                builder.Append("This is the final segment: create a resolved ending exactly at the requested duration. ");
            }
            else
            {
                builder.Append("Do not add an outro, cadence or fade-out; leave the music ready to continue seamlessly. ");
            }

            builder.Append("Relative timeline inside this segment: ");
            for (int index = 0; index < segment.timeline.Count; index++)
            {
                SegmentTimelineSlice slice = segment.timeline[index];
                if (index > 0)
                {
                    builder.Append("; ");
                }

                builder.Append('[');
                builder.Append(slice.relativeStartSecond.ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append('-');
                builder.Append(slice.relativeEndSecond.ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append("s] ");
                builder.Append(slice.gameplayAction);
                builder.Append(", energy ");
                builder.Append(slice.energyLevel);
                builder.Append("/10, ");
                builder.Append(slice.musicalTreatment);
            }

            if (!string.IsNullOrWhiteSpace(document.musicDirection.additionalPrompt))
            {
                builder.Append(". Additional direction: ");
                builder.Append(document.musicDirection.additionalPrompt.Trim());
            }

            return builder.ToString();
        }

        private static bool IsBarBoundary(float time, float secondsPerBar)
        {
            float barPosition = time / secondsPerBar;
            return Mathf.Abs(barPosition - Mathf.Round(barPosition)) <= BeatGridTolerance;
        }

        private static string BuildCopyPastePrompt(MusicGenerationDocument document)
        {
            StringBuilder builder = new StringBuilder(1024);
            builder.Append("Create ");
            builder.Append(document.musicDirection.instrumentalOnly ? "an instrumental " : "a ");
            builder.Append(document.musicDirection.genre);
            builder.Append(" track for a rhythm fitness game. Mood: ");
            builder.Append(document.musicDirection.mood);
            builder.Append(". Keep exactly ");
            builder.Append(document.level.bpm.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" BPM in ");
            builder.Append(document.level.timeSignature);
            builder.Append(" with no tempo changes or tempo drift. The first downbeat must be exactly at 0.000 seconds with no intro silence. Target usable duration: ");
            builder.Append(document.level.totalDurationSeconds.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" seconds. Follow this structure: ");

            for (int index = 0; index < document.phaseTimeline.Count; index++)
            {
                PhaseMusicData phase = document.phaseTimeline[index];
                if (index > 0)
                {
                    builder.Append("; ");
                }

                builder.Append('[');
                builder.Append(phase.startSecond.ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append('-');
                builder.Append(phase.endSecond.ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append("s] ");
                builder.Append(phase.suggestedSection);
                builder.Append(", energy ");
                builder.Append(phase.energyLevel);
                builder.Append("/10, ");
                builder.Append(phase.musicalTreatment);
            }

            builder.Append(". Treat every gap between listed phases as a short transition fill or breathing section without changing tempo");

            if (!string.IsNullOrWhiteSpace(document.musicDirection.additionalPrompt))
            {
                builder.Append(". Additional direction: ");
                builder.Append(document.musicDirection.additionalPrompt.Trim());
            }

            builder.Append(". Export without leading or trailing silence so Unity can schedule it sample-accurately.");
            return builder.ToString();
        }

        private static int GetEnergyLevel(NeonPulseLevelPhase phase)
        {
            int baseEnergy;
            switch (phase.Action)
            {
                case LevelPhaseAction.RhythmTiles: baseEnergy = 5; break;
                case LevelPhaseAction.PunchObjects: baseEnergy = 6; break;
                case LevelPhaseAction.SlashObjects: baseEnergy = 7; break;
                case LevelPhaseAction.DodgeWalls: baseEnergy = 7; break;
                case LevelPhaseAction.RandomMixed: baseEnergy = 9; break;
                case LevelPhaseAction.OverheadClap: baseEnergy = 6; break;
                default: baseEnergy = 5; break;
            }

            int speedEnergy = Mathf.RoundToInt(Mathf.InverseLerp(8f, 30f, phase.FlySpeed) * 2f);
            return Mathf.Clamp(baseEnergy + speedEnergy, 1, 10);
        }

        private static string GetSuggestedSection(LevelPhaseAction action, int index, int phaseCount)
        {
            if (index == 0)
            {
                return "intro groove with immediate downbeat";
            }

            if (index == phaseCount - 1)
            {
                return "final climax and resolved ending";
            }

            switch (action)
            {
                case LevelPhaseAction.RhythmTiles: return "groove section";
                case LevelPhaseAction.PunchObjects: return "driving verse";
                case LevelPhaseAction.SlashObjects: return "aggressive drop";
                case LevelPhaseAction.DodgeWalls: return "tension and movement section";
                case LevelPhaseAction.RandomMixed: return "peak-energy drop";
                case LevelPhaseAction.OverheadClap: return "anthemic accent section";
                default: return "controlled breakdown with a strong pulse";
            }
        }

        private static string GetMusicalTreatment(LevelPhaseAction action)
        {
            switch (action)
            {
                case LevelPhaseAction.RhythmTiles:
                    return "clear kick and hi-hat footwork pulse, stable bass groove";
                case LevelPhaseAction.PunchObjects:
                    return "heavy kick/snare impacts and short rhythmic stabs";
                case LevelPhaseAction.SlashObjects:
                    return "sharp synth transients, distorted bass and sweeping accents";
                case LevelPhaseAction.DodgeWalls:
                    return "rising tension, stereo movement and impacts while keeping the beat obvious";
                case LevelPhaseAction.RandomMixed:
                    return "full drums, layered synths and maximum rhythmic intensity";
                case LevelPhaseAction.OverheadClap:
                    return "wide clap accents, uplifting chords and strong downbeats";
                case LevelPhaseAction.LegDrawUp:
                    return "reduced arrangement, sustained pulse and clear hold/release tension";
                default:
                    return "clear electronic workout groove";
            }
        }

        private static bool IsQuarterBeatAligned(float beats)
        {
            return Mathf.Abs(beats * 4f - Mathf.Round(beats * 4f)) <= BeatGridTolerance;
        }

        private static float Round(float value)
        {
            return (float)Math.Round(value, 3, MidpointRounding.AwayFromZero);
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "NeonPulseLevel";
            }

            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            StringBuilder builder = new StringBuilder(value.Length);
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                builder.Append(Array.IndexOf(invalidCharacters, character) >= 0 ? '_' : character);
            }

            return builder.ToString();
        }

        private static UnityEngine.Object TryLoadExportedAsset(string absolutePath)
        {
            string normalizedAssetsPath = Application.dataPath.Replace('\\', '/');
            string normalizedFilePath = absolutePath.Replace('\\', '/');
            if (!normalizedFilePath.StartsWith(normalizedAssetsPath, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string assetPath = "Assets" + normalizedFilePath.Substring(normalizedAssetsPath.Length);
            return AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
        }
    }
}
