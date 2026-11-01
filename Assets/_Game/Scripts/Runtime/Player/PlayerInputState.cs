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
        /// <summary>Holding up the work order (Tab).</summary>
        public bool JobSheetHeld;
        /// <summary>The "wai" emote (G).</summary>
        public bool WaiPressed;
        /// <summary>Use the held tool (right mouse button).</summary>
        public bool UsePressed;
        /// <summary>Use kept down (talking on a walkie-talkie).</summary>
        public bool UseHeld;
        /// <summary>Lobby: toggle ready (R).</summary>
        public bool ReadyPressed;
        /// <summary>Confirm / go now (Enter) — the host departs or leaves the results early.</summary>
        public bool ConfirmPressed;
        /// <summary>Open / close the pause menu (Esc).</summary>
        public bool PausePressed;
        /// <summary>Open the chat field (M4C).</summary>
        public bool ChatPressed;
        /// <summary>Mark where we look (M4C).</summary>
        public bool PingPressed;
        /// <summary>The quick-message wheel is held open (M4C).</summary>
        public bool QuickWheelHeld;
        /// <summary>A quick-message number key, 1–6 (0 = none).</summary>
        public int QuickPressed;
        /// <summary>Push-to-talk held (voice chat, M4C).</summary>
        public bool TalkHeld;
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
            Next.Look = Vector2.zero; // a per-frame delta, not a held value
            Next.JumpPressed = false;
            Next.InteractPressed = false;
            Next.ThrowPressed = false;
            Next.RotateSteps = 0f;
            Next.WaiPressed = false;
            Next.UsePressed = false;
            Next.ReadyPressed = false;
            Next.ConfirmPressed = false;
            Next.PausePressed = false;
            Next.ChatPressed = false;
            Next.PingPressed = false;
            Next.QuickPressed = 0;
            return state;
        }
    }
}
