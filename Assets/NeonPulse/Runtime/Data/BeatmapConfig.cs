using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeonPulse
{
    public enum GameplayAction
    {
        LeftPunch,
        RightPunch,
        BothPunch,
        Duck,
        Jump,
        DodgeLeft,
        DodgeRight
    }

    [Serializable]
    public struct BeatmapEvent
    {
        [Min(0f)] public float Beat;
        [Range(0, 3)] public int Lane;
        public GameplayAction Action;

        public BeatmapEvent(float beat, int lane, GameplayAction action)
        {
            Beat = beat;
            Lane = lane;
            Action = action;
        }
    }

    [CreateAssetMenu(fileName = "Beatmap", menuName = "Neon Pulse/Beatmap")]
    public sealed class BeatmapConfig : ScriptableObject
    {
        [Header("Timing")]
        [SerializeField, Min(60f)] private float bpm = 105f;
        [SerializeField, Min(8)] private int songBeats = 64;

        [Header("Chart")]
        [SerializeField] private List<BeatmapEvent> events = new List<BeatmapEvent>(48);

        public float Bpm => bpm;
        public int SongBeats => songBeats;
        public float SecondsPerBeat => 60f / bpm;
        public IReadOnlyList<BeatmapEvent> Events => events;

        /// <summary>Creates the dependency-free sample chart used when no authored asset is assigned.</summary>
        public static BeatmapConfig CreateRuntimeSample()
        {
            BeatmapConfig map = CreateInstance<BeatmapConfig>();
            map.name = "Neon Pulse Runtime Sample";
            map.bpm = 105f;
            map.songBeats = 64;

            map.Add(4f, 0, GameplayAction.LeftPunch);
            map.Add(6f, 3, GameplayAction.RightPunch);
            map.Add(8f, 1, GameplayAction.LeftPunch);
            map.Add(10f, 2, GameplayAction.RightPunch);
            map.Add(12f, 1, GameplayAction.BothPunch);
            map.Add(15f, 0, GameplayAction.Duck);
            map.Add(18f, 0, GameplayAction.LeftPunch);
            map.Add(20f, 3, GameplayAction.RightPunch);
            map.Add(22f, 0, GameplayAction.Jump);
            map.Add(25f, 1, GameplayAction.LeftPunch);
            map.Add(27f, 2, GameplayAction.RightPunch);
            map.Add(29f, 1, GameplayAction.BothPunch);
            map.Add(32f, 0, GameplayAction.DodgeLeft);
            map.Add(35f, 0, GameplayAction.LeftPunch);
            map.Add(37f, 3, GameplayAction.RightPunch);
            map.Add(39f, 0, GameplayAction.Duck);
            map.Add(42f, 1, GameplayAction.BothPunch);
            map.Add(45f, 0, GameplayAction.Jump);
            map.Add(48f, 1, GameplayAction.LeftPunch);
            map.Add(49f, 2, GameplayAction.RightPunch);
            map.Add(51f, 1, GameplayAction.BothPunch);
            map.Add(54f, 3, GameplayAction.DodgeRight);
            map.Add(57f, 0, GameplayAction.LeftPunch);
            map.Add(58f, 3, GameplayAction.RightPunch);
            map.Add(60f, 1, GameplayAction.BothPunch);
            return map;
        }

        private void Add(float beat, int lane, GameplayAction action)
        {
            events.Add(new BeatmapEvent(beat, lane, action));
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            bpm = Mathf.Max(60f, bpm);
            songBeats = Mathf.Max(8, songBeats);
            events.Sort((left, right) => left.Beat.CompareTo(right.Beat));
        }
#endif
    }
}
