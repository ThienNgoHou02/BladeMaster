using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NeonPulse.EditorTools
{
    /// <summary>Exports one self-contained AI music generation brief for each gameplay phase.</summary>
    public static class NeonPulseMusicDataExporter
    {
        private const string SchemaVersion = "3.0";
        private const string DefaultConfigPath = "Assets/NeonPulse/Resources/NeonPulseGameConfig.asset";
        private const float BeatGridTolerance = 0.02f;
        private const int MaximumExportedCues = 2048;

        [Serializable]
        private sealed class PhaseMusicGenerationDocument
        {
            public string schemaVersion;
            public string documentType;
            public string generatedAtUtc;
            public string usage;
            public PhaseIdentity phase;
            public MusicDirection musicDirection;
            public PhaseTimingConfiguration timing;
            public GameplayConfiguration gameplay;
            public TechnicalRequirements technicalRequirements;
            public List<BeatActionCue> actionCues;
            public string copyPastePrompt;
            public List<string> timingWarnings;
        }

        [Serializable]
        private sealed class PhaseIdentity
        {
            public string levelName;
            public int phaseNumber;
            public int phaseCount;
            public string gameplayAction;
            public string gameplayDescription;
            public string suggestedSection;
            public int energyLevel;
        }

        [Serializable]
        private sealed class MusicDirection
        {
            public string genre;
            public string mood;
            public string musicalKey;
            public bool instrumentalOnly;
            public string musicalTreatment;
            public string additionalPrompt;
        }

        [Serializable]
        private sealed class PhaseTimingConfiguration
        {
            public float bpm;
            public int beatsPerBar;
            public string timeSignature;
            public float secondsPerBeat;
            public float exactDurationSeconds;
            public float exactDurationBeats;
            public int completeBars;
            public float remainingBeats;
            public bool durationEndsOnBeat;
            public bool durationEndsOnBar;
            public int firstActionBeatIndex;
            public int firstActionMusicalBeatNumber;
            public float firstActionSecond;
            public int actionIntervalBeats;
            public float beatAlignedActionIntervalSeconds;
            public int actionCueCount;
        }

        [Serializable]
        private sealed class GameplayConfiguration
        {
            public float authoredSpawnIntervalSeconds;
            public float authoredSpawnIntervalBeats;
            public float recommendedSpawnIntervalSeconds;
            public int recommendedSpawnIntervalBeats;
            public int objectsPerWave;
            public float flySpeed;
            public float travelDurationSeconds;
            public float firstSpawnSecond;
            public float authoredHoldDurationSeconds;
            public float holdDurationBeats;
            public string synchronizationRule;
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
            public string trimmingRule;
        }

        [Serializable]
        private sealed class BeatActionCue
        {
            public int cueNumber;
            public int beatIndex;
            public int musicalBeatNumber;
            public int bar;
            public int beatInBar;
            public float hitSecond;
            public float spawnSecond;
            public string accent;
        }

        public static void Export(NeonPulseLevelDefinition level, NeonPulseGameConfig config)
        {
            if (level == null || config == null)
            {
                EditorUtility.DisplayDialog("Không thể xuất", "Level hoặc Gameplay Configuration đang bị thiếu.", "OK");
                return;
            }

            if (!level.ValidateDefinition(out string validationMessage))
            {
                EditorUtility.DisplayDialog("Không thể xuất", validationMessage, "OK");
                return;
            }

            string defaultDirectory = Path.Combine(Application.dataPath, "NeonPulse", "MusicGeneration");
            Directory.CreateDirectory(defaultDirectory);
            string directory = EditorUtility.SaveFolderPanel(
                "Chọn thư mục xuất data nhạc theo phase",
                defaultDirectory,
                SanitizeFileName(level.LevelName) + "_MusicPhases");
            if (string.IsNullOrEmpty(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);
            int exportedCount = 0;
            string firstExportedPath = null;
            for (int index = 0; index < level.Phases.Count; index++)
            {
                NeonPulseLevelPhase phase = level.Phases[index];
                if (phase == null)
                {
                    continue;
                }

                string fileName = BuildPhaseFileName(level, phase, index);
                string path = Path.Combine(directory, fileName);
                File.WriteAllText(path, BuildPhaseJson(level, config, index), new UTF8Encoding(false));
                firstExportedPath = firstExportedPath ?? path;
                exportedCount++;
            }

            AssetDatabase.Refresh();
            UnityEngine.Object exportedAsset = TryLoadExportedAsset(firstExportedPath);
            if (exportedAsset != null)
            {
                Selection.activeObject = exportedAsset;
                EditorGUIUtility.PingObject(exportedAsset);
            }

            EditorUtility.DisplayDialog(
                "Đã xuất data Gen nhạc theo phase",
                "Đã tạo " + exportedCount + " file JSON tại:\n" + directory +
                "\n\nMỗi file tương ứng đúng một phase và chứa danh sách beat action cue riêng.",
                "OK");
        }

        public static string BuildPhaseJson(
            NeonPulseLevelDefinition level,
            NeonPulseGameConfig config,
            int phaseIndex)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (phaseIndex < 0 || phaseIndex >= level.Phases.Count || level.Phases[phaseIndex] == null)
            {
                throw new ArgumentOutOfRangeException(nameof(phaseIndex));
            }

            PhaseMusicGenerationDocument document = BuildPhaseDocument(level, config, phaseIndex);
            return JsonUtility.ToJson(document, true);
        }

        [MenuItem("Assets/Neon Pulse/Export AI Music Data By Phase", true)]
        private static bool CanExportSelectedLevel()
        {
            return Selection.activeObject is NeonPulseLevelDefinition;
        }

        [MenuItem("Assets/Neon Pulse/Export AI Music Data By Phase", priority = 2100)]
        private static void ExportSelectedLevel()
        {
            NeonPulseLevelDefinition level = Selection.activeObject as NeonPulseLevelDefinition;
            NeonPulseGameConfig config = AssetDatabase.LoadAssetAtPath<NeonPulseGameConfig>(DefaultConfigPath);
            Export(level, config);
        }

        private static PhaseMusicGenerationDocument BuildPhaseDocument(
            NeonPulseLevelDefinition level,
            NeonPulseGameConfig config,
            int phaseIndex)
        {
            NeonPulseLevelPhase phase = level.Phases[phaseIndex];
            RhythmSettings rhythm = config.Rhythm;
            MusicGenerationSettings settings = level.MusicGeneration ?? new MusicGenerationSettings();
            float secondsPerBeat = rhythm.SecondsPerBeat;
            float durationBeats = phase.DurationSeconds / secondsPerBeat;
            int beatsPerBar = settings.BeatsPerBar;
            int firstActionBeat = NeonPulsePhaseBeatTiming.GetFirstActionBeat(phase, rhythm);
            int actionIntervalBeats = NeonPulsePhaseBeatTiming.GetActionIntervalBeats(phase, phase.Action, rhythm);
            float travelDuration = NeonPulsePhaseBeatTiming.GetTravelDurationSeconds(phase, rhythm);
            List<BeatActionCue> cues = BuildActionCues(
                phase,
                firstActionBeat,
                actionIntervalBeats,
                secondsPerBeat,
                beatsPerBar,
                travelDuration);
            List<string> warnings = BuildTimingWarnings(
                phase,
                settings,
                durationBeats,
                actionIntervalBeats,
                secondsPerBeat);

            PhaseMusicGenerationDocument document = new PhaseMusicGenerationDocument
            {
                schemaVersion = SchemaVersion,
                documentType = "NeonPulseAIPhaseMusicGenerationBrief",
                generatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                usage = phase.Action == LevelPhaseAction.RandomMixed
                    ? "Generate exactly one standalone music clip for this phase. Beat index 0 is the first downbeat at 0.000s. actionCues define the nominal accent grid; randomized actions may use a subset of whole beats."
                    : "Generate exactly one standalone music clip for this phase. Beat index 0 is the first downbeat at 0.000s. Use actionCues as the authoritative hit/accent timeline.",
                phase = new PhaseIdentity
                {
                    levelName = level.LevelName,
                    phaseNumber = phaseIndex + 1,
                    phaseCount = level.Phases.Count,
                    gameplayAction = phase.Action.ToString(),
                    gameplayDescription = NeonPulseLevelPhase.GetDisplayName(phase.Action),
                    suggestedSection = GetSuggestedSection(phase.Action, phaseIndex, level.Phases.Count),
                    energyLevel = GetEnergyLevel(phase)
                },
                musicDirection = new MusicDirection
                {
                    genre = settings.Genre,
                    mood = settings.Mood,
                    musicalKey = settings.MusicalKey,
                    instrumentalOnly = settings.InstrumentalOnly,
                    musicalTreatment = GetMusicalTreatment(phase.Action),
                    additionalPrompt = settings.AdditionalPrompt
                },
                timing = new PhaseTimingConfiguration
                {
                    bpm = Round(rhythm.Bpm),
                    beatsPerBar = beatsPerBar,
                    timeSignature = beatsPerBar + "/4",
                    secondsPerBeat = Round(secondsPerBeat),
                    exactDurationSeconds = Round(phase.DurationSeconds),
                    exactDurationBeats = Round(durationBeats),
                    completeBars = Mathf.FloorToInt(durationBeats / beatsPerBar),
                    remainingBeats = Round(Mathf.Repeat(durationBeats, beatsPerBar)),
                    durationEndsOnBeat = IsWholeBeatAligned(durationBeats),
                    durationEndsOnBar = IsWholeBeatAligned(durationBeats / beatsPerBar),
                    firstActionBeatIndex = firstActionBeat,
                    firstActionMusicalBeatNumber = firstActionBeat + 1,
                    firstActionSecond = Round(firstActionBeat * secondsPerBeat),
                    actionIntervalBeats = actionIntervalBeats,
                    beatAlignedActionIntervalSeconds = Round(actionIntervalBeats * secondsPerBeat),
                    actionCueCount = cues.Count
                },
                gameplay = new GameplayConfiguration
                {
                    authoredSpawnIntervalSeconds = Round(phase.SpawnIntervalSeconds),
                    authoredSpawnIntervalBeats = Round(phase.SpawnIntervalSeconds / secondsPerBeat),
                    recommendedSpawnIntervalSeconds = Round(actionIntervalBeats * secondsPerBeat),
                    recommendedSpawnIntervalBeats = actionIntervalBeats,
                    objectsPerWave = phase.ObjectsPerWave,
                    flySpeed = Round(phase.FlySpeed),
                    travelDurationSeconds = Round(travelDuration),
                    firstSpawnSecond = cues.Count > 0 ? cues[0].spawnSecond : -1f,
                    authoredHoldDurationSeconds = Round(phase.HoldDurationSeconds),
                    holdDurationBeats = Round(phase.HoldDurationSeconds / secondsPerBeat),
                    synchronizationRule = "Spawn time compensates for object travel. Player hit/action time is on a whole local beat; spawn time may be off-beat."
                },
                technicalRequirements = new TechnicalRequirements
                {
                    constantTempo = true,
                    allowTempoChanges = false,
                    firstDownbeatAtSecond = 0f,
                    allowIntroSilence = false,
                    recommendedSampleRate = 48000,
                    exportFormat = "WAV preferred; OGG allowed for Unity import",
                    trimmingRule = "Return audio with no leading silence. Trim the usable clip to exactDurationSeconds without changing BPM."
                },
                actionCues = cues,
                timingWarnings = warnings
            };
            document.copyPastePrompt = BuildCopyPastePrompt(document);
            return document;
        }

        private static List<BeatActionCue> BuildActionCues(
            NeonPulseLevelPhase phase,
            int firstActionBeat,
            int actionIntervalBeats,
            float secondsPerBeat,
            int beatsPerBar,
            float travelDuration)
        {
            int capacity = Mathf.Min(
                MaximumExportedCues,
                Mathf.Max(0, Mathf.CeilToInt(phase.DurationSeconds / (actionIntervalBeats * secondsPerBeat))));
            List<BeatActionCue> cues = new List<BeatActionCue>(capacity);
            int beat = firstActionBeat;
            while (cues.Count < MaximumExportedCues)
            {
                float hitSecond = beat * secondsPerBeat;
                if (hitSecond > phase.DurationSeconds + BeatGridTolerance)
                {
                    break;
                }

                if (phase.Action == LevelPhaseAction.LegDrawUp &&
                    hitSecond + phase.HoldDurationSeconds > phase.DurationSeconds + BeatGridTolerance)
                {
                    break;
                }

                int beatInBar = beat % beatsPerBar + 1;
                cues.Add(new BeatActionCue
                {
                    cueNumber = cues.Count + 1,
                    beatIndex = beat,
                    musicalBeatNumber = beat + 1,
                    bar = beat / beatsPerBar + 1,
                    beatInBar = beatInBar,
                    hitSecond = Round(hitSecond),
                    spawnSecond = Round(Mathf.Max(0f, hitSecond - travelDuration)),
                    accent = beatInBar == 1 ? "strong downbeat accent" : "clear rhythmic accent"
                });
                beat += actionIntervalBeats;
            }

            return cues;
        }

        private static List<string> BuildTimingWarnings(
            NeonPulseLevelPhase phase,
            MusicGenerationSettings settings,
            float durationBeats,
            int actionIntervalBeats,
            float secondsPerBeat)
        {
            List<string> warnings = new List<string>(3);
            float authoredIntervalBeats = phase.SpawnIntervalSeconds / secondsPerBeat;
            if (Mathf.Abs(authoredIntervalBeats - actionIntervalBeats) > BeatGridTolerance)
            {
                warnings.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "Authored spawn interval {0:0.###}s equals {1:0.###} beats. Runtime/export use {2} beats ({3:0.###}s) so player actions land on beats.",
                    phase.SpawnIntervalSeconds,
                    authoredIntervalBeats,
                    actionIntervalBeats,
                    actionIntervalBeats * secondsPerBeat));
            }

            if (phase.Action == LevelPhaseAction.RandomMixed)
            {
                warnings.Add(
                    "RandomMixed resolves each action at runtime. Every player action stays on a whole beat, but hold actions may advance to a later beat than the nominal actionCues grid.");
            }

            if (!IsWholeBeatAligned(durationBeats))
            {
                warnings.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "Phase duration ends at beat {0:0.###}, not a whole beat. Keep the requested duration; do not place an action after the last whole beat.",
                    durationBeats));
            }

            if (phase.DurationSeconds > settings.MaximumSegmentDurationSeconds + BeatGridTolerance)
            {
                warnings.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "Phase duration {0:0.###}s exceeds the configured generator limit {1:0.###}s. Use a generator that supports the full phase duration or extend the result before trimming.",
                    phase.DurationSeconds,
                    settings.MaximumSegmentDurationSeconds));
            }

            return warnings;
        }

        private static string BuildCopyPastePrompt(PhaseMusicGenerationDocument document)
        {
            StringBuilder builder = new StringBuilder(1024);
            builder.Append("Generate one standalone ");
            if (document.musicDirection.instrumentalOnly)
            {
                builder.Append("instrumental ");
            }

            builder.Append("gameplay music clip for phase ");
            builder.Append(document.phase.phaseNumber);
            builder.Append("/");
            builder.Append(document.phase.phaseCount);
            builder.Append(" (action: ");
            builder.Append(document.phase.gameplayAction);
            builder.Append("). Style: ");
            builder.Append(document.musicDirection.genre);
            builder.Append(". Mood: ");
            builder.Append(document.musicDirection.mood);
            builder.Append(". Key: ");
            builder.Append(document.musicDirection.musicalKey);
            builder.Append(". Keep exactly ");
            builder.Append(document.timing.bpm.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" BPM in ");
            builder.Append(document.timing.timeSignature);
            builder.Append(" with no tempo drift. Start the first downbeat at 0.000s with no pickup or intro silence. Exact usable duration: ");
            builder.Append(document.timing.exactDurationSeconds.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append("s. Energy ");
            builder.Append(document.phase.energyLevel);
            builder.Append("/10. Musical treatment: ");
            builder.Append(document.musicDirection.musicalTreatment);
            builder.Append(". Make every beat clearly audible. Add a synchronized transient/accent at these player action beats: ");

            if (document.actionCues.Count == 0)
            {
                builder.Append("none; keep a clear beat grid throughout");
            }
            else
            {
                for (int index = 0; index < document.actionCues.Count; index++)
                {
                    if (index > 0)
                    {
                        builder.Append(", ");
                    }

                    BeatActionCue cue = document.actionCues[index];
                    builder.Append("bar ");
                    builder.Append(cue.bar);
                    builder.Append(" beat ");
                    builder.Append(cue.beatInBar);
                    builder.Append(" [beat index ");
                    builder.Append(cue.beatIndex);
                    builder.Append("] (");
                    builder.Append(cue.hitSecond.ToString("0.###", CultureInfo.InvariantCulture));
                    builder.Append("s)");
                }
            }

            builder.Append(". Do not shift, swing or humanize these cue positions. ");
            if (!string.IsNullOrWhiteSpace(document.musicDirection.additionalPrompt))
            {
                builder.Append(document.musicDirection.additionalPrompt.Trim());
                builder.Append(" ");
            }

            builder.Append("Export without leading/trailing silence and trim exactly to the requested duration without changing BPM.");
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
                case LevelPhaseAction.LegDrawUp: baseEnergy = 4; break;
                default: baseEnergy = 5; break;
            }

            int densityBonus = phase.SpawnIntervalSeconds <= 0.5f ? 2 : phase.SpawnIntervalSeconds <= 1f ? 1 : 0;
            int waveBonus = phase.ObjectsPerWave > 1 ? 1 : 0;
            return Mathf.Clamp(baseEnergy + densityBonus + waveBonus, 1, 10);
        }

        private static string GetSuggestedSection(LevelPhaseAction action, int index, int phaseCount)
        {
            if (index == 0)
            {
                return "intro groove with immediate downbeat";
            }

            if (index == phaseCount - 1)
            {
                return "final peak with a clean ending at the exact phase duration";
            }

            switch (action)
            {
                case LevelPhaseAction.RhythmTiles: return "groove section";
                case LevelPhaseAction.PunchObjects: return "driving verse";
                case LevelPhaseAction.SlashObjects: return "aggressive drop";
                case LevelPhaseAction.DodgeWalls: return "tension and movement section";
                case LevelPhaseAction.RandomMixed: return "peak-energy drop";
                case LevelPhaseAction.OverheadClap: return "anthemic accent section";
                case LevelPhaseAction.LegDrawUp: return "controlled low-body groove";
                default: return "rhythmic gameplay section";
            }
        }

        private static string GetMusicalTreatment(LevelPhaseAction action)
        {
            switch (action)
            {
                case LevelPhaseAction.RhythmTiles:
                    return "clear kick and hi-hat footwork pulse with a stable bass groove";
                case LevelPhaseAction.PunchObjects:
                    return "heavy kick/snare impacts and short rhythmic stabs";
                case LevelPhaseAction.SlashObjects:
                    return "sharp synth transients, distorted bass and sweeping accents";
                case LevelPhaseAction.DodgeWalls:
                    return "rising tension, stereo movement and impacts while keeping every beat obvious";
                case LevelPhaseAction.RandomMixed:
                    return "full drums, layered synths and maximum rhythmic clarity";
                case LevelPhaseAction.OverheadClap:
                    return "wide clap accents, uplifting chords and strong downbeats";
                case LevelPhaseAction.LegDrawUp:
                    return "controlled bass pulses, grounded kick accents and sustained tension during holds";
                default:
                    return "clear rhythmic accents synchronized to gameplay cues";
            }
        }

        private static bool IsWholeBeatAligned(float beats)
        {
            return Mathf.Abs(beats - Mathf.Round(beats)) <= BeatGridTolerance;
        }

        private static float Round(float value)
        {
            return (float)Math.Round(value, 4, MidpointRounding.AwayFromZero);
        }

        private static string BuildPhaseFileName(
            NeonPulseLevelDefinition level,
            NeonPulseLevelPhase phase,
            int phaseIndex)
        {
            return SanitizeFileName(level.LevelName) + "_Phase_" +
                   (phaseIndex + 1).ToString("D2", CultureInfo.InvariantCulture) + "_" +
                   SanitizeFileName(phase.Action.ToString()) + "_MusicBrief.json";
        }

        private static string SanitizeFileName(string value)
        {
            string safeValue = string.IsNullOrWhiteSpace(value) ? "NeonPulse" : value.Trim();
            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            for (int index = 0; index < invalidCharacters.Length; index++)
            {
                safeValue = safeValue.Replace(invalidCharacters[index], '_');
            }

            return safeValue;
        }

        private static UnityEngine.Object TryLoadExportedAsset(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
            {
                return null;
            }

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
