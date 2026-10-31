using System;
using System.Collections.Generic;
using System.Linq;
using SpiritMovers.Core.Ghost;
using SpiritMovers.Core.Player;
using SpiritMovers.Ghost;
using SpiritMovers.Player;
using Unity.Netcode;
using UnityEngine;

namespace SpiritMovers.Interaction
{
    /// <summary>
    /// A physics item players carry (D-034). One carrier owns it and moves it locally (method B);
    /// with two carriers (heavy items only) the host owns and moves it toward their shared hold point (method A).
    /// The host decides who carries; the owner of the item applies the follow velocity.
    /// The host also drops items left far from the hand (stuck, glitched) and releases carriers who disconnect.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Carryable : NetworkBehaviour, IInteractable
    {
        private readonly CarryConfig m_Config = CarryConfig.Default;
        private const float MaxPickUpAngle = 60f;

        private readonly NetworkVariable<float> m_YawOffset = new();
        private readonly List<System.Numerics.Vector3> m_HoldPointBuffer = new();
        private NetworkList<ulong> m_Carriers;
        private Rigidbody m_Body;
        private Vector3? m_PendingThrow;
        /// <summary>Server: one watch per carrier, so a carrier who walks away from a shared crate lets go.</summary>
        private readonly Dictionary<ulong, HoldErrorWatch> m_HoldWatches = new();

        /// <summary>Server: an item was taken out of a carrier's hands because it stayed far from them.</summary>
        public static event Action<Carryable, ulong> ForceDropped;

        /// <summary>Test hook: stops the owner pulling the item to the hand.</summary>
        internal bool FollowEnabled { get; set; } = true;

        public float Mass => m_Body.mass;
        public IReadOnlyList<ulong> Carriers => m_Carriers.Count == 0 ? new List<ulong>() : Enumerable.Range(0, m_Carriers.Count).Select(i => m_Carriers[i]).ToList();
        public int CarrierCount => m_Carriers.Count;
        public bool IsCarried => m_Carriers.Count > 0;
        /// <summary>Carried yaw relative to the carrier's heading.</summary>
        internal float YawOffset => m_YawOffset.Value;
        public string PromptKey => "interact.pickup";

        private void Awake()
        {
            m_Body = GetComponent<Rigidbody>();
            m_Carriers = new NetworkList<ulong>();
        }

        public override void OnNetworkSpawn()
        {
            m_Carriers.OnListChanged += _ => PlayerCarrier.RefreshHeld(this);
            if (IsServer)
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null)
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            PlayerCarrier.ForgetItem(this);
        }

        /// <summary>Server: a player who leaves drops what they held; the item stays (D-034).</summary>
        internal void OnClientDisconnected(ulong clientId) => ReleaseCarrier(clientId);

        public bool IsCarriedBy(ulong clientId)
        {
            for (var i = 0; i < m_Carriers.Count; i++)
                if (m_Carriers[i] == clientId)
                    return true;
            return false;
        }

        public bool CanInteract(PlayerCarrier by) =>
            by.Held == null && !IsCarriedBy(by.OwnerClientId) && m_Carriers.Count < CarryRules.MaxCarriersFor(Mass, m_Config);

        /// <summary>
        /// Only the interacting player's own machine sends the request (the RPC carries their id), with the heading
        /// it is facing now: the host's copy of a remote player's heading lags, so a quick turn-and-grab would fail.
        /// </summary>
        public void Interact(PlayerCarrier by)
        {
            if (by.IsOwner)
                RequestPickUpRpc(by.Motor.Forward);
        }

        [Rpc(SendTo.Server)]
        public void RequestPickUpRpc(Vector3 heading, RpcParams rpcParams = default) => TryAddCarrier(rpcParams.Receive.SenderClientId, heading);

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

        /// <summary>
        /// Server: add a carrier if allowed (not already carrying, room for another, within reach). <paramref name="heading"/>
        /// is the carrier's own view of where they face (default: the host's copy).
        /// </summary>
        public bool TryAddCarrier(ulong clientId, Vector3? heading = null)
        {
            if (!IsServer || IsCarriedBy(clientId) || m_Carriers.Count >= CarryRules.MaxCarriersFor(Mass, m_Config))
                return false;
            if (!PlayerCarrier.TryGet(clientId, out var carrier) || carrier.Held != null)
                return false;
            var facing = heading is { } h && new Vector2(h.x, h.z).sqrMagnitude > 0.01f ? h : carrier.Motor.Forward;
            if (!InReach(carrier.Motor, facing, out var why))
            {
                Debug.Log($"[Carry] pick-up refused {name} for client {clientId}: {why}");
                return false;
            }

            m_PendingThrow = null; // a throw not yet applied must not fling the item after this new carry ends

            if (m_Carriers.Count == 0)
                m_YawOffset.Value = Mathf.DeltaAngle(Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg, transform.eulerAngles.y);
            m_Carriers.Add(clientId);
            m_HoldWatches[clientId] = new HoldErrorWatch(m_Config);
            UpdateOwnership();
            return true;
        }

