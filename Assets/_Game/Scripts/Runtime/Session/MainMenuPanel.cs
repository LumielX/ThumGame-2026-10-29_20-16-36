using System;
using System.Linq;
using System.Threading.Tasks;
using SpiritMovers.Core.Session;
using SpiritMovers.Core.Text;
using SpiritMovers.Player;
using SpiritMovers.Save;
using SpiritMovers.Settings;
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
        private TMP_InputField m_Name;
        private Transform m_Canvas;
        private object[] m_StatusArgs = Array.Empty<object>();
        private TextMeshProUGUI m_Status;
        private TextMeshProUGUI m_Money;
        private GameSession m_Session;

        /// <summary>Key of the message on the status line (null when empty).</summary>
        public string StatusKey { get; private set; }
        /// <summary>The status line as shown (localized and shaped).</summary>
        public string StatusText => m_Status != null ? m_Status.text : "";
        public bool ButtonsInteractable { get; private set; } = true;
        /// <summary>The saved-money line as shown.</summary>
        public string MoneyText => m_Money != null ? m_Money.text : "";
        /// <summary>The name field (holds shaped Thai).</summary>
        internal TMP_InputField NameField => m_Name;
        /// <summary>Every button label as shown (shaped), one per line.</summary>
        internal string ButtonTexts => m_Canvas == null ? ""
            : string.Join("\n", m_Canvas.GetComponentsInChildren<Button>(true).Select(b => b.GetComponentInChildren<TMP_Text>().text));

        private void Start()
        {
            ThaiFontSupport.Register(m_Font, m_GlyphMap);
            GameCursor.Capture(false);
            LobbyState.ClearRoster();
            m_Session = GameSession.Instance;
            if (m_Session != null)
                m_Session.Failed += OnFailed;
            Build();

            if (GameSession.PendingMessageKey != null)
            {
                ShowStatus(GameSession.PendingMessageKey, GameSession.PendingArgs ?? new object[] { GameSession.PendingPay });
                GameSession.PendingMessageKey = null;
                GameSession.PendingArgs = null;
                GameSession.PendingPay = 0;
            }
            else if (GameData.MessageKey != null)
            {
                ShowStatus(GameData.MessageKey);
                GameData.MessageKey = null;
            }
            GameData.Changed += ShowMoney;
            SettingsApplier.LanguageChanged += Relabel;
            ShowMoney();
        }

        /// <summary>The language changed: build the menu again, keeping what was typed and shown.</summary>
        private void Relabel()
        {
            var code = m_Code != null ? m_Code.text : "";
            if (m_Canvas != null)
                Destroy(m_Canvas.gameObject);
            Build();
            m_Code.SetTextWithoutNotify(code);
            foreach (var button in m_Buttons)
                button.interactable = ButtonsInteractable;
            if (StatusKey != null)
                ShowStatus(StatusKey, m_StatusArgs);
            ShowMoney();
        }

        private void ShowMoney()
        {
            if (m_Money != null)
                m_Money.text = SessionUi.Text("menu.money", GameData.Money);
            if (GameData.MessageKey == "save.failed")
            {
                ShowStatus(GameData.MessageKey);
                GameData.MessageKey = null;
            }
        }

        private void OnDestroy()
        {
            if (m_Session != null)
                m_Session.Failed -= OnFailed;
            GameData.Changed -= ShowMoney;
            SettingsApplier.LanguageChanged -= Relabel;
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

        internal void ClickSettings()
        {
            var settings = FindAnyObjectByType<SettingsPanel>();
            if (settings != null)
                settings.Open();
        }

        internal void ClickSolo()
        {
            SetBusy(true);
            m_Session.StartSolo();
        }

        private void Build()
        {
            var canvas = SessionUi.CreateCanvas(transform, "MenuCanvas");
            m_Canvas = canvas;
            var column = SessionUi.CreateColumn(canvas, new Vector2(0.5f, 0.5f), new Vector2(620, 720));
            var title = SessionUi.CreateLabel(column, m_Font, 80, 48);
            title.text = Application.productName;
            title.alignment = TextAlignmentOptions.Center;
            m_Money = SessionUi.CreateLabel(column, m_Font, 40, 26);
            m_Money.alignment = TextAlignmentOptions.Center;
            m_Name = SessionUi.CreateNameField(column, m_Font, "menu.name_hint");
            m_Name.SetTextWithoutNotify(ThaiShaper.Shape(GameData.PlayerName));
            m_Name.onEndEdit.AddListener(typed =>
            {
                GameData.PlayerName = ThaiShaper.Unshape(typed);
                m_Name.SetTextWithoutNotify(ThaiShaper.Shape(GameData.PlayerName));
            });
            var host = SessionUi.CreateButton(column, m_Font, "menu.host", ClickHost);
            m_Code = SessionUi.CreateCodeField(column, m_Font, "menu.code_hint");
            var join = SessionUi.CreateButton(column, m_Font, "menu.join", () => ClickJoin(m_Code.text));
            var solo = SessionUi.CreateButton(column, m_Font, "menu.solo", ClickSolo);
            SessionUi.CreateButton(column, m_Font, "menu.settings", ClickSettings);
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
            m_StatusArgs = args;
            if (m_Status != null)
                m_Status.text = SessionUi.Text(key, args);
        }
    }
}
