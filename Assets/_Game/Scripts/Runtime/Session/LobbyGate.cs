using System.Collections.Generic;
using SpiritMovers.Core.Session;
using SpiritMovers.Save;
using Unity.Netcode;
using UnityEngine;

namespace SpiritMovers.Session
{
    /// <summary>
    /// Host: who may connect (GDD 9) — only while the team is in the van and there is room; the refusal reason is a
    /// localization key the joining player's menu shows. The version and the player's name come in the payload.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public sealed class LobbyGate : MonoBehaviour
    {
        /// <summary>Host: the names joining players sent (cleaned), by client id.</summary>
        private static readonly Dictionary<ulong, string> s_Names = new();

        private NetworkManager m_Network;

        private void Awake()
        {
            m_Network = GetComponent<NetworkManager>();
            m_Network.ConnectionApprovalCallback = Approve;
            m_Network.OnServerStarted += s_Names.Clear;
        }

        /// <summary>Host: the name to show for a player — the host's own from its save, "" if a client sent none.</summary>
        internal static string NameFor(ulong clientId) =>
            clientId == NetworkManager.ServerClientId ? GameData.PlayerName : s_Names.TryGetValue(clientId, out var name) ? name : "";

        internal void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            var phase = GameSession.Instance != null ? GameSession.Instance.Phase : SessionPhase.Lobby;
            // The host itself connects with an empty payload; everyone else sends their game version (TECHNICAL_DESIGN 7).
            var isHost = request.ClientNetworkId == NetworkManager.ServerClientId;
            var versionMatches = isHost || VersionMatches(request.Payload, Bootstrap.Version);
            if (!isHost && ConnectionPayload.TryRead(request.Payload, out _, out var name))
                s_Names[request.ClientNetworkId] = name;
            var refusal = Decide(phase, m_Network.ConnectedClientsIds.Count, versionMatches);
            response.Approved = refusal == JoinRefusal.None;
            response.CreatePlayerObject = response.Approved;
            // A version refusal carries the host's version for the "yours · room" message.
            response.Reason = refusal == JoinRefusal.VersionMismatch
                ? $"{SessionErrors.Version}:{Bootstrap.Version}"
                : SessionErrors.KeyFor(refusal);
            if (!response.Approved)
                Debug.Log($"[Lobby] refused client {request.ClientNetworkId}: {refusal}");
        }

        internal static JoinRefusal Decide(SessionPhase phase, int players, bool versionMatches) =>
            SessionRules.CanJoin(phase, players, SessionRules.MaxPlayers, versionMatches);

        /// <summary>The joining player's version (first line of the approval payload) can play with the host's.</summary>
        internal static bool VersionMatches(byte[] payload, SpiritMovers.Core.GameVersion host) =>
            ConnectionPayload.TryRead(payload, out var version, out _) &&
            SpiritMovers.Core.GameVersion.TryParse(version, out var client) && host.CanPlayWith(client);
    }
}
