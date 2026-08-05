using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace NeonPulse
{
    [Serializable]
    public sealed class RhythmSettings
    {
        [SerializeField, Min(60f)] private float bpm = 105f;
        [SerializeField, Min(8)] private int songBeats = 64;
        [SerializeField, Min(2f)] private float travelBeats = 6f;
        [SerializeField, Min(0f)] private float tileWaveStartBeat = 26f;
        [SerializeField, Min(0f)] private float tileWaveEndBeat = 38f;
        [SerializeField] private float spawnZ = 50f;
        [SerializeField] private float hitZ = 1.5f;
        [SerializeField] private float despawnZ = -5f;
        [SerializeField, Min(5f)] private float labelVisibleZ = 30f;
        [SerializeField, Range(0f, 5f)] private float countdownDuration = 3f;
        [SerializeField, Range(0f, 3f)] private float resultDelay = 0.6f;
        [SerializeField] private bool randomizeSpawnPattern = true;
        [SerializeField, Range(0.02f, 0.2f)] private float perfectWindow = 0.08f;
        [SerializeField, Range(0.05f, 0.3f)] private float greatWindow = 0.16f;
        [SerializeField, Range(0.1f, 0.5f)] private float goodWindow = 0.28f;
        [SerializeField, Range(0.1f, 1f)] private float holdWindowLead = 0.45f;
        [SerializeField, Range(0.05f, 0.5f)] private float holdInputGrace = 0.18f;
        [SerializeField, Range(0.1f, 1f)] private float holdWindowTrail = 0.5f;

        public float Bpm => bpm;
        public int SongBeats => songBeats;
        public float TravelBeats => travelBeats;
        public float TileWaveStartBeat => tileWaveStartBeat;
        public float TileWaveEndBeat => tileWaveEndBeat;
        public float SpawnZ => spawnZ;
        public float HitZ => hitZ;
        public float DespawnZ => despawnZ;
        public float LabelVisibleZ => labelVisibleZ;
        public float CountdownDuration => countdownDuration;
        public float ResultDelay => resultDelay;
        public bool RandomizeSpawnPattern => randomizeSpawnPattern;
        public float PerfectWindow => perfectWindow;
        public float GreatWindow => greatWindow;
        public float GoodWindow => goodWindow;
        public float HoldWindowLead => holdWindowLead;
        public float HoldInputGrace => holdInputGrace;
        public float HoldWindowTrail => holdWindowTrail;
        public float SecondsPerBeat => 60f / bpm;

        /// <summary>Returns true when a tile belongs to the dedicated middle-game wave.</summary>
        public bool ContainsTileBeat(float beat)
        {
            return beat >= tileWaveStartBeat && beat <= tileWaveEndBeat;
        }

        /// <summary>Checks whether a travelling target or obstacle would be visible during the tile wave.</summary>
        public bool GameplayEventOverlapsTileWave(float eventBeat)
        {
            float eventVisibleStart = eventBeat - travelBeats;
            float tileWaveVisibleStart = tileWaveStartBeat - travelBeats;
            return eventBeat >= tileWaveVisibleStart && eventVisibleStart <= tileWaveEndBeat;
        }
    }

    [Serializable]
    public sealed class ScoreSettings
    {
        [SerializeField, Min(0)] private int perfectPoints = 1000;
        [SerializeField, Min(0)] private int greatPoints = 750;
        [SerializeField, Min(0)] private int goodPoints = 500;
        [SerializeField, Min(0)] private int perfectComboBonus = 10;
        [SerializeField, Min(0)] private int greatComboBonus = 7;
        [SerializeField, Min(0)] private int goodComboBonus = 5;

        public int PerfectPoints => perfectPoints;
        public int GreatPoints => greatPoints;
        public int GoodPoints => goodPoints;
        public int PerfectComboBonus => perfectComboBonus;
        public int GreatComboBonus => greatComboBonus;
        public int GoodComboBonus => goodComboBonus;
    }

    [Serializable]
    public sealed class InputBindingSettings
    {
        [SerializeField] private KeyCode leftPunch = KeyCode.Q;
        [SerializeField] private KeyCode leftPunchAlternative = KeyCode.LeftArrow;
        [SerializeField] private KeyCode rightPunch = KeyCode.E;
        [SerializeField] private KeyCode rightPunchAlternative = KeyCode.RightArrow;
        [SerializeField] private KeyCode bothPunch = KeyCode.F;
        [SerializeField] private KeyCode duck = KeyCode.S;
        [SerializeField] private KeyCode duckAlternative = KeyCode.DownArrow;
        [SerializeField] private KeyCode jump = KeyCode.Space;
        [SerializeField] private KeyCode jumpAlternative = KeyCode.W;
        [SerializeField] private KeyCode dodgeLeft = KeyCode.A;
        [SerializeField] private KeyCode dodgeRight = KeyCode.D;
        [SerializeField] private KeyCode restart = KeyCode.R;
        [SerializeField] private KeyCode restartAlternative = KeyCode.Return;

        public KeyCode LeftPunch => leftPunch;
        public KeyCode LeftPunchAlternative => leftPunchAlternative;
        public KeyCode RightPunch => rightPunch;
        public KeyCode RightPunchAlternative => rightPunchAlternative;
        public KeyCode BothPunch => bothPunch;
        public KeyCode Duck => duck;
        public KeyCode DuckAlternative => duckAlternative;
        public KeyCode Jump => jump;
        public KeyCode JumpAlternative => jumpAlternative;
        public KeyCode DodgeLeft => dodgeLeft;
        public KeyCode DodgeRight => dodgeRight;
        public KeyCode Restart => restart;
        public KeyCode RestartAlternative => restartAlternative;
    }

    [Serializable]
    public sealed class CameraFeelSettings
    {
        [SerializeField, Range(1.8f, 3f)] private float standingHeight = 2.55f;
        [SerializeField, Min(1f)] private float poseSmoothing = 18f;
        [SerializeField] private float dodgeDistance = 1.95f;
        [SerializeField] private float duckDistance = 0.95f;
        [SerializeField] private float jumpDistance = 0.9f;
        [SerializeField, Min(0.1f)] private float punchDistance = 1.9f;
        [SerializeField, Range(0.1f, 0.6f)] private float punchDuration = 0.24f;
        [SerializeField, Range(0f, 0.2f)] private float punchShakeAmplitude = 0.045f;
        [SerializeField, Range(0.05f, 0.5f)] private float punchShakeDuration = 0.12f;
        [SerializeField, Range(0f, 0.3f)] private float bothPunchShakeAmplitude = 0.075f;
        [SerializeField, Range(0.05f, 0.5f)] private float bothPunchShakeDuration = 0.15f;
        [SerializeField, Range(0f, 0.15f)] private float rhythmTileShakeAmplitude = 0.025f;
        [SerializeField, Range(0.03f, 0.3f)] private float rhythmTileShakeDuration = 0.1f;
        [SerializeField, Range(0f, 0.4f)] private float failShakeAmplitude = 0.14f;
        [SerializeField, Range(0.05f, 1f)] private float failShakeDuration = 0.32f;

        public float StandingHeight => standingHeight;
        public float PoseSmoothing => poseSmoothing;
        public float DodgeDistance => dodgeDistance;
        public float DuckDistance => duckDistance;
        public float JumpDistance => jumpDistance;
        public float PunchDistance => punchDistance;
        public float PunchDuration => punchDuration;
        public float PunchShakeAmplitude => punchShakeAmplitude;
        public float PunchShakeDuration => punchShakeDuration;
        public float BothPunchShakeAmplitude => bothPunchShakeAmplitude;
        public float BothPunchShakeDuration => bothPunchShakeDuration;
        public float RhythmTileShakeAmplitude => rhythmTileShakeAmplitude;
        public float RhythmTileShakeDuration => rhythmTileShakeDuration;
        public float FailShakeAmplitude => failShakeAmplitude;
        public float FailShakeDuration => failShakeDuration;
    }

    [Serializable]
    public sealed class VisualSettings
    {
        [SerializeField] private Texture2D backgroundTexture;
        [SerializeField] private Color cyan = new Color(0.02f, 1f, 0.95f, 1f);
        [SerializeField] private Color magenta = new Color(1f, 0.03f, 0.72f, 1f);
        [SerializeField] private Color purple = new Color(0.48f, 0.05f, 1f, 1f);
        [SerializeField] private Color yellow = new Color(1f, 0.82f, 0.05f, 1f);
        [SerializeField] private Color obstacle = new Color(1f, 0.06f, 0.2f, 1f);
        [SerializeField, Range(0.5f, 6f)] private float neonIntensity = 2.2f;
        [SerializeField, Range(0f, 4f)] private float beatPulseIntensity = 1.4f;
        [SerializeField, Range(1f, 1.8f)] private float targetGlowScale = 1.26f;
        [SerializeField, Range(0.6f, 2.5f)] private float rhythmTileLength = 1.35f;
        [SerializeField, Range(0.05f, 0.6f)] private float judgementLinePulseStrength = 0.28f;
        [SerializeField, Range(0.05f, 0.4f)] private float screenFlashDuration = 0.12f;
        [SerializeField, Range(0.05f, 0.5f)] private float screenFlashIntensity = 0.18f;
        [SerializeField, Range(8, 64)] private int hitParticleCount = 34;
        [SerializeField, Range(8, 64)] private int travellerPoolCapacity = 24;
        [SerializeField, Range(8, 64)] private int rhythmTilePoolCapacity = 24;
        [SerializeField, Range(4, 24)] private int hitVfxPoolCapacity = 8;
        [SerializeField, Range(0f, 1f)] private float audioVolume = 0.58f;

        public Texture2D BackgroundTexture => backgroundTexture;
        public Color Cyan => cyan;
        public Color Magenta => magenta;
        public Color Purple => purple;
        public Color Yellow => yellow;
        public Color Obstacle => obstacle;
        public float NeonIntensity => neonIntensity;
        public float BeatPulseIntensity => beatPulseIntensity;
        public float TargetGlowScale => targetGlowScale;
        public float RhythmTileLength => rhythmTileLength;
        public float JudgementLinePulseStrength => judgementLinePulseStrength;
        public float ScreenFlashDuration => screenFlashDuration;
        public float ScreenFlashIntensity => screenFlashIntensity;
        public int HitParticleCount => hitParticleCount;
        public int TravellerPoolCapacity => travellerPoolCapacity;
        public int RhythmTilePoolCapacity => rhythmTilePoolCapacity;
        public int HitVfxPoolCapacity => hitVfxPoolCapacity;
        public float AudioVolume => audioVolume;
    }

    [CreateAssetMenu(fileName = "NeonPulseGameConfig", menuName = "Neon Pulse/Gameplay Configuration")]
    public sealed class NeonPulseGameConfig : ScriptableObject
    {
        [SerializeField] private CombatGameplayMode gameplayMode = CombatGameplayMode.Punch;
        [SerializeField] private bool autoPlay;
        [SerializeField] private RhythmSettings rhythm = new RhythmSettings();
        [SerializeField] private ScoreSettings scoring = new ScoreSettings();
        [SerializeField] private InputBindingSettings input = new InputBindingSettings();
        [SerializeField] private CameraFeelSettings cameraFeel = new CameraFeelSettings();
        [SerializeField] private VisualSettings visuals = new VisualSettings();
        [SerializeField] private NeonPulseLevelDefinition levelDefinition;
        [FormerlySerializedAs("beatmapEvents")]
        [SerializeField] private List<BeatmapEvent> punchEvents = new List<BeatmapEvent>(24);
        [SerializeField] private List<BeatmapEvent> obstacleEvents = new List<BeatmapEvent>(12);
        [SerializeField] private List<RhythmTileEvent> rhythmTileEvents = new List<RhythmTileEvent>(32);

        public CombatGameplayMode GameplayMode => gameplayMode;
        public bool AutoPlay => autoPlay;
        public RhythmSettings Rhythm => rhythm;
        public ScoreSettings Scoring => scoring;
        public InputBindingSettings Input => input;
        public CameraFeelSettings CameraFeel => cameraFeel;
        public VisualSettings Visuals => visuals;
        public NeonPulseLevelDefinition LevelDefinition => levelDefinition;
        public IReadOnlyList<BeatmapEvent> PunchEvents => punchEvents;
        public IReadOnlyList<BeatmapEvent> ObstacleEvents => obstacleEvents;
        public IReadOnlyList<RhythmTileEvent> RhythmTileEvents => rhythmTileEvents;

        private void OnEnable()
        {
            punchEvents = punchEvents ?? new List<BeatmapEvent>(24);
            obstacleEvents = obstacleEvents ?? new List<BeatmapEvent>(12);
            rhythmTileEvents = rhythmTileEvents ?? new List<RhythmTileEvent>(32);

            for (int index = punchEvents.Count - 1; index >= 0; index--)
            {
                BeatmapEvent chartEvent = punchEvents[index];
                if (!IsObstacleAction(chartEvent.Action))
                {
                    continue;
                }

                obstacleEvents.Add(chartEvent);
                punchEvents.RemoveAt(index);
            }

            if (rhythmTileEvents.Count == 0)
            {
                AddDefaultTiles();
            }

            SortBeatmap();
        }

        /// <summary>Loads the editable Resources asset, or creates a safe in-memory default.</summary>
        public static NeonPulseGameConfig LoadRuntime(out bool ownsInstance)
        {
            NeonPulseGameConfig loaded = Resources.Load<NeonPulseGameConfig>("NeonPulseGameConfig");
            if (loaded != null)
            {
                ownsInstance = false;
                return loaded;
            }

            NeonPulseGameConfig runtime = CreateInstance<NeonPulseGameConfig>();
            runtime.name = "Neon Pulse Runtime Defaults";
            runtime.ResetToDefaults();
            ownsInstance = true;
            return runtime;
        }

        /// <summary>Restores every setting and the sample chart to beginner-friendly defaults.</summary>
        public void ResetToDefaults()
        {
            gameplayMode = CombatGameplayMode.Punch;
            autoPlay = false;
            rhythm = new RhythmSettings();
            scoring = new ScoreSettings();
            input = new InputBindingSettings();
            cameraFeel = new CameraFeelSettings();
            visuals = new VisualSettings();
            ResetBeatmapToDefaults();
        }

        /// <summary>Restores only the sample chart without changing other gameplay settings.</summary>
        public void ResetBeatmapToDefaults()
        {
            punchEvents = punchEvents ?? new List<BeatmapEvent>(24);
            obstacleEvents = obstacleEvents ?? new List<BeatmapEvent>(12);
            rhythmTileEvents = rhythmTileEvents ?? new List<RhythmTileEvent>(32);
            punchEvents.Clear();
            obstacleEvents.Clear();
            rhythmTileEvents.Clear();
            AddDefaultChart();
        }

        /// <summary>Sorts chart events by beat while preserving their authored action and lane.</summary>
        public void SortBeatmap()
        {
            punchEvents.Sort(CompareEvents);
            obstacleEvents.Sort(CompareEvents);
            rhythmTileEvents.Sort(CompareTileEvents);
        }

        /// <summary>Checks the most common authoring mistakes and returns a Vietnamese status message.</summary>
        public bool ValidateConfiguration(out string message)
        {
            if (levelDefinition != null)
            {
                return levelDefinition.ValidateDefinition(out message);
            }

            if (rhythm.PerfectWindow > rhythm.GreatWindow || rhythm.GreatWindow > rhythm.GoodWindow)
            {
                message = "Timing phải theo thứ tự Perfect ≤ Great ≤ Good.";
                return false;
            }

            if (rhythm.SpawnZ <= rhythm.HitZ || rhythm.DespawnZ >= rhythm.HitZ)
            {
                message = "Vị trí phải theo thứ tự: Xuất hiện > Vạch đánh > Biến mất.";
                return false;
            }

            if (rhythm.TileWaveStartBeat < rhythm.TravelBeats ||
                rhythm.TileWaveStartBeat > rhythm.TileWaveEndBeat ||
                rhythm.TileWaveEndBeat >= rhythm.SongBeats)
            {
                message = "Đợt gạch phải nằm giữa bài và Beat bắt đầu phải đủ lớn hơn thời gian vật thể bay tới.";
                return false;
            }

            if (rhythm.HoldInputGrace > rhythm.HoldWindowLead)
            {
                message = "Thời gian phản ứng khi né không được lớn hơn thời gian cho phép giữ sớm.";
                return false;
            }

            if (punchEvents == null || punchEvents.Count == 0)
            {
                message = gameplayMode == CombatGameplayMode.Slash
                    ? "Danh sách mục tiêu chém đang trống."
                    : "Danh sách vòng đấm đang trống.";
                return false;
            }

            if (obstacleEvents == null || obstacleEvents.Count == 0)
            {
                message = "Danh sách cửa/chướng ngại đang trống.";
                return false;
            }

            if (rhythmTileEvents == null || rhythmTileEvents.Count == 0)
            {
                message = "Danh sách gạch nhịp đang trống.";
                return false;
            }

            if (!ValidateGameplayList(punchEvents, false, out message) ||
                !ValidateGameplayList(obstacleEvents, true, out message) ||
                !ValidateTileList(out message) ||
                !ValidateWaveSeparation(out message))
            {
                return false;
            }

            message = "Cấu hình hợp lệ và sẵn sàng Play.";
            return true;
        }

        private void AddDefaultChart()
        {
            AddPunch(4f, 0, GameplayAction.LeftPunch);
            AddPunch(6f, 3, GameplayAction.RightPunch);
            AddPunch(9f, 1, GameplayAction.LeftPunch);
            AddPunch(11f, 2, GameplayAction.RightPunch);
            AddPunch(14f, 1, GameplayAction.BothPunch);
            AddPunch(18f, 0, GameplayAction.LeftPunch);
            AddPunch(45f, 3, GameplayAction.RightPunch);
            AddPunch(47f, 1, GameplayAction.LeftPunch);
            AddPunch(50f, 2, GameplayAction.RightPunch);
            AddPunch(52f, 1, GameplayAction.BothPunch);
            AddPunch(58f, 0, GameplayAction.LeftPunch);
            AddPunch(60f, 3, GameplayAction.RightPunch);

            AddObstacle(16f, GameplayAction.Duck);
            AddObstacle(49f, GameplayAction.Jump);
            AddObstacle(55f, GameplayAction.DodgeRight);

            AddDefaultTiles();
        }

        private void AddDefaultTiles()
        {
            int[] beats = { 26, 28, 30, 34, 36, 38 };
            for (int index = 0; index < beats.Length; index++)
            {
                rhythmTileEvents.Add(new RhythmTileEvent(beats[index], 1, RhythmTileColor.Cyan));
                rhythmTileEvents.Add(new RhythmTileEvent(beats[index], 2, RhythmTileColor.Magenta));
            }
        }

        private void AddPunch(float beat, int lane, GameplayAction action)
        {
            punchEvents.Add(new BeatmapEvent(beat, lane, action));
        }

        private void AddObstacle(float beat, GameplayAction action)
        {
            obstacleEvents.Add(new BeatmapEvent(beat, 0, action));
        }

        private bool ValidateGameplayList(List<BeatmapEvent> events, bool expectObstacle, out string message)
        {
            float previousBeat = -1f;
            for (int index = 0; index < events.Count; index++)
            {
                BeatmapEvent chartEvent = events[index];
                bool isObstacle = IsObstacleAction(chartEvent.Action);
                bool validAction = expectObstacle
                    ? isObstacle
                    : chartEvent.Action == GameplayAction.LeftPunch || chartEvent.Action == GameplayAction.RightPunch ||
                      chartEvent.Action == GameplayAction.BothPunch;
                if (chartEvent.Beat < previousBeat || chartEvent.Beat < 0f || chartEvent.Beat >= rhythm.SongBeats ||
                    chartEvent.Lane < 0 || chartEvent.Lane > 3 || !validAction)
                {
                    message = expectObstacle
                        ? "Danh sách cửa có beat/lane/action không hợp lệ hoặc chưa sắp xếp."
                        : gameplayMode == CombatGameplayMode.Slash
                            ? "Danh sách mục tiêu chém có beat/lane/action không hợp lệ hoặc chưa sắp xếp."
                            : "Danh sách vòng đấm có beat/lane/action không hợp lệ hoặc chưa sắp xếp.";
                    return false;
                }

                previousBeat = chartEvent.Beat;
            }

            message = string.Empty;
            return true;
        }

        private bool ValidateTileList(out string message)
        {
            if ((rhythmTileEvents.Count & 1) != 0)
            {
                message = "Danh sách gạch phải có số lượng chẵn để tạo từng cặp chân.";
                return false;
            }

            float previousBeat = -1f;
            for (int index = 0; index < rhythmTileEvents.Count; index++)
            {
                RhythmTileEvent tileEvent = rhythmTileEvents[index];
                if (tileEvent.Beat < previousBeat || tileEvent.Beat < 0f || tileEvent.Beat >= rhythm.SongBeats ||
                    tileEvent.Lane < 0 || tileEvent.Lane > 3)
                {
                    message = "Danh sách gạch có beat/lane không hợp lệ hoặc chưa sắp xếp.";
                    return false;
                }

                previousBeat = tileEvent.Beat;
            }

            for (int index = 0; index < rhythmTileEvents.Count; index += 2)
            {
                RhythmTileEvent leftFoot = rhythmTileEvents[index];
                RhythmTileEvent rightFoot = rhythmTileEvents[index + 1];
                bool sameBeat = Mathf.Approximately(leftFoot.Beat, rightFoot.Beat);
                bool validFootLanes = leftFoot.Lane <= 1 && rightFoot.Lane >= 2;
                if (!sameBeat || !validFootLanes)
                {
                    message = "Mỗi cặp gạch phải cùng Beat: chân trái ở lane 0/1, chân phải ở lane 2/3.";
                    return false;
                }
            }

            message = string.Empty;
            return true;
        }

        private bool ValidateWaveSeparation(out string message)
        {
            for (int index = 0; index < rhythmTileEvents.Count; index++)
            {
                if (!rhythm.ContainsTileBeat(rhythmTileEvents[index].Beat))
                {
                    message = "Gạch chỉ được đặt trong khoảng Beat của đợt gạch giữa màn.";
                    return false;
                }
            }

            if (ContainsGameplayEventOverlappingTileWave(punchEvents) ||
                ContainsGameplayEventOverlappingTileWave(obstacleEvents))
            {
                message = "Vòng đấm/cửa đang chồng thời gian hiển thị với đợt gạch. Hãy chừa khoảng chuyển đợt.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private bool ContainsGameplayEventOverlappingTileWave(List<BeatmapEvent> events)
        {
            for (int index = 0; index < events.Count; index++)
            {
                if (rhythm.GameplayEventOverlapsTileWave(events[index].Beat))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareEvents(BeatmapEvent left, BeatmapEvent right)
        {
            return left.Beat.CompareTo(right.Beat);
        }

        private static int CompareTileEvents(RhythmTileEvent left, RhythmTileEvent right)
        {
            int beatComparison = left.Beat.CompareTo(right.Beat);
            return beatComparison != 0 ? beatComparison : left.Lane.CompareTo(right.Lane);
        }

        private static bool IsObstacleAction(GameplayAction action)
        {
            return action == GameplayAction.Duck || action == GameplayAction.Jump ||
                   action == GameplayAction.DodgeLeft || action == GameplayAction.DodgeRight;
        }
    }
}
