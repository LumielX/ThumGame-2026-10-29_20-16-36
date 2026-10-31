using UnityEngine;

namespace SpiritMovers.Job
{
    /// <summary>The space of one room of the house (trigger box). Slot markers and spots are its children.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RoomVolume : MonoBehaviour
    {
        [SerializeField] private string m_RoomId;

        private BoxCollider m_Box;

        public string RoomId => m_RoomId;

        private void Awake()
        {
            m_Box = GetComponent<BoxCollider>();
            m_Box.isTrigger = true;
        }

        public bool Contains(Vector3 point)
        {
            if (m_Box == null)
                m_Box = GetComponent<BoxCollider>();
            var local = transform.InverseTransformPoint(point) - m_Box.center;
            var half = m_Box.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }
    }
}
