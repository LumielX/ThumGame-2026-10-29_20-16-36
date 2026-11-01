using System.Linq;
using SpiritMovers.Player;
using SpiritMovers.Session;
using SpiritMovers.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpiritMovers.Voice
{
    /// <summary>
    /// "Players" in the Esc menu (D-009): everyone else in the room, each with a mute button and a volume slider.
    /// The choices last for the session (VoiceDirector keeps them across van and house), not in the save.
    /// </summary>
    public sealed class PlayerVoicePanel : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset m_Font;
        [SerializeField] private ThaiGlyphMap m_GlyphMap;

        private Transform m_Canvas;
        private int m_ClosedFrame = -1;

        public bool IsOpen { get; private set; }
        /// <summary>The Esc that closed the page must not also close the pause menu under it.</summary>
        public bool ClosedThisFrame => m_ClosedFrame == Time.frameCount;

        /// <summary>Every visible text on the page (shaped), for tests.</summary>
        internal string LastText => m_Canvas == null ? ""
            : string.Join("\n", m_Canvas.GetComponentsInChildren<TMP_Text>(false).Select(t => t.text));

        private void Start() => ThaiFontSupport.Register(m_Font, m_GlyphMap);

        public void Open()
        {
            IsOpen = true;
            Build();
        }

        public void Close()
        {
            if (!IsOpen)
                return;
            IsOpen = false;
            m_ClosedFrame = Time.frameCount;
            if (m_Canvas != null)
                Destroy(m_Canvas.gameObject);
            m_Canvas = null;
        }

        internal void SetMuted(string voiceId, bool muted) => VoiceDirector.Instance?.SetPlayerMuted(voiceId, muted);

        internal void SetVolume(string voiceId, float gain01) => VoiceDirector.Instance?.SetPlayerVolume(voiceId, gain01);

        private void Update()
        {
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Close();
        }

        private void Build()
        {
            if (m_Canvas != null)
                Destroy(m_Canvas.gameObject);
            m_Canvas = SessionUi.CreateCanvas(transform, "PlayersCanvas");
            m_Canvas.GetComponent<Canvas>().sortingOrder = 60;
            var column = SessionUi.CreateColumn(m_Canvas, new Vector2(0.5f, 0.5f), new Vector2(900, 560));
            column.GetComponent<Image>().color = new Color(0.12f, 0.09f, 0.07f, 1f);
            SessionUi.CreateLabel(column, m_Font, 50, 34).text = SessionUi.Text("pause.players");
            var voice = VoiceDirector.Instance;
            var me = voice != null && voice.State == VoiceState.On ? voice.Service.PlayerId : GameSession.Instance?.PlayerId;
            foreach (var member in LobbyState.Roster.Where(m => m.VoiceId.ToString() != me && !m.VoiceId.IsEmpty))
            {
                var id = member.VoiceId.ToString();
                var row = SessionUi.CreateRow(column, 52);
                var name = SessionUi.CreateLabel(row, m_Font, 52, 26);
                name.text = member.Name.IsEmpty ? SessionUi.Text("lobby.player_n", member.Number) : member.Name.ToString();
                var muted = voice != null && voice.IsPlayerMuted(id);
                Button mute = null;
                mute = SessionUi.CreateButton(row, m_Font, muted ? "voice.unmute" : "voice.mute", () =>
                {
                    var now = !(VoiceDirector.Instance?.IsPlayerMuted(id) ?? false);
                    SetMuted(id, now);
                    mute.GetComponentInChildren<TextMeshProUGUI>().text = SessionUi.Text(now ? "voice.unmute" : "voice.mute");
                });
                SessionUi.CreateSlider(row, 0f, 1f, voice != null ? voice.PlayerVolume(id) : 1f, v => SetVolume(id, v));
            }
            SessionUi.CreateButton(column, m_Font, "players.close", Close);
        }
    }
}
