using SpiritMovers.Player;
using SpiritMovers.Settings;
using SpiritMovers.Text;
using TMPro;
using UnityEngine;

namespace SpiritMovers.Session
{
    /// <summary>
    /// Esc during a session: back to the game, or leave the room (the job goes on for the others — GDD 9). The game
    /// keeps running underneath (it is online); the local player just stops walking while the menu is open.
    /// Lives on the persistent GameNetwork object.
    /// </summary>
    [RequireComponent(typeof(GameSession))]
    public sealed class PauseMenu : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset m_Font;
        [SerializeField] private ThaiGlyphMap m_GlyphMap;

        private GameSession m_Session;
        private GameObject m_Panel;
        private Transform m_Canvas;
        private SettingsPanel m_Settings;
        private Voice.PlayerVoicePanel m_Players;
        private Voice.ChatPanel m_Chat;

        public bool IsOpen { get; private set; }

        private void Start()
        {
            m_Session = GetComponent<GameSession>();
            m_Settings = GetComponent<SettingsPanel>();
            m_Players = GetComponent<Voice.PlayerVoicePanel>();
            m_Chat = GetComponent<Voice.ChatPanel>();
            ThaiFontSupport.Register(m_Font, m_GlyphMap);
            Build();
            m_Panel.SetActive(false);
            SettingsApplier.LanguageChanged += Relabel;
        }

        private void OnDestroy() => SettingsApplier.LanguageChanged -= Relabel;

        private void Relabel()
        {
            Destroy(m_Canvas.gameObject);
            Build();
            m_Panel.SetActive(IsOpen);
        }

        private void Build()
        {
            var canvas = SessionUi.CreateCanvas(transform, "PauseCanvas");
            m_Canvas = canvas;
            canvas.GetComponent<Canvas>().sortingOrder = 50;
            var column = SessionUi.CreateColumn(canvas, new Vector2(0.5f, 0.5f), new Vector2(560, 420));
            SessionUi.CreateButton(column, m_Font, "pause.resume", ClickResume);
            SessionUi.CreateButton(column, m_Font, "pause.players", ClickPlayers);
            SessionUi.CreateButton(column, m_Font, "menu.settings", ClickSettings);
            SessionUi.CreateButton(column, m_Font, "pause.leave", ClickLeave);
            m_Panel = column.gameObject;
        }

        private void Update()
        {
            var local = PlayerMotor.Local;
            if (local == null)
            {
                if (IsOpen)
                    SetOpen(false);
                return;
            }
            // Esc belongs to the settings page while it is open (it closes the page, not this menu).
            if (m_Settings != null && (m_Settings.IsOpen || m_Settings.ClosedThisFrame))
                return;
            if (m_Players != null && (m_Players.IsOpen || m_Players.ClosedThisFrame))
                return;
            // Esc while typing a chat message cancels the message, not opens this menu.
            if (m_Chat != null && (m_Chat.IsTyping || m_Chat.ClosedThisFrame))
                return;
            if (local.CurrentInput.PausePressed)
                SetOpen(!IsOpen);
        }

        internal void ClickPlayers()
        {
            if (m_Players != null)
                m_Players.Open();
        }

        internal void ClickSettings()
        {
            if (m_Settings != null)
                m_Settings.Open();
        }

        internal void ClickResume() => SetOpen(false);

        internal void ClickLeave()
        {
            SetOpen(false);
            _ = m_Session.LeaveAsync();
        }

        private void SetOpen(bool open)
        {
            IsOpen = open;
            if (m_Panel != null)
                m_Panel.SetActive(open);
            var local = PlayerMotor.Local;
            if (local != null)
                local.InputEnabled = !open;
            // The motor keeps reading Esc while its gameplay input is off, so the menu can still be closed with Esc.
            GameCursor.Capture(!open && local != null);
        }
    }
}
