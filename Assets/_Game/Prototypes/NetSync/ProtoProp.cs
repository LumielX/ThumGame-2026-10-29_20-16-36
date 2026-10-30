using Unity.Netcode;
using UnityEngine;

namespace SpiritMovers.Prototypes.NetSync
{
    public enum CarryMode : byte
    {
        /// <summary>(A) Host simulates the carried object toward the carrier's synced hold point.</summary>
        HostSimulated = 0,
        /// <summary>(B) Carrier takes ownership and moves the object locally; host takes it back on drop.</summary>
        OwnerTransfer = 1,
    }

    /// <summary>
    /// Throwaway M2 prototype: a physics prop that players can carry with either approach
    /// from TECHNICAL_DESIGN 6 so the two can be compared under simulated latency.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ProtoProp : NetworkBehaviour
    {
        public const ulong NoCarrier = ulong.MaxValue;
        private const float FollowGain = 12f;

        private readonly NetworkVariable<ulong> m_Carrier = new(NoCarrier);
        private readonly NetworkVariable<CarryMode> m_Mode = new(CarryMode.HostSimulated);

        private Rigidbody m_Body;

        public ulong Carrier => m_Carrier.Value;
        public CarryMode Mode => m_Mode.Value;
        public bool IsCarried => m_Carrier.Value != NoCarrier;
        public Rigidbody Body => m_Body;

        private void Awake() => m_Body = GetComponent<Rigidbody>();

        [Rpc(SendTo.Server)]
        public void RequestCarryRpc(CarryMode mode, RpcParams rpcParams = default)
        {
            var sender = rpcParams.Receive.SenderClientId;
            if (IsCarried)
                return;

            m_Mode.Value = mode;
            m_Carrier.Value = sender;
            if (mode == CarryMode.OwnerTransfer && sender != NetworkManager.ServerClientId)
                NetworkObject.ChangeOwnership(sender);
        }

        [Rpc(SendTo.Server)]
        public void RequestDropRpc(RpcParams rpcParams = default)
        {
            if (m_Carrier.Value != rpcParams.Receive.SenderClientId)
                return;

            m_Carrier.Value = NoCarrier;
            if (OwnerClientId != NetworkManager.ServerClientId)
                NetworkObject.RemoveOwnership();
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && IsCarried)
                m_Carrier.Value = NoCarrier;
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsCarried)
                return;

            var simulatesHere = m_Mode.Value == CarryMode.HostSimulated ? IsServer : IsOwner;
            if (!simulatesHere || !ProtoPlayer.TryGet(m_Carrier.Value, out var carrier))
                return;

            m_Body.linearVelocity = (carrier.HoldPoint - m_Body.position) * FollowGain;
            m_Body.angularVelocity *= 0.5f;
        }
    }
}