        /// <summary>Test hook: make <paramref name="clientId"/> the only carrier without the usual checks.</summary>
        internal void SetSingleCarrierForTest(ulong clientId)
        {
            m_Carriers.Clear();
            m_Carriers.Add(clientId);
            UpdateOwnership();
        }

        /// <summary>
        /// Host check (TECHNICAL_DESIGN 6): within reach of the chest, roughly in front of <paramref name="facing"/>
        /// (heading only: head pitch is not synced) and visible from the eyes, so no picking up through walls or behind
        /// the back.
        /// </summary>
        private bool InReach(PlayerMotor motor, Vector3 facing, out string why)
        {
            var item = m_Body.position;
            var distance = Vector3.Distance(motor.Feet + Vector3.up, item);
            why = $"{distance:F2} m away";
            if (distance > m_Config.ReachMeters + 0.5f)
                return false;

            var toItem = item - motor.Feet;
            toItem.y = 0f;
            facing.y = 0f;
            var angle = toItem.sqrMagnitude > 0.01f ? Vector3.Angle(facing, toItem) : 0f;
            why = $"{angle:F0} deg off heading";
            if (angle > MaxPickUpAngle)
                return false;

            // Rays starting inside the player's own capsule ignore it; anything else hit first is in the way.
            if (!Physics.Linecast(motor.Head.position, item, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                || hit.collider.GetComponentInParent<Carryable>() == this)
                return true;
            why = $"blocked by {hit.collider.name}";
            return false;
        }

        /// <summary>Server: stop <paramref name="clientId"/> carrying this item; it stays in the world.</summary>
        public void ReleaseCarrier(ulong clientId)
        {
            if (!IsServer || !m_Carriers.Remove(clientId))
                return;
            m_HoldWatches.Remove(clientId);
            UpdateOwnership();
        }

        /// <summary>Server: every carrier lets go at once; ownership is back on the host when this returns.</summary>
        public void ReleaseAll()
        {
            if (!IsServer || m_Carriers.Count == 0)
                return;
            m_Carriers.Clear();
            m_HoldWatches.Clear();
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
            foreach (var watch in m_HoldWatches.Values)
                watch.Suspend(m_Config.HandoffGraceSeconds);
            if (owner == NetworkManager.ServerClientId)
                NetworkObject.RemoveOwnership();
            else
                NetworkObject.ChangeOwnership(owner);
        }

        /// <summary>Host: a hard knock (dropped, thrown, bumped) is a noise the ghost may hear.</summary>
        private void OnCollisionEnter(Collision collision)
        {
            var hub = NoiseHub.Current;
            if (!IsSpawned || !IsServer || hub == null)
                return;
            var loudness = hub.Config.ImpactLoudness(collision.relativeVelocity.magnitude);
            if (loudness > 0f)
                hub.Report(new NoiseEvent(m_Body.position.ToNumerics(), loudness, NoiseSource.ItemImpact));
        }

        private void FixedUpdate()
        {
            if (!IsSpawned)
                return;
            if (IsServer)
                CheckHoldError();
            if (!IsOwner)
                return;

            if (m_PendingThrow is { } velocity && !IsCarried)
            {
                m_Body.linearVelocity = velocity;
                m_PendingThrow = null;
            }

            m_Body.useGravity = !IsCarried;
            if (!IsCarried || !FollowEnabled || !TryGetHoldPoint(out var hold, out var yaw))
                return;

            m_Body.linearVelocity = CarryRules.FollowVelocity(hold.ToNumerics(), m_Body.position.ToNumerics(), m_Config).ToUnity();
            m_Body.angularVelocity = Vector3.zero;
            m_Body.MoveRotation(Quaternion.Euler(0f, yaw + m_YawOffset.Value, 0f));
        }

        /// <summary>
        /// Server: a carrier whose hand stays far from the item lets go of it (stuck item, or a shared crate
        /// whose carriers walk apart). A carrier whose player object is gone lets go at once. Both positions are
        /// the host's (equally delayed) view; checks pause briefly after an ownership handoff.
        /// </summary>
        private void CheckHoldError()
        {
            for (var i = m_Carriers.Count - 1; i >= 0; i--)
            {
                var clientId = m_Carriers[i];
                if (!PlayerCarrier.TryGet(clientId, out var carrier))
                {
                    ReleaseCarrier(clientId);
                    continue;
                }
                var distance = Vector3.Distance(m_Body.position, carrier.HoldPoint);
                if (!m_HoldWatches.TryGetValue(clientId, out var watch) || !watch.Update(distance, Time.fixedDeltaTime))
                    continue;

                Debug.Log($"[Carry] force drop {name} from client {clientId} ({distance:F1} m from hand)");
                ReleaseCarrier(clientId);
                ForceDropped?.Invoke(this, clientId);
            }
        }

        /// <summary>Where the item should be: the carrier's hold point, or the average of all carriers'.</summary>
        private bool TryGetHoldPoint(out Vector3 hold, out float yaw)
        {
            var points = m_HoldPointBuffer;
            points.Clear();
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
