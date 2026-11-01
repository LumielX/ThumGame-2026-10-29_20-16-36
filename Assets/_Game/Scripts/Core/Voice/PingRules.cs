using System.Collections.Generic;

namespace SpiritMovers.Core.Voice
{
    /// <summary>
    /// Host: how often each player may ping (⚙ M4C plan) — a ping lasts 6 s, at most 3 up at once per player, and at
    /// least 0.5 s between two pings.
    /// </summary>
    public sealed class PingRules
    {
        private readonly float m_Lifetime;
        private readonly float m_Cooldown;
        private readonly int m_MaxActive;
        private readonly Dictionary<ulong, List<float>> m_Sent = new();

        public PingRules(float lifetime = 6f, float cooldown = 0.5f, int maxActive = 3)
        {
            m_Lifetime = lifetime;
            m_Cooldown = cooldown;
            m_MaxActive = maxActive;
        }

        public float Lifetime => m_Lifetime;

        public bool TryAdd(ulong sender, float now)
        {
            var sent = Active(sender, now);
            if (sent.Count >= m_MaxActive || (sent.Count > 0 && now - sent[sent.Count - 1] < m_Cooldown))
                return false;
            sent.Add(now);
            return true;
        }

        public int ActiveFor(ulong sender, float now) => Active(sender, now).Count;

        private List<float> Active(ulong sender, float now)
        {
            if (!m_Sent.TryGetValue(sender, out var sent))
                m_Sent[sender] = sent = new List<float>();
            sent.RemoveAll(at => now - at >= m_Lifetime);
            return sent;
        }
    }
}
