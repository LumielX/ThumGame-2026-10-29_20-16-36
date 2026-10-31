using System;
using System.Numerics;

namespace SpiritMovers.Core.Ghost
{
    public enum NoiseSource
    {
        Footstep,
        ItemImpact,
        Voice,
        Radio,
    }

    /// <summary>A sound the ghost may hear (TECHNICAL_DESIGN 5): where, how loud (0..1) and what made it.</summary>
    public readonly struct NoiseEvent
    {
        public Vector3 Position { get; }
        public float Loudness { get; }
        public NoiseSource Source { get; }

        public NoiseEvent(Vector3 position, float loudness, NoiseSource source)
        {
            Position = position;
            Loudness = Math.Clamp(loudness, 0f, 1f);
            Source = source;
        }
    }

    /// <summary>Tuning for noise (⚙ defaults in the M3D plan); footstep loudness lives in MovementConfig.</summary>
    public sealed class NoiseConfig
    {
        public float FootstepInterval { get; set; } = 0.5f;
        /// <summary>Items hitting something slower than this make no noise.</summary>
        public float ImpactMinSpeed { get; set; } = 2f;
        /// <summary>Impact speed that counts as fully loud.</summary>
        public float ImpactFullSpeed { get; set; } = 6f;
        /// <summary>At or above this a noise annoys the ghost.</summary>
        public float LoudThreshold { get; set; } = 0.8f;
        public float LoudCooldown { get; set; } = 5f;
        /// <summary>Below this a noise still counts as silence (crouch-walking is 0.05).</summary>
        public float SilenceLoudness { get; set; } = 0.1f;

        public float ImpactLoudness(float speed) =>
            speed < ImpactMinSpeed ? 0f : Math.Min(1f, speed / ImpactFullSpeed);
    }

    /// <summary>Turns loud noises into at most one anger event per cooldown, so running is not punished per step.</summary>
    public sealed class LoudNoiseGate
    {
        private readonly NoiseConfig m_Config;
        private float m_NextAt = float.MinValue;

        public LoudNoiseGate(NoiseConfig config) => m_Config = config;

        public bool TryAccept(NoiseEvent e, float now)
        {
            if (e.Loudness < m_Config.LoudThreshold || now < m_NextAt)
                return false;
            m_NextAt = now + m_Config.LoudCooldown;
            return true;
        }
    }
}
