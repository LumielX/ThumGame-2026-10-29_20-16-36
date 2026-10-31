using SpiritMovers.Core.Job;
using SpiritMovers.Interaction;
using UnityEngine;

namespace SpiritMovers.Job
{
    /// <summary>
    /// A memory slot (GDD 5.1): this transform is where the ghost remembers <see cref="Item"/>. Authored at the
    /// item's home, so the target never depends on where the item happens to be when someone joins.
    /// </summary>
    public sealed class MemorySlotMarker : MonoBehaviour
    {
        [SerializeField] private string m_SlotId;
        [SerializeField] private Carryable m_Item;
        [SerializeField] private ProblemType[] m_Supports = { ProblemType.Moved, ProblemType.Hidden };
        [SerializeField] private float m_PositionTolerance = 0.25f;
        [SerializeField] private float m_AngleTolerance = 20f;

        public string SlotId => m_SlotId;
        public Carryable Item => m_Item;
        public ProblemType[] Supports => m_Supports;
        public Tolerance Tolerance => new() { PositionMeters = m_PositionTolerance, AngleDegrees = m_AngleTolerance };

        /// <summary>Editor setup.</summary>
        public void Configure(string slotId, Carryable item)
        {
            m_SlotId = slotId;
            m_Item = item;
        }
    }
}
