namespace SpiritMovers.Core.Voice
{
    /// <summary>
    /// The sharp-eared ghost hears a loud microphone (TECHNICAL_DESIGN 8.3, D-010): only when the player allows it and
    /// is actually transmitting, only above the threshold ⚙, and at most 4 reports a second.
    /// </summary>
    public sealed class MicNoiseGate
    {
        private readonly double m_Threshold;
        private readonly float m_MinInterval;
        private float m_NextAt = float.MinValue;

        // The threshold is a double like Vivox's AudioEnergy: 0.6f as a double is above 0.6.
        public MicNoiseGate(double threshold = 0.6, float minInterval = 0.25f)
        {
            m_Threshold = threshold;
            m_MinInterval = minInterval;
        }

        public bool TryReport(bool enabled, bool transmitting, double energy, float now)
        {
            if (!enabled || !transmitting || energy < m_Threshold || now < m_NextAt)
                return false;
            m_NextAt = now + m_MinInterval;
            return true;
        }
    }
}
