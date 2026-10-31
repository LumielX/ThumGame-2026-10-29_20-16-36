using System.Collections.Generic;
using System.Linq;
using SpiritMovers.Core.Session;
using SpiritMovers.Player;
using SpiritMovers.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace SpiritMovers.Session
{
    /// <summary>
    /// The van's notice board on screen: room code, who is in and ready, the job, the crew-size label and the
    /// departure countdown. Also turns the local player's R (ready) and the host's Enter (depart) into lobby requests.
    /// </summary>
    public sealed class LobbyPanel : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset m_Font;
        [SerializeField] private ThaiGlyphMap m_GlyphMap;

        private TextMeshProUGUI m_Text;
        private LobbyState m_Lobby;

        /// <summary>The board as last drawn (localized and shaped).</summary>
        public string LastText { get; private set; } = "";

        private void Start()
        {
            ThaiFontSupport.Register(m_Font, m_GlyphMap);
            var canvas = SessionUi.CreateCanvas(transform, "LobbyCanvas");
            var column = SessionUi.CreateColumn(canvas, new Vector2(0f, 1f), new Vector2(720, 700));
            m_Text = SessionUi.CreateLabel(column, m_Font, 640, 28);
        }

        private void LateUpdate()
        {
            if (m_Lobby == null)
                m_Lobby = FindAnyObjectByType<LobbyState>();
            if (m_Lobby == null || !m_Lobby.IsSpawned)
                return;

            var local = PlayerMotor.Local;
            if (local != null)
            {
                if (local.CurrentInput.ReadyPressed)
                    m_Lobby.ToggleReadyRpc();
                if (local.CurrentInput.ConfirmPressed && NetworkManager.Singleton.IsServer)
                    m_Lobby.TryStartCountdown();
            }

            var session = GameSession.Instance;
            Render(m_Lobby.Members, session != null ? session.JoinCode : null, session == null || session.IsSolo,
                m_Lobby.IsCountingDown ? Mathf.CeilToInt(m_Lobby.SecondsLeft) : -1);
        }

        /// <summary>Draw the board for these members (tests call it directly).</summary>
        internal void Render(IReadOnlyList<LobbyMember> members, string joinCode, bool solo, int countdown = -1)
        {
            var lines = new List<string> { SessionUi.Text("lobby.title") };
            if (!solo && !string.IsNullOrEmpty(joinCode))
                lines.Add(SessionUi.Text("lobby.code", joinCode));
            foreach (var member in members.OrderBy(m => m.Number))
                lines.Add(SessionUi.Text("lobby.member", member.Number, SessionUi.Text(member.Ready ? "lobby.ready" : "lobby.not_ready")));
            if (SessionRules.SizeLabel(members.Count) == CrewSize.Experimental)
                lines.Add(SessionUi.Text("lobby.experimental"));
            lines.Add(SessionUi.Text("lobby.job"));
            lines.Add(SessionUi.Text("lobby.hard_locked"));
            lines.Add(countdown >= 0 ? SessionUi.Text("lobby.countdown", countdown)
                : SessionUi.Text(SessionRules.CanStart(members.Select(m => m.Ready).ToList()) ? "lobby.hint_start" : "lobby.hint_ready"));
            LastText = string.Join("\n", lines);
            if (m_Text != null)
                m_Text.text = LastText;
        }
    }
}
