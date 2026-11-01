using System.Collections.Generic;
using System.Linq;
using SpiritMovers.Core.Text;
using SpiritMovers.Core.Voice;
using SpiritMovers.Net;
using SpiritMovers.Player;
using SpiritMovers.Save;
using SpiritMovers.Session;
using SpiritMovers.Settings;
using SpiritMovers.Text;
using TMPro;
using UnityEngine;

namespace SpiritMovers.Voice
{
    /// <summary>
    /// Text chat for the team (D-013 J3) over Vivox text: the van's channel, or the radio's during a job. The chat key
    /// opens a Thai-shaped field (no walking while typing), Enter sends, Esc cancels. The last 6 lines stay for 10 s.
    /// Quick messages (Task 8) show here too. Solo has no chat.
    /// </summary>
    public sealed class ChatPanel : MonoBehaviour
    {
        private const int MaxLines = 6;
        private const float LineSeconds = 10f;
        private const float SendInterval = 0.5f;

        [SerializeField] private TMP_FontAsset m_Font;
        [SerializeField] private ThaiGlyphMap m_GlyphMap;

        private readonly List<(string Text, float At)> m_Lines = new();
        private TextMeshProUGUI m_Feed;
        private TMP_InputField m_Field;
        private IVoiceService m_Subscribed;
        private bool m_OpenNextFrame;
        private float m_NextSendAt;
        private int m_ClosedFrame = -1;

        public bool IsTyping { get; private set; }
        /// <summary>The Esc that closed the field must not also open the pause menu.</summary>
        public bool ClosedThisFrame => m_ClosedFrame == Time.frameCount;

        /// <summary>The chat box as shown (shaped), for tests.</summary>
        internal string LastText { get; private set; } = "";

        private void Start()
        {
            ThaiFontSupport.Register(m_Font, m_GlyphMap);
            var canvas = SessionUi.CreateCanvas(transform, "ChatCanvas");
            canvas.GetComponent<Canvas>().sortingOrder = 35;
            m_Feed = SessionUi.CreateLabel(canvas, m_Font, 220, 22);
            var rect = m_Feed.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
            // Low on the left, under the van's job board and above the list of who is talking.
            rect.anchoredPosition = new Vector2(24f, 110f);
            rect.sizeDelta = new Vector2(640f, 140f);
            m_Feed.alignment = TextAlignmentOptions.BottomLeft;

            var column = SessionUi.CreateColumn(canvas, new Vector2(0.5f, 0f), new Vector2(680, 96));
            column.anchoredPosition = new Vector2(0f, 24f);
            m_Field = SessionUi.CreateNameField(column, m_Font, "chat.placeholder");
            m_Field.characterLimit = ChatText.MaxLength + 20;
            m_Field.onSubmit.AddListener(typed => Submit(ThaiShaper.Unshape(typed)));
            column.gameObject.SetActive(false);
        }

        private void OnDestroy() => Subscribe(null);

        private void Update()
        {
            var voice = VoiceDirector.Instance;
            Subscribe(voice != null && voice.State != VoiceState.Off ? voice.Service : null);
            var online = voice != null && voice.State != VoiceState.Off;
            var local = PlayerMotor.Local;

            if (m_OpenNextFrame)
            {
                m_OpenNextFrame = false;
                Open();
            }
            else if (IsTyping && (!online || local == null || local.CurrentInput.PausePressed))
            {
                Close();
            }
            else if (!IsTyping && online && local != null && local.CurrentInput.ChatPressed && local.InputEnabled)
            {
                // A frame later, so the key that opened the field is not typed into it.
                m_OpenNextFrame = true;
            }

            Refresh();
        }

        private void Refresh()
        {
            var voice = VoiceDirector.Instance;
            var online = voice != null && voice.State != VoiceState.Off;
            m_Lines.RemoveAll(l => Time.unscaledTime - l.At > LineSeconds);
            var lines = m_Lines.Select(l => l.Text).ToList();
            if (online && !IsTyping)
                lines.Add(SessionUi.Text("chat.hint", KeyNames.For("Gameplay/Chat")));
            LastText = string.Join("\n", lines);
            if (m_Feed != null)
                m_Feed.text = LastText;
        }

        /// <summary>Tests and bots: type <paramref name="text"/> and press Enter.</summary>
        internal void Type(string text)
        {
            if (!IsTyping)
                Open();
            m_Field.SetTextWithoutNotify(ThaiShaper.Shape(text));
            Submit(text);
        }

        /// <summary>A line from somewhere else (a quick message): "name: text".</summary>
        internal void Show(string sender, string text) => AddLine($"{sender}: {ChatText.Clean(text)}");

        private void Open()
        {
            IsTyping = true;
            m_Field.transform.parent.gameObject.SetActive(true);
            m_Field.SetTextWithoutNotify("");
            m_Field.ActivateInputField();
            if (PlayerMotor.Local != null)
                PlayerMotor.Local.InputEnabled = false;
            GameCursor.Capture(false);
        }

        private void Close()
        {
            IsTyping = false;
            m_ClosedFrame = Time.frameCount;
            m_Field.DeactivateInputField();
            m_Field.transform.parent.gameObject.SetActive(false);
            var local = PlayerMotor.Local;
            var paused = FindAnyObjectByType<PauseMenu>()?.IsOpen ?? false;
            if (local != null && !paused)
            {
                local.InputEnabled = true;
                GameCursor.Capture(true);
            }
        }

        private void Submit(string typed)
        {
            var text = ChatText.Clean(typed);
            Close();
            if (text.Length == 0 || Time.unscaledTime < m_NextSendAt)
                return;
            var voice = VoiceDirector.Instance;
            var channel = voice != null ? VoiceRules.ChatChannel(voice.Space, voice.RoomCode) : null;
            if (voice == null || voice.State != VoiceState.On || channel == null)
            {
                AddLine(SessionUi.Text("chat.unavailable"));
                return;
            }
            m_NextSendAt = Time.unscaledTime + SendInterval;
            _ = Send(voice.Service, channel, text);
        }

        private static async System.Threading.Tasks.Task Send(IVoiceService service, string channel, string text)
        {
            try
            {
                await service.SendTextAsync(channel, text);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Chat] not sent: {e.Message}");
            }
        }

        private void OnText(string sender, string channel, string text)
        {
            var voice = VoiceDirector.Instance;
            var self = voice != null && sender == voice.Service.PlayerId;
            var name = self ? GameData.PlayerName : LobbyState.NameForVoice(sender);
            if (string.IsNullOrEmpty(name))
                name = Job.JobUi.Format("lobby.player_n", "?");
            Show(name, text);
        }

        private void AddLine(string line)
        {
            m_Lines.Add((ThaiShaper.Shape(line), Time.unscaledTime));
            while (m_Lines.Count > MaxLines)
                m_Lines.RemoveAt(0);
            Refresh();
        }

        private void Subscribe(IVoiceService service)
        {
            if (service == m_Subscribed)
                return;
            if (m_Subscribed != null)
                m_Subscribed.TextReceived -= OnText;
            m_Subscribed = service;
            if (m_Subscribed != null)
                m_Subscribed.TextReceived += OnText;
        }
    }
}
