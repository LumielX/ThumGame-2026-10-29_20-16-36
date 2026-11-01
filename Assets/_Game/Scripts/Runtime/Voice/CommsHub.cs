using System.Collections.Generic;
using SpiritMovers.Core.Voice;
using SpiritMovers.Job;
using SpiritMovers.Player;
using SpiritMovers.Save;
using SpiritMovers.Session;
using Unity.Netcode;
using UnityEngine;

namespace SpiritMovers.Voice
{
    /// <summary>
    /// Pings and quick messages for players without a microphone (GDD 10, D-013) — over the game's own network, so
    /// they work solo and while voice is down. One per scene (van and house). The host holds the rate down
    /// (PingRules, one quick message per 0.5 s) and everyone sees the marker or the message.
    /// </summary>
    public sealed class CommsHub : NetworkBehaviour
    {
        private const float PingReach = 30f;
        private const float MissDistance = 10f;
        private const float QuickInterval = 0.5f;

        [SerializeField] private GameObject m_MarkerPrefab;

        private readonly PingRules m_Rules = new();
        private readonly Dictionary<ulong, float> m_QuickReadyAt = new();
        private readonly List<PingMarker> m_Markers = new();

        public static CommsHub Current { get; private set; }

        /// <summary>The ping markers up now on this machine.</summary>
        internal IReadOnlyList<PingMarker> Markers
        {
            get
            {
                m_Markers.RemoveAll(m => m == null);
                return m_Markers;
            }
        }

        public override void OnNetworkSpawn() => Current = this;

        public override void OnNetworkDespawn()
        {
            if (Current == this)
                Current = null;
        }

        private void Update()
        {
            var local = PlayerMotor.Local;
            if (!IsSpawned || local == null)
                return;
            var input = local.CurrentInput;
            if (input.PingPressed)
                Ping(local);
            if (input.QuickPressed >= 1 && input.QuickPressed <= 6)
                SendQuick((QuickMessage)(input.QuickPressed - 1));
        }

        /// <summary>Mark where this player looks: the first thing in 30 m, or a point 10 m ahead.</summary>
        public void Ping(PlayerMotor from)
        {
            var eye = from.Head.position;
            var look = from.Look;
            var point = eye + look * MissDistance;
            var best = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(eye, look, PingReach, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(from.transform) || hit.distance >= best)
                    continue;
                best = hit.distance;
                point = hit.point;
            }
            PingRpc(point);
        }

        public void SendQuick(QuickMessage message) => QuickRpc(message);

        [Rpc(SendTo.Server)]
        internal void PingRpc(Vector3 point, RpcParams rpcParams = default)
        {
            var sender = rpcParams.Receive.SenderClientId;
            if (m_Rules.TryAdd(sender, Time.time))
                ShowPingRpc(sender, point);
        }

        [Rpc(SendTo.Everyone)]
        private void ShowPingRpc(ulong sender, Vector3 point)
        {
            if (m_MarkerPrefab == null)
                return;
            var marker = Instantiate(m_MarkerPrefab, point, Quaternion.identity).GetComponent<PingMarker>();
            marker.Show(NameFor(sender), m_Rules.Lifetime);
            m_Markers.Add(marker);
        }

        [Rpc(SendTo.Server)]
        private void QuickRpc(QuickMessage message, RpcParams rpcParams = default)
        {
            var sender = rpcParams.Receive.SenderClientId;
            if (m_QuickReadyAt.TryGetValue(sender, out var readyAt) && Time.time < readyAt)
                return;
            m_QuickReadyAt[sender] = Time.time + QuickInterval;
            ShowQuickRpc(sender, message);
        }

        [Rpc(SendTo.Everyone)]
        private void ShowQuickRpc(ulong sender, QuickMessage message)
        {
            // Each machine shows it in its own language.
            var chat = FindAnyObjectByType<ChatPanel>();
            if (chat != null)
                chat.Show(NameFor(sender), JobUi.Text(ChatText.KeyFor(message)));
        }

        /// <summary>Our own name from the save; others from the room's roster ("ผู้เล่น N" if they gave none).</summary>
        private static string NameFor(ulong clientId)
        {
            var network = NetworkManager.Singleton;
            if (network != null && clientId == network.LocalClientId)
                return GameData.PlayerName;
            foreach (var member in LobbyState.Roster)
                if (member.ClientId == clientId)
                    return member.Name.IsEmpty ? JobUi.Format("lobby.player_n", member.Number) : member.Name.ToString();
            return JobUi.Format("lobby.player_n", "?");
        }
    }
}
