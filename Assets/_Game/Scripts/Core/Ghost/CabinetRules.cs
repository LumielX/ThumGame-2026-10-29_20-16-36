namespace SpiritMovers.Core.Ghost
{
    /// <summary>The memory cabinet a caught player waits in (GDD 6.3, D-011).</summary>
    public static class CabinetRules
    {
        /// <summary>How long until the player gets out alone (and pays for it).</summary>
        public static float ReleaseSeconds(bool solo, GhostConfig c) => solo ? c.SoloCabinetSeconds : c.CabinetSeconds;

        /// <summary>A friend can open the cabinet once one more item in its room is right, or the spirit camera is aimed at it.</summary>
        public static bool CanFriendOpen(int correctAtCapture, int correctNow, bool cameraAimed) =>
            cameraAimed || correctNow > correctAtCapture;
    }
}
