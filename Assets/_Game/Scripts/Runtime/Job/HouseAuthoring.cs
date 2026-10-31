using System.Collections.Generic;
using SpiritMovers.Core.Job;
using SpiritMovers.Player;
using UnityEngine;

namespace SpiritMovers.Job
{
    /// <summary>
    /// A house laid out in the scene: <see cref="RoomVolume"/> children, each holding its
    /// <see cref="MemorySlotMarker"/>s and spots named "Scatter…" (moved items land there) and "Hide…".
    /// Everything is read in hierarchy order, so every peer builds the same <see cref="HouseLayout"/>.
    /// </summary>
    public sealed class HouseAuthoring : MonoBehaviour
    {
        [SerializeField] private string m_HouseId = "house";

        private readonly List<RoomVolume> m_Rooms = new();
        private readonly Dictionary<string, MemorySlotMarker> m_Slots = new();
        private HouseLayout m_Layout;

        public string HouseId => m_HouseId;
        public IReadOnlyList<RoomVolume> Rooms => m_Rooms;

        private void Awake() => Collect();

        public HouseLayout BuildLayout()
        {
            if (m_Layout != null)
                return m_Layout;
            Collect();

            var rooms = new List<RoomLayout>();
            var scatter = new List<Placement>();
            var hiding = new List<Placement>();
            foreach (var room in m_Rooms)
            {
                var slots = new List<SlotDefinition>();
                foreach (Transform child in room.transform)
                {
                    if (child.TryGetComponent<MemorySlotMarker>(out var marker))
                        slots.Add(new SlotDefinition(marker.SlotId, marker.Item != null ? marker.Item.name : "",
                            ToPlacement(room.RoomId, child), marker.Tolerance, marker.Supports));
                    else if (child.name.StartsWith("Scatter"))
                        scatter.Add(ToPlacement(room.RoomId, child));
                    else if (child.name.StartsWith("Hide"))
                        hiding.Add(ToPlacement(room.RoomId, child));
                }
                rooms.Add(new RoomLayout(room.RoomId, slots));
            }
            m_Layout = new HouseLayout(m_HouseId, rooms, scatter, hiding);
            return m_Layout;
        }

        /// <summary>The first room (in hierarchy order) containing the point, or "" outside every room.</summary>
        public string RoomAt(Vector3 point)
        {
            Collect();
            foreach (var room in m_Rooms)
                if (room.Contains(point))
                    return room.RoomId;
            return "";
        }

        public MemorySlotMarker Slot(string slotId)
        {
            Collect();
            return m_Slots.TryGetValue(slotId, out var marker) ? marker : null;
        }

        private void Collect()
        {
            if (m_Rooms.Count > 0)
                return;
            foreach (Transform child in transform)
            {
                if (!child.TryGetComponent<RoomVolume>(out var room))
                    continue;
                m_Rooms.Add(room);
                foreach (var marker in child.GetComponentsInChildren<MemorySlotMarker>())
                    m_Slots[marker.SlotId] = marker;
            }
        }

        private static Placement ToPlacement(string roomId, Transform t) =>
            new(roomId, t.position.ToNumerics(), t.rotation.ToNumerics());
    }
}
