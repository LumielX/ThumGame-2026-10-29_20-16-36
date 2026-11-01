using SpiritMovers.Job;
using SpiritMovers.Session;
using SpiritMovers.Text;
using TMPro;
using UnityEngine;

namespace SpiritMovers.Voice
{
    /// <summary>A small line in the corner while voice is connecting, lost or unavailable (D-009: the game goes on).</summary>
    public sealed class VoiceStatus : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset m_Font;
        [SerializeField] private ThaiGlyphMap m_GlyphMap;

        private TextMeshProUGUI m_Label;
        private VoiceDirector m_Director;

        /// <summary>The message shown now (localization key), or null.</summary>
        public string LastKey { get; private set; }

        private void Start()
        {
            ThaiFontSupport.Register(m_Font, m_GlyphMap);
            var canvas = SessionUi.CreateCanvas(transform, "VoiceStatusCanvas");
            canvas.GetComponent<Canvas>().sortingOrder = 40;
            m_Label = SessionUi.CreateLabel(canvas, m_Font, 40, 22);
            var rect = m_Label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -16f);
            rect.sizeDelta = new Vector2(620f, 40f);
            m_Label.alignment = TextAlignmentOptions.TopRight;
            m_Director = GetComponent<VoiceDirector>();
            if (m_Director != null)
                m_Director.StateChanged += Show;
            Settings.SettingsApplier.LanguageChanged += Show;
            Show();
        }

        private void OnDestroy()
        {
            if (m_Director != null)
                m_Director.StateChanged -= Show;
            Settings.SettingsApplier.LanguageChanged -= Show;
        }

        private void Update()
        {
            if (KeyNow() != LastKey)
                Show();
        }

        private string KeyNow() => m_Director == null ? null : m_Director.State switch
        {
            VoiceState.Connecting => "voice.connecting",
            VoiceState.Lost => "voice.lost",
            VoiceState.Unavailable => "voice.unavailable",
            VoiceState.On when m_Director.RadioHeld => "radio.on_air",
            _ => null,
        };

        private void Show()
        {
            LastKey = KeyNow();
            if (m_Label != null)
                m_Label.text = LastKey == null ? "" : SessionUi.Text(LastKey);
        }
    }
}
