using System.Collections.Generic;
using System.Linq;
using SpiritMovers.Core.Ghost;
using SpiritMovers.Core.Job;
using SpiritMovers.Job;
using SpiritMovers.Player;
using UnityEngine;

namespace SpiritMovers.Ghost
{
    /// <summary>
    /// This machine's spirit camera overlay (TECHNICAL_DESIGN 12): local, see-through amber copies of the planned items
    /// that are out of place, drawn where the ghost remembers them, for a few seconds. Never networked — only the
    /// player who looked sees it. Lives on the job director object.
    /// </summary>
    [RequireComponent(typeof(JobDirector))]
    public sealed class MemoryOverlay : MonoBehaviour
    {
        [SerializeField] private Material m_Material;

        private readonly ToolConfig m_Config = new();
        private readonly List<GameObject> m_Shown = new();
        private JobDirector m_Director;
        private float m_HideAt;

        public static MemoryOverlay Current { get; private set; }

        public int ActiveCount => m_Shown.Count;

        private void Awake()
        {
            Current = this;
            m_Director = GetComponent<JobDirector>();
        }

        private void OnDestroy()
        {
            Clear();
            if (Current == this)
                Current = null;
        }

        /// <summary>Show the memories within range of <paramref name="eye"/>; replaces any overlay still showing.</summary>
        public void Show(Vector3 eye)
        {
            Clear();
            var plan = m_Director.Plan;
            if (plan == null)
                return;
            var house = m_Director.House;
            foreach (var task in plan.Rooms.SelectMany(r => r.Slots))
            {
                var item = house.Slot(task.Slot.Id).Item;
                var here = item.transform;
                var actual = new Placement(house.RoomAt(here.position), here.position.ToNumerics(), here.rotation.ToNumerics());
                var place = task.Slot.Target.Position.ToUnity();
                if (PlacementMath.IsCorrect(task.Slot.Target, actual, task.Slot.Tolerance) ||
                    Vector3.Distance(eye, place) > m_Config.OverlayRange)
                    continue;

                var memory = new GameObject($"Memory_{task.Slot.Id}");
                memory.transform.SetParent(transform, false);
                memory.transform.SetPositionAndRotation(place, task.Slot.Target.Rotation.ToUnity());
                memory.transform.localScale = here.lossyScale;
                memory.AddComponent<MeshFilter>().sharedMesh = item.GetComponent<MeshFilter>().sharedMesh;
                memory.AddComponent<MeshRenderer>().sharedMaterial = m_Material;
                m_Shown.Add(memory);
            }
            m_HideAt = Time.time + m_Config.OverlaySeconds;
        }

        private void Update()
        {
            if (m_Shown.Count > 0 && (Time.time >= m_HideAt || m_Director.Phase == JobPhase.Ended))
                Clear();
        }

        private void Clear()
        {
            foreach (var memory in m_Shown)
                if (memory != null)
                    Destroy(memory);
            m_Shown.Clear();
        }
    }
}
