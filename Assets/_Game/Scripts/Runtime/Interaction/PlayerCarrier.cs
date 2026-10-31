using System.Collections.Generic;
using SpiritMovers.Core.Player;
using SpiritMovers.Player;
using Unity.Netcode;
using UnityEngine;

namespace SpiritMovers.Interaction
{
    /// <summary>The hands of a player: what they hold and where it sits. Present on every peer.</summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerCarrier : NetworkBehaviour
    {
        private static readonly Dictionary<ulong, PlayerCarrier> s_ByClient = new();
        private readonly CarryConfig m_Config = CarryConfig.Default;

        public PlayerMotor Motor { get; private set; }
        public Carryable Held { get; private set; }

        /// <summary>Hold point for the held item (yaw only, so every peer computes the same).</summary>
        public Vector3 HoldPoint =>
            CarryRules.HoldPoint(Motor.Feet.ToNumerics(), Motor.Forward.ToNumerics(), Held != null ? Held.Mass : 0f, m_Config).ToUnity();

        public static bool TryGet(ulong clientId, out PlayerCarrier carrier) => s_ByClient.TryGetValue(clientId, out carrier);

        private void Awake() => Motor = GetComponent<PlayerMotor>();

        public override void OnNetworkSpawn() => s_ByClient[OwnerClientId] = this;

        public override void OnNetworkDespawn()
        {
            if (s_ByClient.TryGetValue(OwnerClientId, out var registered) && registered == this)
                s_ByClient.Remove(OwnerClientId);
        }

        /// <summary>Called on every peer when an item's carrier list changes.</summary>
        internal static void RefreshHeld(Carryable item)
        {
            foreach (var carrier in s_ByClient.Values)
            {
                if (item.IsCarriedBy(carrier.OwnerClientId))
                    carrier.Held = item;
                else if (carrier.Held == item)
                    carrier.Held = null;
            }
        }

        internal static void ForgetItem(Carryable item)
        {
            foreach (var carrier in s_ByClient.Values)
                if (carrier.Held == item)
                    carrier.Held = null;
        }

        private void Update()
        {
            if (IsOwner)
                Motor.Load = Held == null ? CarryLoad.None : CarryRules.LoadFor(Held.Mass, Held.CarrierCount, m_Config);
        }
    }
}
