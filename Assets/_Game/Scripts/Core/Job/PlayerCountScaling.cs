using System;

namespace SpiritMovers.Core.Job
{
    /// <summary>Job size and anger rate by team size 1–6 (D-011 ⚙; defaults from the M3A plan).</summary>
    public static class PlayerCountScaling
    {
        //                                      players: 1      2     3     4     5     6
        private static readonly int[] s_ItemOffset = { -1, 0, 0, 1, 1, 1 };
        private static readonly float[] s_AngerMultiplier = { 0.8f, 1f, 1f, 1.1f, 1.2f, 1.2f };

        public static int ItemOffset(int players) => s_ItemOffset[Index(players)];

        public static float AngerMultiplier(int players) => s_AngerMultiplier[Index(players)];

        private static int Index(int players) => Math.Clamp(players, 1, 6) - 1;
    }
}
