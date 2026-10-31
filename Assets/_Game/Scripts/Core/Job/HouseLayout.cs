using System.Collections.Generic;

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
    }
}
