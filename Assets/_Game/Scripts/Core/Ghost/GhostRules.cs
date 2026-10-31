using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SpiritMovers.Core.Ghost
{
    /// <summary>What the ghost does at each anger stage (TECHNICAL_DESIGN 5, GDD 6.2); dragging is part of Hunt.</summary>
    public enum GhostMode
    {
        Idle,
        Prank,
        Disturb,
        Hunt,
        Evict,
    }

    /// <summary>A player's eyes as the host sees them: where they are and where they look.</summary>
    public readonly struct Viewer
    {
        public ulong ClientId { get; }
        public Vector3 Eye { get; }
        public Vector3 Look { get; }

        public Viewer(ulong clientId, Vector3 eye, Vector3 look)
        {
            ClientId = clientId;
            Eye = eye;
            Look = look;
        }
    }

    /// <summary>A player the ghost could go after.</summary>
    public readonly struct Target
    {
        public ulong ClientId { get; }
        public Vector3 Feet { get; }
        public bool Captured { get; }
        public float ImmuneUntil { get; }

        public Target(ulong clientId, Vector3 feet, bool captured, float immuneUntil)
        {
            ClientId = clientId;
            Feet = feet;
            Captured = captured;
            ImmuneUntil = immuneUntil;
        }
    }

    /// <summary>A planned item the ghost could prank with.</summary>
    public readonly struct PrankCandidate
    {
        public string SlotId { get; }
        public string RoomId { get; }
        public bool Carried { get; }
        public bool RoomAccepted { get; }

        public PrankCandidate(string slotId, string roomId, bool carried, bool roomAccepted)
        {
            SlotId = slotId;
            RoomId = roomId;
            Carried = carried;
            RoomAccepted = roomAccepted;
        }
    }

    /// <summary>Tuning for the shy ghost (⚙ defaults in the M3D plan).</summary>
    public sealed class GhostConfig
    {
        /// <summary>Half-angle of the cone in which a player counts as looking at the ghost.</summary>
        public float WatchHalfAngle { get; set; } = 40f;
        public float WatchRange { get; set; } = 25f;
        public float ThinkInterval { get; set; } = 0.1f;
        public float FloatHeight { get; set; } = 1.2f;
        public float IdleSpeed { get; set; } = 1.0f;
        /// <summary>When calm the ghost drifts toward the nearest player but stops this far away.</summary>
        public float IdleKeepAway { get; set; } = 4f;
        public float HuntSpeed { get; set; } = 3.0f;
        public float CatchDistance { get; set; } = 1.2f;
        public float PrankCooldown { get; set; } = 20f;
        public float DisturbCooldown { get; set; } = 15f;
        public float LightsOutSeconds { get; set; } = 3f;
        public float LightsOutFactor { get; set; } = 0.15f;
        public float PushSpeed { get; set; } = 2f;
        public float CabinetSeconds { get; set; } = 45f;
        public float SoloCabinetSeconds { get; set; } = 20f;
        /// <summary>After leaving a cabinet the ghost leaves the player alone this long.</summary>
        public float ImmuneSeconds { get; set; } = 5f;

        /// <summary>Problems in hand-edited tuning; empty when usable.</summary>
        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();
            if (WatchHalfAngle <= 0f || WatchHalfAngle >= 90f)
                problems.Add($"watch half-angle {WatchHalfAngle} must be between 0 and 90");
            if (WatchRange <= 0f || ThinkInterval <= 0f)
                problems.Add("watch range and think interval must be positive");
            if (IdleSpeed < 0f || HuntSpeed < 0f || PushSpeed < 0f)
                problems.Add("negative speed");
            if (CatchDistance <= 0f || CabinetSeconds <= 0f || SoloCabinetSeconds <= 0f)
                problems.Add("catch distance and cabinet times must be positive");
            if (PrankCooldown < 0f || DisturbCooldown < 0f || LightsOutSeconds < 0f || ImmuneSeconds < 0f)
                problems.Add("negative time");
            return problems;
        }
    }

    /// <summary>Decisions of the shy ghost (GDD 6.4 "ขี้อาย"); the host feeds in what it sees.</summary>
    public static class GhostRules
    {
        public static GhostMode ModeFor(int angerStage) => (GhostMode)Math.Clamp(angerStage, 0, (int)GhostMode.Evict);

        /// <summary>
        /// True when any player looks at the ghost: inside their view cone and range, with nothing in between
        /// (<paramref name="clearLine"/> answers for the line from eye to ghost).
        /// </summary>
        public static bool IsWatched(Vector3 ghost, IEnumerable<Viewer> viewers, GhostConfig c, Func<Vector3, Vector3, bool> clearLine)
        {
            var minCos = MathF.Cos(c.WatchHalfAngle * MathF.PI / 180f);
            foreach (var viewer in viewers)
            {
                var toGhost = ghost - viewer.Eye;
                var distance = toGhost.Length();
                if (distance > c.WatchRange)
                    continue;
                if (distance > 1e-4f && viewer.Look.LengthSquared() > 1e-8f &&
                    Vector3.Dot(toGhost / distance, Vector3.Normalize(viewer.Look)) < minCos)
                    continue;
                if (clearLine(viewer.Eye, ghost))
                    return true;
            }
            return false;
        }

        /// <summary>The nearest player not in a cabinet and no longer immune, or null.</summary>
        public static Target? PickTarget(Vector3 ghost, IReadOnlyList<Target> players, float now)
        {
            Target? best = null;
            var bestDistance = float.MaxValue;
            foreach (var player in players)
            {
                if (player.Captured || now < player.ImmuneUntil)
                    continue;
                var distance = Vector3.DistanceSquared(ghost, player.Feet);
                if (distance < bestDistance)
                {
                    best = player;
                    bestDistance = distance;
                }
            }
            return best;
        }

        /// <summary>
        /// Two different items in the ghost's room to swap, never one being carried or in a room the ghost has
        /// already accepted (pranks must not undo finished work, GDD 7); null when there are not two.
        /// </summary>
        public static (string A, string B)? PickPrank(IReadOnlyList<PrankCandidate> items, string ghostRoom, Random rng)
        {
            var free = Free(items, ghostRoom);
            if (free.Count < 2)
                return null;
            var a = rng.Next(free.Count);
            var b = rng.Next(free.Count - 1);
            if (b >= a)
                b++;
            return (free[a], free[b]);
        }

        /// <summary>One item in the ghost's room to push, by the same rules as <see cref="PickPrank"/>; null if none.</summary>
        public static string PickPush(IReadOnlyList<PrankCandidate> items, string ghostRoom, Random rng)
        {
            var free = Free(items, ghostRoom);
            return free.Count == 0 ? null : free[rng.Next(free.Count)];
        }

        private static List<string> Free(IReadOnlyList<PrankCandidate> items, string ghostRoom) =>
            items.Where(i => i.RoomId == ghostRoom && !i.Carried && !i.RoomAccepted).Select(i => i.SlotId).ToList();
    }
}
