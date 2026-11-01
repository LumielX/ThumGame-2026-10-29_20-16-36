using UnityEngine;

namespace SpiritMovers.Player
{
    /// <summary>
    /// The one place the mouse is captured for looking around or freed for menus and the results screen, so no screen
    /// can leave it in the wrong state for the next one (M4A final review).
    /// </summary>
    public static class GameCursor
    {
        /// <summary>What the game last asked for; tracked in batch mode too, where the real cursor is left alone.</summary>
        public static bool Captured { get; private set; }

        public static void Capture(bool captured)
        {
            Captured = captured;
            if (Application.isBatchMode)
                return;
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }
    }
}
