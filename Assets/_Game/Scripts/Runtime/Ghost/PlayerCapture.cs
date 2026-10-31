using SpiritMovers.Player;
using Unity.Netcode;
using UnityEngine;

namespace SpiritMovers.Ghost
{
    /// <summary>
    /// A player's side of the memory cabinet (GDD 6.3): which cabinet holds them (synced), when they get out alone,
    /// and a short immunity after leaving. While caught the owner cannot walk or pick things up; pressing interact
    /// knocks on the cabinet so friends notice.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerCapture : NetworkBehaviour
    {
        private readonly NetworkVariable<NetworkObjectReference> m_Cabinet = new();
        private readonly NetworkVariable<float> m_ReleaseAt = new();
        private PlayerMotor m_Motor;

        public bool IsCaptured => m_Cabinet.Value.TryGet(out _);
        public MemoryCabinet Cabinet => m_Cabinet.Value.TryGet(out var cabinet) ? cabinet.GetComponent<MemoryCabinet>() : null;
        /// <summary>When they get out alone, on the host's clock.</summary>
        public float ReleaseAt => m_ReleaseAt.Value;
        /// <summary>Host: the ghost leaves this player alone until then.</summary>
        public float ImmuneUntil { get; private set; }

        private void Awake() => m_Motor = GetComponent<PlayerMotor>();

        public override void OnNetworkSpawn()
        {
            m_Cabinet.OnValueChanged += (_, _) => ApplyLock();
            ApplyLock();
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
                Cabinet?.OnCaptiveGone(this);
        }

        private void ApplyLock()
        {
            if (IsOwner)
                m_Motor.MovementLocked = IsCaptured;
        }

        private void LateUpdate()
        {
            if (IsSpawned && IsOwner && IsCaptured && m_Motor.CurrentInput.InteractPressed)
                KnockRpc();
        }

        [Rpc(SendTo.Server)]
        private void KnockRpc() => Cabinet?.Knock();

        /// <summary>Host: shut this player in a cabinet, standing at <paramref name="feet"/>.</summary>
        internal void SetCaptured(MemoryCabinet cabinet, float releaseAt, Vector3 feet, float yaw)
        {
            m_ReleaseAt.Value = releaseAt;
            m_Cabinet.Value = cabinet.NetworkObject;
            TeleportRpc(feet, yaw);
        }

        /// <summary>Host: let this player out at <paramref name="feet"/>; the ghost ignores them until <paramref name="immuneUntil"/>.</summary>
        internal void SetReleased(Vector3 feet, float yaw, float immuneUntil)
        {
            ImmuneUntil = immuneUntil;
            m_Cabinet.Value = default;
            TeleportRpc(feet, yaw);
        }

        /// <summary>The owner moves its own body (owner-authoritative movement).</summary>
        [Rpc(SendTo.Owner)]
        private void TeleportRpc(Vector3 feet, float yaw) => m_Motor.Teleport(feet, yaw);
    }
}
