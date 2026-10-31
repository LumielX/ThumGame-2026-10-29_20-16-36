using System.Collections;
using SpiritMovers.Job;
using Unity.Netcode;
using UnityEngine;

namespace SpiritMovers.Player
{
    /// <summary>
    /// Emotes (GDD 8.1). M3D has the one that matters: "wai" (G) bows to the ghost and calms it once per room — the
    /// host decides by where it sees the player. Placeholder look: the head dips for a moment on every machine.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerEmote : NetworkBehaviour
    {
        private const float BowSeconds = 0.6f;
        private const float BowDegrees = 35f;

        private PlayerMotor m_Motor;

        private void Awake() => m_Motor = GetComponent<PlayerMotor>();

        private void LateUpdate()
        {
            if (IsSpawned && IsOwner && m_Motor.CurrentInput.WaiPressed)
                RequestWaiRpc();
        }

        [Rpc(SendTo.Server)]
        private void RequestWaiRpc()
        {
            var director = FindAnyObjectByType<JobDirector>();
            if (director != null)
                director.Wai(director.House.RoomAt(m_Motor.Feet));
            WaiRpc();
        }

        [Rpc(SendTo.Everyone)]
        private void WaiRpc()
        {
            if (isActiveAndEnabled)
                StartCoroutine(Bow());
        }

        private IEnumerator Bow()
        {
            var head = m_Motor.Head;
            var start = Time.time;
            while (Time.time - start < BowSeconds)
            {
                var t = (Time.time - start) / BowSeconds;
                head.localRotation = Quaternion.Euler(m_Motor.Pitch + Mathf.Sin(t * Mathf.PI) * BowDegrees, 0f, 0f);
                yield return null;
            }
            head.localRotation = Quaternion.Euler(m_Motor.Pitch, 0f, 0f);
        }
    }
}
