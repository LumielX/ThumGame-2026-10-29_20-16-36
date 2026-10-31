using UnityEngine;

namespace SpiritMovers.Player
{
    /// <summary>One frame of player input, from the keyboard or from a test/bot script.</summary>
    public struct PlayerInputState
    {
        public Vector2 Move;
        public Vector2 Look;
        public bool Sprint;
        public bool Crouch;
        public bool JumpPressed;
        public bool InteractPressed;
        public bool ThrowPressed;
        /// <summary>Scroll notches this frame; positive turns the held item clockwise.</summary>
        public float RotateSteps;
    }

    public interface IPlayerInputSource
    {
        PlayerInputState Read();
    }

    /// <summary>Input set from code (tests, bots, headless runs). Held values persist; presses fire once.</summary>
    public sealed class ScriptedInput : IPlayerInputSource
    {
        public PlayerInputState Next;

        public PlayerInputState Read()
        {
            var state = Next;
            Next.JumpPressed = false;
            Next.InteractPressed = false;
            Next.ThrowPressed = false;
            Next.RotateSteps = 0f;
            return state;
        }
    }
}
