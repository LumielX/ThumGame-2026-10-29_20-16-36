using System;

namespace SpiritMovers.Core.Player
{
    /// <summary>Seconds of running left (D-015). Drains 1/s while running; refills after a short rest.</summary>
    public sealed class Stamina
    {
        private readonly float m_Max;
        private readonly float m_RegenPerSecond;
        private readonly float m_RegenDelay;
        private readonly float m_MinToStart;
        private float m_SinceRun;

        public float Current { get; private set; }

        public Stamina(float max = 5f, float regenPerSecond = 1f, float regenDelay = 1f, float minToStart = 0.5f)
        {
            m_Max = max;
            m_RegenPerSecond = regenPerSecond;
            m_RegenDelay = regenDelay;
            m_MinToStart = minToStart;
            Current = max;
        }

        /// <summary>A run in progress continues until empty; a new run needs a little in reserve.</summary>
        public bool AllowsRun(bool wasRunning) => wasRunning ? Current > 0f : Current >= m_MinToStart;

        public void Tick(float dt, bool running)
        {
            if (running)
            {
                m_SinceRun = 0f;
                Current = MathF.Max(0f, Current - dt);
                return;
            }

            // Only time past the rest delay refills.
            var before = m_SinceRun;
            m_SinceRun += dt;
            var resting = m_SinceRun - MathF.Max(before, m_RegenDelay);
            if (resting > 0f)
                Current = MathF.Min(m_Max, Current + resting * m_RegenPerSecond);
        }
    }
}
