using System;
using System.Threading.Tasks;
using SpiritMovers.Core.Session;
using SpiritMovers.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpiritMovers.Session
{
    /// <summary>
    /// The main menu (GDD 9, 16): host a room, join one by code, play solo, settings (M4B), quit — and a status line
    /// for "connecting…", translated errors, and the message left by the last session (e.g. the host left).
    /// </summary>
    public sealed class MainMenuPanel : MonoBehaviour
    {
        private const string Connecting = "menu.connecting";

        [SerializeField] private TMP_FontAsset m_Font;
        [SerializeField] private ThaiGlyphMap m_GlyphMap;

        private Button[] m_Buttons = Array.Empty<Button>();
        private TMP_InputField m_Code;
        private TextMeshProUGUI m_Status;
        private GameSession m_Session;

        /// <summary>Key of the message on the status line (null when empty).</summary>
        public string StatusKey { get; private set; }
        /// <summary>The status line as shown (localized and shaped).</summary>
        public string StatusText => m_Status != null ? m_Status.text : "";
        public bool ButtonsInteractable { get; private set; } = true;

        private void Start()
        {
            ThaiFontSupport.Register(m_Font, m_GlyphMap);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            m_Session = GameSession.Instance;
            if (m_Session != null)
                m_Session.Failed += OnFailed;
            Build();

            if (GameSession.PendingMessageKey != null)
            {
                ShowStatus(GameSession.PendingMessageKey, GameSession.PendingPay);
                GameSession.PendingMessageKey = null;
                GameSession.PendingPay = 0;
            }
        }

        private void OnDestroy()
        {
            if (m_Session != null)
                m_Session.Failed -= OnFailed;
        }

        internal void ClickHost()
        {
            SetBusy(true);
            Run(m_Session.HostAsync());
        }

        internal void ClickJoin(string code)
        {
            if (SessionRules.NormalizeCode(code) == null)
            {
                ShowStatus(SessionErrors.Code);
                return;
            }
            SetBusy(true);
            Run(m_Session.JoinAsync(code));
        }

        internal void ClickSolo()
        {
            SetBusy(true);
            m_Session.StartSolo();
        }

        private void Build()
        {
            var canvas = SessionUi.CreateCanvas(transform, "MenuCanvas");
            var column = SessionUi.CreateColumn(canvas, new Vector2(0.5f, 0.5f), new Vector2(620, 720));
            var title = SessionUi.CreateLabel(column, m_Font, 80, 48);
            title.text = Application.productName;
            title.alignment = TextAlignmentOptions.Center;
            var host = SessionUi.CreateButton(column, m_Font, "menu.host", ClickHost);
            m_Code = SessionUi.CreateCodeField(column, m_Font, "menu.code_hint");
            var join = SessionUi.CreateButton(column, m_Font, "menu.join", () => ClickJoin(m_Code.text));
            var solo = SessionUi.CreateButton(column, m_Font, "menu.solo", ClickSolo);
            var settings = SessionUi.CreateButton(column, m_Font, "menu.settings", () => { });
            settings.interactable = false; // M4B
            var quit = SessionUi.CreateButton(column, m_Font, "menu.quit", Application.Quit);
            m_Status = SessionUi.CreateLabel(column, m_Font, 120, 26);
            m_Buttons = new[] { host, join, solo, quit };
        }

        private async void Run(Task task)
        {
            try
            {
                await task;
            }
            catch (Exception e)
            {
                // GameSession reports its own failures through Failed; anything else is a bug worth seeing.
                Debug.LogException(e);
                SetBusy(false);
            }
        }

        private void OnFailed(string key, object[] args)
        {
            SetBusy(false);
            ShowStatus(key, args);
        }

        private void SetBusy(bool busy)
        {
            ButtonsInteractable = !busy;
            foreach (var button in m_Buttons)
                button.interactable = !busy;
            if (m_Code != null)
                m_Code.interactable = !busy;
            if (busy)
                ShowStatus(Connecting);
        }

        private void ShowStatus(string key, params object[] args)
        {
            StatusKey = key;
            if (m_Status != null)
                m_Status.text = SessionUi.Text(key, args);
        }
    }
}
