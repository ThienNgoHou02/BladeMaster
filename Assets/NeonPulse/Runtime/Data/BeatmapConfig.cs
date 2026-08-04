using System;
using UnityEngine;

namespace NeonPulse
{
    public enum CombatGameplayMode
    {
        Punch,
        Slash
    }

    public enum GameplayAction
    {
        LeftPunch,
        RightPunch,
        BothPunch,
        Duck,
        Jump,
        DodgeLeft,
        DodgeRight,
        RhythmTile
    }

    public enum RhythmTileColor
    {
        Cyan,
        Magenta,
        Yellow,
        Purple
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

    [Serializable]
    public struct RhythmTileEvent
    {
        [Min(0f)] public float Beat;
        [Range(0, 3)] public int Lane;
        public RhythmTileColor Color;

        public RhythmTileEvent(float beat, int lane, RhythmTileColor color)
        {
            Beat = beat;
            Lane = lane;
            Color = color;
        }
    }
}
