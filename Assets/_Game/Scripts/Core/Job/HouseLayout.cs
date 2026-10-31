using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SpiritMovers.Core.Job
{
    /// <summary>How an item was disturbed when the house was "unpacked" (GDD 5.3).</summary>
    public enum ProblemType
    {
        Moved,
        Hidden,
        /// <summary>Something else must be moved first to reach or place it.</summary>
        Order,
        /// <summary>Haunted: must be calmed before it can be carried.</summary>
        Possessed,
    }

    /// <summary>A memory slot: one item and where the ghost remembers it (GDD 5.1).</summary>
    public sealed class SlotDefinition
    {
        public string Id { get; }
        public string ItemId { get; }
        public Placement Target { get; }
        public Tolerance Tolerance { get; }
        public IReadOnlyList<ProblemType> Supports { get; }

        public SlotDefinition(string id, string itemId, Placement target, Tolerance tolerance, IReadOnlyList<ProblemType> supports)
        {
            Id = id;
            ItemId = itemId;
            Target = target;
            Tolerance = tolerance;
            Supports = supports;
        }
    }

    public sealed class RoomLayout
    {
        public string Id { get; }
        public IReadOnlyList<SlotDefinition> Slots { get; }

        public RoomLayout(string id, IReadOnlyList<SlotDefinition> slots)
        {
            Id = id;
            Slots = slots;
        }
    }

    /// <summary>Engine-free description of a house; built from its HouseDefinition asset at runtime.</summary>
    public sealed class HouseLayout
    {
        public string Id { get; }
        public IReadOnlyList<RoomLayout> Rooms { get; }
        /// <summary>Where moved items may be dropped when the house is unpacked.</summary>
        public IReadOnlyList<Placement> ScatterPoints { get; }
        /// <summary>Drawers, under beds, cupboards: where hidden items go.</summary>
        public IReadOnlyList<Placement> HidingSpots { get; }

        public HouseLayout(string id, IReadOnlyList<RoomLayout> rooms, IReadOnlyList<Placement> scatterPoints, IReadOnlyList<Placement> hidingSpots)
        {
            Id = id;
            Rooms = rooms;
            ScatterPoints = scatterPoints;
            HidingSpots = hidingSpots;
        }

        /// <summary>
        /// 64-bit FNV-1a of everything that shapes a job plan, in order. Peers compare it so a client whose
        /// house data differs from the host's is caught instead of silently playing a different job.
        /// </summary>
        public string ContentHash()
        {
            var text = new StringBuilder();
            text.Append(Id).Append('\n');
            foreach (var room in Rooms)
            {
                text.Append("R|").Append(room.Id).Append('\n');
                foreach (var slot in room.Slots)
                {
                    text.Append("S|").Append(slot.Id).Append('|').Append(slot.ItemId).Append('|');
                    AppendPlacement(text, slot.Target);
                    text.Append(Number(slot.Tolerance.PositionMeters)).Append('|').Append(Number(slot.Tolerance.AngleDegrees)).Append('|');
                    foreach (var problem in slot.Supports)
                        text.Append((int)problem).Append(',');
                    text.Append('\n');
                }
            }
            foreach (var point in ScatterPoints)
                AppendPlacement(text.Append("P|"), point).Append('\n');
            foreach (var spot in HidingSpots)
                AppendPlacement(text.Append("H|"), spot).Append('\n');

            return Fnv1a(text.ToString());
        }

        /// <summary>64-bit FNV-1a of the UTF-8 text as 16 hex digits; the same on every machine.</summary>
        internal static string Fnv1a(string text)
        {
            const ulong offset = 14695981039346656037UL, prime = 1099511628211UL;
            var hash = offset;
            foreach (var b in Encoding.UTF8.GetBytes(text))
                hash = (hash ^ b) * prime;
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        internal static StringBuilder AppendPlacement(StringBuilder text, Placement p) =>
            text.Append(p.RoomId).Append('|')
                .Append(Number(p.Position.X)).Append(',').Append(Number(p.Position.Y)).Append(',').Append(Number(p.Position.Z)).Append('|')
                .Append(Number(p.Rotation.X)).Append(',').Append(Number(p.Rotation.Y)).Append(',')
                .Append(Number(p.Rotation.Z)).Append(',').Append(Number(p.Rotation.W)).Append('|');

        private static string Number(float value) => value.ToString("F3", CultureInfo.InvariantCulture);
    }
}
