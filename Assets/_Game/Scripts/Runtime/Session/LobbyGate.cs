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
            var refusal = Decide(phase, m_Network.ConnectedClientsIds.Count);
            response.Approved = refusal == JoinRefusal.None;
            response.CreatePlayerObject = response.Approved;
            response.Reason = SessionErrors.KeyFor(refusal);
            if (!response.Approved)
                Debug.Log($"[Lobby] refused client {request.ClientNetworkId}: {refusal}");
        }

        internal static JoinRefusal Decide(SessionPhase phase, int players) =>
            SessionRules.CanJoin(phase, players, SessionRules.MaxPlayers, versionMatches: true);
    }
}
