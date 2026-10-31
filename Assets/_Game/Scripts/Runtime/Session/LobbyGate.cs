using SpiritMovers.Core.Session;
using Unity.Netcode;
using UnityEngine;

namespace SpiritMovers.Session
{
    /// <summary>
    /// Host: who may connect (GDD 9) — only while the team is in the van and there is room; the refusal reason is a
    /// localization key the joining player's menu shows. The game version is already checked on the session itself.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public sealed class LobbyGate : MonoBehaviour
    {
        private NetworkManager m_Network;

        private void Awake()
        {
            m_Network = GetComponent<NetworkManager>();
            m_Network.ConnectionApprovalCallback = Approve;
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            var phase = GameSession.Instance != null ? GameSession.Instance.Phase : SessionPhase.Lobby;
            // The host itself connects with an empty payload; everyone else sends their game version (TECHNICAL_DESIGN 7).
            var isHost = request.ClientNetworkId == NetworkManager.ServerClientId;
            var versionMatches = isHost || VersionMatches(request.Payload, Bootstrap.Version);
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

        /// <summary>The joining player's version (UTF-8 in the approval payload) can play with the host's.</summary>
        internal static bool VersionMatches(byte[] payload, SpiritMovers.Core.GameVersion host)
        {
            if (payload == null || payload.Length == 0)
                return false;
            string text;
            try
            {
                text = System.Text.Encoding.UTF8.GetString(payload);
            }
            catch (System.ArgumentException)
            {
                return false;
            }
            return SpiritMovers.Core.GameVersion.TryParse(text, out var client) && host.CanPlayWith(client);
        }
    }
}
