namespace SpiritMovers.Core.Job
{
    /// <summary>Builds per-job rule objects so callers cannot forget a factor (M3A carry-over 1).</summary>
    public static class JobSetup
    {
        /// <summary>How fast the ghost gets angry: difficulty × team size (GDD 4.4, D-011).</summary>
        public static float AngerMultiplier(JobDifficulty difficulty, int players) =>
            difficulty.AngerMultiplier * PlayerCountScaling.AngerMultiplier(players);

        public static AngerModel CreateAnger(AngerConfig config, JobDifficulty difficulty, int players) =>
            new(config, AngerMultiplier(difficulty, players));
    }
}
