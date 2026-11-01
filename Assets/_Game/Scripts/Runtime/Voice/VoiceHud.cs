using System.Collections.Generic;
using System.Linq;
using SpiritMovers.Core.Voice;
using SpiritMovers.Session;
using SpiritMovers.Text;
using TMPro;
using UnityEngine;

namespace SpiritMovers.Voice
{
    /// <summary>
    /// Who is talking right now (GDD 16 HUD "ไอคอนพูด/วิทยุ", D-009), as a short list in the lower left: "● name", with
    /// "[วิทยุ]" for voices coming over the radio. Names come from the roster by UGS player id. Heads-up icons over the
    /// characters come with the real HUD in M5.
    /// </summary>
    public sealed class VoiceHud : MonoBehaviour
    {
        private const float RefreshSeconds = 0.2f;
        private const float RadioLevel = 0.02f;

        [SerializeField] private TMP_FontAsset m_Font;
        [SerializeField] private ThaiGlyphMap m_GlyphMap;

        private TextMeshProUGUI m_Label;
        private float m_NextRefresh;

        /// <summary>The list as shown (shaped), for tests.</summary>
        internal string LastText { get; private set; } = "";

        private void Start()
        {
            ThaiFontSupport.Register(m_Font, m_GlyphMap);
            var canvas = SessionUi.CreateCanvas(transform, "VoiceHudCanvas");
            canvas.GetComponent<Canvas>().sortingOrder = 30;
            m_Label = SessionUi.CreateLabel(canvas, m_Font, 220, 24);
            var rect = m_Label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(24f, 140f);
            rect.sizeDelta = new Vector2(420f, 220f);
            m_Label.alignment = TextAlignmentOptions.BottomLeft;
        }

        private void Update()
        {
            if (Time.unscaledTime < m_NextRefresh)
                return;
            m_NextRefresh = Time.unscaledTime + RefreshSeconds;
            LastText = string.Join("\n", Lines());
            if (m_Label != null)
                m_Label.text = LastText;
        }

        private static IEnumerable<string> Lines()
        {
            var voice = VoiceDirector.Instance;
            if (voice == null || voice.State != VoiceState.On)
                yield break;
            var radio = voice.Space == VoiceSpace.House ? VoiceRules.ChannelsFor(VoiceSpace.House, voice.RoomCode)[1].Name : null;
            var listener = radio != null ? FindAnyObjectByType<RadioListener>() : null;
            var seen = new HashSet<string>();
            foreach (var p in voice.Service.Participants)
            {
                // A radio voice heard through a tap is silent in Vivox's own mix: its level comes from the tap (M2 3.1).
                var filter = p.Channel == radio && listener != null ? listener.FilterFor(p.PlayerId) : null;
                var overRadio = filter != null && filter.Level > RadioLevel;
                if (!(p.Speaking || overRadio) || !seen.Add(p.PlayerId))
                    continue;
                var name = LobbyState.NameForVoice(p.PlayerId);
                var line = "● " + (string.IsNullOrEmpty(name) ? SessionUi.Text("lobby.player_n", "?") : name);
                yield return overRadio || p.Channel == radio ? $"{line} {SessionUi.Text("voice.radio_tag")}" : line;
            }
        }
    }
}
