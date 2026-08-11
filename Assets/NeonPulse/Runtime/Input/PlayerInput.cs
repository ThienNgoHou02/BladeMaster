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
        public readonly bool OverheadClap;
        public readonly bool Duck;
        public readonly bool Jump;
        public readonly bool DodgeLeft;
        public readonly bool DodgeRight;
        public readonly bool LeftLegDrawUp;
        public readonly bool RightLegDrawUp;
        public readonly bool Restart;

        public PlayerInputFrame(
            bool leftPunch,
            bool rightPunch,
            bool bothPunch,
            bool overheadClap,
            bool duck,
            bool jump,
            bool dodgeLeft,
            bool dodgeRight,
            bool leftLegDrawUp,
            bool rightLegDrawUp,
            bool restart)
        {
            LeftPunch = leftPunch;
            RightPunch = rightPunch;
            BothPunch = bothPunch;
            OverheadClap = overheadClap;
            Duck = duck;
            Jump = jump;
            DodgeLeft = dodgeLeft;
            DodgeRight = dodgeRight;
            LeftLegDrawUp = leftLegDrawUp;
            RightLegDrawUp = rightLegDrawUp;
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
        private readonly InputBindingSettings bindings;

        /// <summary>Creates a keyboard provider from the editable gameplay bindings.</summary>
        public KeyboardInputProvider(InputBindingSettings inputBindings)
        {
            bindings = inputBindings ?? new InputBindingSettings();
        }

        /// <summary>Reads keyboard edges without allocating managed memory.</summary>
        public PlayerInputFrame ReadInput()
        {
            bool left = Input.GetKeyDown(bindings.LeftPunch) || Input.GetKeyDown(bindings.LeftPunchAlternative);
            bool right = Input.GetKeyDown(bindings.RightPunch) || Input.GetKeyDown(bindings.RightPunchAlternative);
            bool explicitBoth = Input.GetKeyDown(bindings.BothPunch);

            return new PlayerInputFrame(
                left,
                right,
                explicitBoth || (left && right),
                Input.GetKeyDown(bindings.OverheadClap),
                Input.GetKey(bindings.Duck) || Input.GetKey(bindings.DuckAlternative),
                Input.GetKey(bindings.Jump) || Input.GetKey(bindings.JumpAlternative),
                Input.GetKey(bindings.DodgeLeft),
                Input.GetKey(bindings.DodgeRight),
                Input.GetKey(bindings.LeftLegDrawUp),
                Input.GetKey(bindings.RightLegDrawUp),
                Input.GetKeyDown(bindings.Restart) || Input.GetKeyDown(bindings.RestartAlternative));
        }
    }
}
