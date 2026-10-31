using SpiritMovers.Player;
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

        public bool IsOpen { get; private set; }

        private void Start()
        {
            m_Session = GetComponent<GameSession>();
            ThaiFontSupport.Register(m_Font, m_GlyphMap);
            var canvas = SessionUi.CreateCanvas(transform, "PauseCanvas");
            canvas.GetComponent<Canvas>().sortingOrder = 50;
            var column = SessionUi.CreateColumn(canvas, new Vector2(0.5f, 0.5f), new Vector2(560, 260));
            SessionUi.CreateButton(column, m_Font, "pause.resume", ClickResume);
            SessionUi.CreateButton(column, m_Font, "pause.leave", ClickLeave);
            m_Panel = column.gameObject;
            m_Panel.SetActive(false);
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
            if (local.CurrentInput.PausePressed)
                SetOpen(!IsOpen);
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
            Cursor.lockState = open || local == null || Application.isBatchMode ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open || local == null;
        }
    }
}
