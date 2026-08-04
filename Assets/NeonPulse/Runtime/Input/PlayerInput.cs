using UnityEngine;

namespace NeonPulse
{
    /// <summary>
    /// Snapshot of punch input edges and continuously held movement actions.
    /// A webcam pose provider can produce the same data without changing gameplay code.
    /// </summary>
    public readonly struct PlayerInputFrame
    {
        public readonly bool LeftPunch;
        public readonly bool RightPunch;
        public readonly bool BothPunch;
        public readonly bool Duck;
        public readonly bool Jump;
        public readonly bool DodgeLeft;
        public readonly bool DodgeRight;
        public readonly bool Restart;

        public PlayerInputFrame(
            bool leftPunch,
            bool rightPunch,
            bool bothPunch,
            bool duck,
            bool jump,
            bool dodgeLeft,
            bool dodgeRight,
            bool restart)
        {
            LeftPunch = leftPunch;
            RightPunch = rightPunch;
            BothPunch = bothPunch;
            Duck = duck;
            Jump = jump;
            DodgeLeft = dodgeLeft;
            DodgeRight = dodgeRight;
            Restart = restart;
        }
    }

    /// <summary>Supplies one allocation-free gameplay input snapshot per frame.</summary>
    public interface IPlayerInputProvider
    {
        PlayerInputFrame ReadInput();
    }

    /// <summary>Desktop MVP input implementation using Unity's built-in input manager.</summary>
    public sealed class KeyboardInputProvider : IPlayerInputProvider
    {
        /// <summary>Reads keyboard edges without allocating managed memory.</summary>
        public PlayerInputFrame ReadInput()
        {
            bool left = Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.LeftArrow);
            bool right = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.RightArrow);
            bool explicitBoth = Input.GetKeyDown(KeyCode.F);

            return new PlayerInputFrame(
                left,
                right,
                explicitBoth || (left && right),
                Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow),
                Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.W),
                Input.GetKey(KeyCode.A),
                Input.GetKey(KeyCode.D),
                Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Return));
        }
    }
}
