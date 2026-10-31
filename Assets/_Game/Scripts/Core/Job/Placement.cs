using System;
using System.Numerics;

namespace SpiritMovers.Core.Job
{
    /// <summary>Where an item is: the room it is in plus its pose in house space.</summary>
    public readonly struct Placement
    {
        public string RoomId { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }

        public Placement(string roomId, Vector3 position, Quaternion rotation)
        {
            RoomId = roomId;
            Position = position;
            Rotation = rotation;
        }
    }

    /// <summary>How far an item may be from its remembered pose and still count as correct (GDD 5.1 ⚙).</summary>
    public sealed class Tolerance
    {
        public float PositionMeters { get; set; } = 0.25f;
        public float AngleDegrees { get; set; } = 20f;

        /// <summary>A fresh copy, so changing one slot's tolerance never changes another's.</summary>
        public static Tolerance Default => new();
    }

    public static class PlacementMath
    {
        /// <summary>Smallest angle between two orientations; q and -q count as the same orientation.</summary>
        public static float AngleDegrees(Quaternion a, Quaternion b)
        {
            var dot = MathF.Abs(Quaternion.Dot(Safe(a), Safe(b)));
            return 2f * MathF.Acos(MathF.Min(1f, dot)) * 180f / MathF.PI;
        }

        /// <summary>A zero quaternion (unset rotation) means "no rotation" instead of NaN.</summary>
        private static Quaternion Safe(Quaternion q) =>
            q.LengthSquared() < 1e-12f ? Quaternion.Identity : Quaternion.Normalize(q);

        public static bool IsCorrect(Placement target, Placement actual, Tolerance tolerance) =>
            target.RoomId == actual.RoomId &&
            Vector3.Distance(target.Position, actual.Position) <= tolerance.PositionMeters &&
            AngleDegrees(target.Rotation, actual.Rotation) <= tolerance.AngleDegrees;
    }
}
