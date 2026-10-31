using System;
using System.Collections;
using SpiritMovers.Core.Job;
using SpiritMovers.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace SpiritMovers.Job
{
    /// <summary>
    /// The ghost bell of a room (GDD 5.2): ring it and the ghost knocks once per item already in place, without
    /// saying which. Ringing annoys the ghost; rings within the cooldown do nothing (host decides).
    /// </summary>
    public sealed class GhostBell : NetworkBehaviour, IInteractable
    {
        private const float CooldownSeconds = 5f;
        private const float FlashSeconds = 0.3f;

        [SerializeField] private string m_RoomId;
        [SerializeField] private Light m_Light;

        private JobDirector m_Director;
        private float m_ReadyAt;

        public string RoomId => m_RoomId;
        /// <summary>The ghost's last answer for this room (−1 before the first ring); every peer.</summary>
        public int LastNod { get; private set; } = -1;
        public string PromptKey => "bell.ring";

        /// <summary>Every peer: the ghost knocked this many times.</summary>
        public event Action<int> Nodded;

        private void Awake()
        {
            m_Director = FindAnyObjectByType<JobDirector>();
            if (m_Light != null)
                m_Light.enabled = false;
        }

        public bool CanInteract(PlayerCarrier by) =>
            by.Held == null && m_Director != null && m_Director.Phase == JobPhase.Exploring;

        public void Interact(PlayerCarrier by)
        {
            if (by.IsOwner)
                RequestRingRpc();
        }

        [Rpc(SendTo.Server)]
        private void RequestRingRpc()
        {
            if (Time.time < m_ReadyAt || m_Director.Phase != JobPhase.Exploring)
                return;
            m_ReadyAt = Time.time + CooldownSeconds;
            var count = m_Director.RingBell(m_RoomId);
            if (count >= 0)
                NodRpc(count);
        }

        [Rpc(SendTo.Everyone)]
        private void NodRpc(int count)
        {
            LastNod = count;
            Nodded?.Invoke(count);
            if (m_Light != null && isActiveAndEnabled)
                StartCoroutine(Flash(count));
        }

        private IEnumerator Flash(int count)
        {
            for (var i = 0; i < count; i++)
            {
                m_Light.enabled = true;
                yield return new WaitForSeconds(FlashSeconds);
                m_Light.enabled = false;
                yield return new WaitForSeconds(FlashSeconds);
            }
        }

        /// <summary>Editor setup.</summary>
        public void Configure(string roomId, Light light)
        {
            m_RoomId = roomId;
            m_Light = light;
        }
    }
}
