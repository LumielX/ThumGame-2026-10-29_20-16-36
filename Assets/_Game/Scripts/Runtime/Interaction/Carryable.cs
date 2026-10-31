using System.Collections.Generic;
using System.Linq;
using SpiritMovers.Core.Player;
using SpiritMovers.Player;
using Unity.Netcode;
using UnityEngine;

namespace SpiritMovers.Interaction
{
    /// <summary>
    /// A physics item players carry (D-034). One carrier owns it and moves it locally (method B);
    /// with two carriers (heavy items only) the host owns and moves it toward their shared hold point (method A).
    /// The host decides who carries; the owner of the item applies the follow velocity.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Carryable : NetworkBehaviour, IInteractable
    {
        private readonly CarryConfig m_Config = CarryConfig.Default;
        private readonly NetworkVariable<float> m_YawOffset = new();
        private NetworkList<ulong> m_Carriers;
        private Rigidbody m_Body;
        private Vector3? m_PendingThrow;

        public float Mass => m_Body.mass;
        public IReadOnlyList<ulong> Carriers => m_Carriers.Count == 0 ? new List<ulong>() : Enumerable.Range(0, m_Carriers.Count).Select(i => m_Carriers[i]).ToList();
        public int CarrierCount => m_Carriers.Count;
        public bool IsCarried => m_Carriers.Count > 0;
        public string PromptKey => "interact.pickup";

        private void Awake()
        {
            m_Body = GetComponent<Rigidbody>();
            m_Carriers = new NetworkList<ulong>();
        }

        public override void OnNetworkSpawn() => m_Carriers.OnListChanged += _ => PlayerCarrier.RefreshHeld(this);

        public override void OnNetworkDespawn() => PlayerCarrier.ForgetItem(this);

        public bool IsCarriedBy(ulong clientId)
        {
            for (var i = 0; i < m_Carriers.Count; i++)
                if (m_Carriers[i] == clientId)
                    return true;
            return false;
        }

        public bool CanInteract(PlayerCarrier by) =>
            by.Held == null && !IsCarriedBy(by.OwnerClientId) && m_Carriers.Count < CarryRules.MaxCarriersFor(Mass, m_Config);

        public void Interact(PlayerCarrier by) => RequestPickUpRpc();

        [Rpc(SendTo.Server)]
        public void RequestPickUpRpc(RpcParams rpcParams = default) => TryAddCarrier(rpcParams.Receive.SenderClientId);

        [Rpc(SendTo.Server)]
        public void RequestDropRpc(RpcParams rpcParams = default) => ReleaseCarrier(rpcParams.Receive.SenderClientId);

        [Rpc(SendTo.Server)]
        public void RequestThrowRpc(Vector3 direction, RpcParams rpcParams = default) => Throw(rpcParams.Receive.SenderClientId, direction);

        [Rpc(SendTo.Server)]
        public void RequestRotateRpc(float steps, RpcParams rpcParams = default)
        {
            if (IsCarriedBy(rpcParams.Receive.SenderClientId))
                m_YawOffset.Value = Mathf.Repeat(m_YawOffset.Value + steps * m_Config.RotateStepDegrees, 360f);
        }

        /// <summary>Server: add a carrier if allowed (not already carrying, room for another, within reach).</summary>
        public bool TryAddCarrier(ulong clientId)
        {
            if (!IsServer || IsCarriedBy(clientId) || m_Carriers.Count >= CarryRules.MaxCarriersFor(Mass, m_Config))
                return false;
            if (!PlayerCarrier.TryGet(clientId, out var carrier) || carrier.Held != null)
                return false;
            var chest = carrier.Motor.Feet + Vector3.up;
            if (Vector3.Distance(chest, m_Body.position) > m_Config.ReachMeters + 0.5f)
                return false;

            if (m_Carriers.Count == 0)
                m_YawOffset.Value = Mathf.DeltaAngle(carrier.Motor.transform.eulerAngles.y, transform.eulerAngles.y);
            m_Carriers.Add(clientId);
            UpdateOwnership();
            return true;
        }

        /// <summary>Server: stop <paramref name="clientId"/> carrying this item; it stays in the world.</summary>
        public void ReleaseCarrier(ulong clientId)
        {
            if (!IsServer || !m_Carriers.Remove(clientId))
                return;
            UpdateOwnership();
        }

        /// <summary>Server: throw a light item the client carries alone.</summary>
        public void Throw(ulong clientId, Vector3 direction)
        {
            if (!IsServer || !IsCarriedBy(clientId) || !CarryRules.CanThrow(Mass, m_Carriers.Count, m_Config))
                return;
            ReleaseCarrier(clientId);
            // Applied in FixedUpdate once the host's rigidbody is authoritative again.
            m_PendingThrow = direction.normalized * m_Config.ThrowSpeed;
        }

        private void UpdateOwnership()
        {
            var owner = m_Carriers.Count == 1 ? m_Carriers[0] : NetworkManager.ServerClientId;
            if (OwnerClientId == owner)
                return;
            if (owner == NetworkManager.ServerClientId)
                NetworkObject.RemoveOwnership();
            else
                NetworkObject.ChangeOwnership(owner);
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsOwner)
                return;

            if (m_PendingThrow is { } velocity && !IsCarried)
            {
                m_Body.linearVelocity = velocity;
                m_PendingThrow = null;
            }

            m_Body.useGravity = !IsCarried;
            if (!IsCarried || !TryGetHoldPoint(out var hold, out var yaw))
                return;

            m_Body.linearVelocity = CarryRules.FollowVelocity(hold.ToNumerics(), m_Body.position.ToNumerics(), m_Config).ToUnity();
            m_Body.angularVelocity = Vector3.zero;
            m_Body.MoveRotation(Quaternion.Euler(0f, yaw + m_YawOffset.Value, 0f));
        }

        /// <summary>Where the item should be: the carrier's hold point, or the average of all carriers'.</summary>
        private bool TryGetHoldPoint(out Vector3 hold, out float yaw)
        {
            var points = new List<System.Numerics.Vector3>();
            yaw = 0f;
            for (var i = 0; i < m_Carriers.Count; i++)
            {
                if (!PlayerCarrier.TryGet(m_Carriers[i], out var carrier))
                    continue;
                if (points.Count == 0)
                    yaw = carrier.Motor.transform.eulerAngles.y;
                points.Add(carrier.HoldPoint.ToNumerics());
            }
            hold = points.Count > 0 ? CarryRules.SharedHoldPoint(points).ToUnity() : Vector3.zero;
            return points.Count > 0;
        }
    }
}
