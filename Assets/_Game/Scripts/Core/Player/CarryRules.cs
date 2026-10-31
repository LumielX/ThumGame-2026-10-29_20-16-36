using System;
using System.Collections.Generic;
using System.Numerics;

namespace SpiritMovers.Core.Player
{
    /// <summary>⚙ carrying tuning; defaults from the M3B plan.</summary>
    public sealed class CarryConfig
    {
        public float HeavyMass { get; set; } = 15f;
        public float ReachMeters { get; set; } = 2.5f;
        public float LightHoldForward { get; set; } = 1.2f;
        public float LightHoldHeight { get; set; } = 1.3f;
        public float HeavyHoldForward { get; set; } = 1.0f;
        public float HeavyHoldHeight { get; set; } = 0.9f;
        public float FollowGain { get; set; } = 12f;
        public float MaxFollowSpeed { get; set; } = 10f;
        public float MaxHoldError { get; set; } = 2.0f;
        public float HoldErrorGraceSeconds { get; set; } = 0.5f;
        public float ThrowSpeed { get; set; } = 6f;
        public float RotateStepDegrees { get; set; } = 15f;
        public int MaxCarriers { get; set; } = 2;
        /// <summary>Hold-error checks pause this long after an item changes owner (the host's view lags a round trip).</summary>
        public float HandoffGraceSeconds { get; set; } = 1f;

        public static CarryConfig Default => new();
    }

    /// <summary>Who can carry what, and where it sits (D-015, D-034, GDD 7–8).</summary>
    public static class CarryRules
    {
        public static bool IsHeavy(float mass, CarryConfig c) => mass >= c.HeavyMass;

        public static CarryLoad LoadFor(float mass, int carriers, CarryConfig c) =>
            carriers <= 0 ? CarryLoad.None
            : !IsHeavy(mass, c) ? CarryLoad.Light
            : carriers == 1 ? CarryLoad.Heavy
            : CarryLoad.HeavyShared;

        public static int MaxCarriersFor(float mass, CarryConfig c) => IsHeavy(mass, c) ? c.MaxCarriers : 1;

        public static bool CanThrow(float mass, int carriers, CarryConfig c) => !IsHeavy(mass, c) && carriers == 1;

        /// <summary>
        /// In front of the feet along the player's heading. Pitch is ignored so looking up or down
        /// never pushes the item into the floor or ceiling, and every peer computes the same point.
        /// </summary>
        public static Vector3 HoldPoint(Vector3 feet, Vector3 forward, float mass, CarryConfig c)
        {
            var flat = new Vector3(forward.X, 0f, forward.Z);
            flat = flat.LengthSquared() > 1e-8f ? Vector3.Normalize(flat) : Vector3.UnitZ;
            var heavy = IsHeavy(mass, c);
            return feet + flat * (heavy ? c.HeavyHoldForward : c.LightHoldForward)
                        + Vector3.UnitY * (heavy ? c.HeavyHoldHeight : c.LightHoldHeight);
        }

        public static Vector3 SharedHoldPoint(IReadOnlyList<Vector3> holdPoints)
        {
            if (holdPoints.Count == 0)
                throw new ArgumentException("A shared carry needs at least one hold point.", nameof(holdPoints));
            var sum = Vector3.Zero;
            foreach (var point in holdPoints)
                sum += point;
            return sum / holdPoints.Count;
        }

        /// <summary>Velocity that pulls the item to the hand; capped so a snag never flings it.</summary>
        public static Vector3 FollowVelocity(Vector3 holdPoint, Vector3 itemPosition, CarryConfig c)
        {
            var velocity = (holdPoint - itemPosition) * c.FollowGain;
            var speed = velocity.Length();
            return speed > c.MaxFollowSpeed ? velocity * (c.MaxFollowSpeed / speed) : velocity;
        }
    }

    /// <summary>Tells the host to drop an item that has stayed far from its carrier's hand (stuck or glitched).</summary>
    public sealed class HoldErrorWatch
    {
        private readonly CarryConfig m_Config;
        private float m_FarSeconds;
        private float m_SuspendedSeconds;

        public HoldErrorWatch(CarryConfig config) => m_Config = config;

        /// <summary>
        /// Ignore distance for a while, e.g. after an ownership handoff when the host's view of the item
        /// lags the carrier by a round trip.
        /// </summary>
        public void Suspend(float seconds)
        {
            m_SuspendedSeconds = MathF.Max(m_SuspendedSeconds, seconds);
            m_FarSeconds = 0f;
        }

        /// <returns>true once the item has been out of range longer than the grace period.</returns>
        public bool Update(float distanceFromHold, float dt)
        {
            if (m_SuspendedSeconds > 0f)
            {
                var skipped = MathF.Min(dt, m_SuspendedSeconds);
                m_SuspendedSeconds -= skipped;
                dt -= skipped;
                if (dt <= 0f)
                    return false;
            }

            if (distanceFromHold <= m_Config.MaxHoldError)
            {
                m_FarSeconds = 0f;
                return false;
            }
            m_FarSeconds += dt;
            return m_FarSeconds > m_Config.HoldErrorGraceSeconds;
        }

        public void Reset()
        {
            m_FarSeconds = 0f;
            m_SuspendedSeconds = 0f;
        }
    }
}
