using System;
using System.Collections.Generic;
using System.Linq;
using SpiritMovers.Core.Session;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SpiritMovers.Session
{
    /// <summary>One player in the van: who, their chosen name ("" shows as "ผู้เล่น N"), ready or not.</summary>
    public struct LobbyMember : INetworkSerializable, IEquatable<LobbyMember>
    {
        public ulong ClientId;
        public bool Ready;
        public int Number;
        public FixedString64Bytes Name;
        /// <summary>The player's UGS id, which voice chat knows them by ("" when unknown).</summary>
        public FixedString64Bytes VoiceId;

        public LobbyMember(ulong clientId, bool ready, int number, string name = "", string voiceId = "")
        {
            VoiceId = new FixedString64Bytes(Core.Session.ConnectionPayload.CleanVoiceId(voiceId).Length > 61
                ? Core.Session.ConnectionPayload.CleanVoiceId(voiceId).Substring(0, 61)
                : Core.Session.ConnectionPayload.CleanVoiceId(voiceId));
            ClientId = clientId;
            Ready = ready;
            Number = number;
            // FixedString64Bytes holds 61 bytes of UTF-8; a cleaned name (20 characters) always fits, this is the guard.
            Name = new FixedString64Bytes(Core.Save.PlayerName.FitUtf8(name, FixedString64Bytes.UTF8MaxLengthInBytes));
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Ready);
            serializer.SerializeValue(ref Number);
            serializer.SerializeValue(ref Name);
            serializer.SerializeValue(ref VoiceId);
        }

        public bool Equals(LobbyMember other) =>
            ClientId == other.ClientId && Ready == other.Ready && Number == other.Number && Name.Equals(other.Name) &&
            VoiceId.Equals(other.VoiceId);
    }

    /// <summary>
    /// The van between jobs (GDD 9): who is in, who is ready, which job the host picked, and the departure countdown.
    /// The host owns it; when the countdown ends with everyone still ready, the team loads the job scene.
    /// </summary>
    public sealed class LobbyState : NetworkBehaviour
    {
        private const float CountdownSeconds = 3f;

        private readonly NetworkVariable<FixedString32Bytes> m_Difficulty = new("normal");
        private readonly NetworkVariable<double> m_CountdownEndsAt = new(-1d);
        private NetworkList<LobbyMember> m_Members;

        /// <summary>Host: the difficulty of the job the team set off to (read by the job scene).</summary>
        public static string ChosenDifficulty { get; private set; } = "normal";

        public IReadOnlyList<LobbyMember> Members
        {
            get
            {
                var members = new List<LobbyMember>(m_Members.Count);
                foreach (var member in m_Members)
                    members.Add(member);
                return members;
            }
        }

        public string DifficultyId => m_Difficulty.Value.ToString();
        public bool IsCountingDown => m_CountdownEndsAt.Value >= 0d;
        public float SecondsLeft => IsCountingDown ? (float)Math.Max(0d, m_CountdownEndsAt.Value - NetworkManager.ServerTime.Time) : 0f;

        private static readonly List<LobbyMember> s_Roster = new();

        /// <summary>
        /// Everyone in the room as last seen in the van, kept on every machine for the job scene (the van scene is gone
        /// by then): names for voice chat and the player list. Cleared on the main menu.
        /// </summary>
        public static IReadOnlyList<LobbyMember> Roster => s_Roster;

        public static void ClearRoster() => s_Roster.Clear();

        /// <summary>The name for a voice id ("" if unknown or unnamed).</summary>
        public static string NameForVoice(string voiceId)
        {
            foreach (var member in s_Roster)
                if (!member.VoiceId.IsEmpty && member.VoiceId.ToString() == voiceId)
                    return member.Name.IsEmpty ? "" : member.Name.ToString();
            return "";
        }

        internal static void AddToRosterForTest(LobbyMember member) => s_Roster.Add(member);

        private void Awake() => m_Members = new NetworkList<LobbyMember>();

        private void CopyRoster()
        {
            s_Roster.Clear();
            foreach (var member in m_Members)
                s_Roster.Add(member);
        }

        public override void OnNetworkSpawn()
        {
            m_Members.OnListChanged += _ => CopyRoster();
            CopyRoster();
            if (!IsServer)
                return;
            foreach (var id in NetworkManager.ConnectedClientsIds)
                AddMember(id);
            NetworkManager.OnClientConnectedCallback += AddMember;
            NetworkManager.OnClientDisconnectCallback += RemoveMember;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer || NetworkManager == null)
                return;
            NetworkManager.OnClientConnectedCallback -= AddMember;
            NetworkManager.OnClientDisconnectCallback -= RemoveMember;
        }

        /// <summary>The sender switches between ready and not ready; un-readying stops a countdown.</summary>
        [Rpc(SendTo.Server)]
        public void ToggleReadyRpc(RpcParams rpcParams = default)
        {
            var sender = rpcParams.Receive.SenderClientId;
            for (var i = 0; i < m_Members.Count; i++)
            {
                if (m_Members[i].ClientId != sender)
                    continue;
                var member = m_Members[i];
                member.Ready = !member.Ready;
                m_Members[i] = member;
            }
            if (IsCountingDown && !CanStart())
                Cancel("someone is not ready");
        }

        /// <summary>Host: start the departure countdown if everyone is ready.</summary>
        public void TryStartCountdown()
        {
            if (!IsServer || IsCountingDown || !CanStart())
                return;
            m_CountdownEndsAt.Value = NetworkManager.ServerTime.Time + CountdownSeconds;
            Debug.Log($"[Lobby] departure in {CountdownSeconds} s ({m_Members.Count} players, {DifficultyId})");
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || !IsCountingDown)
                return;
            if (!CanStart())
            {
                Cancel("someone is not ready");
                return;
            }
            if (NetworkManager.ServerTime.Time < m_CountdownEndsAt.Value)
                return;
            m_CountdownEndsAt.Value = -1d;
            ChosenDifficulty = DifficultyId;
            if (GameSession.Instance != null)
                GameSession.Instance.MarkLoading();
            Debug.Log($"[Lobby] off to the job ({DifficultyId})");
            NetworkManager.SceneManager.LoadScene(GameSession.JobScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }

        /// <summary>Test hook: as if this player had dropped out.</summary>
        internal void SimulateLeftForTest(ulong clientId) => RemoveMember(clientId);

        private bool CanStart() => SessionRules.CanStart(Members.Select(m => m.Ready).ToList());

        private void AddMember(ulong clientId)
        {
            if (Members.Any(m => m.ClientId == clientId))
                return;
            var taken = Members.Select(m => m.Number).ToHashSet();
            var number = Enumerable.Range(1, SessionRules.MaxPlayers + 1).First(n => !taken.Contains(n));
            m_Members.Add(new LobbyMember(clientId, false, number, LobbyGate.NameFor(clientId), LobbyGate.VoiceIdFor(clientId)));
            Cancel("a player joined");
        }

        private void RemoveMember(ulong clientId)
        {
            for (var i = m_Members.Count - 1; i >= 0; i--)
                if (m_Members[i].ClientId == clientId)
                    m_Members.RemoveAt(i);
            Cancel("a player left");
        }

        private void Cancel(string why)
        {
            if (!IsCountingDown)
                return;
            m_CountdownEndsAt.Value = -1d;
            Debug.Log($"[Lobby] departure cancelled: {why}");
        }
    }
}
