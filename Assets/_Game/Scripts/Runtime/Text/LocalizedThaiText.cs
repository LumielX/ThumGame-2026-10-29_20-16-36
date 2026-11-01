using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace SpiritMovers.Text
{
    /// <summary>
    /// Shows a localized string on a TextMeshPro text through the Thai shaper (D-035).
    /// Use instead of the Localization package's LocalizeStringEvent, which would set unshaped text.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedThaiText : MonoBehaviour
    {
        [SerializeField] private LocalizedString m_String = new();

        private TMP_Text m_Target;
        private bool m_Subscribed;

        /// <summary>
        /// Points the text at a table entry (with values for its "{0}"…); updates now and whenever the language changes.
        /// </summary>
        public void Bind(string table, string key, object[] arguments = null)
        {
            Unsubscribe();
            m_String = new LocalizedString(table, key) { Arguments = arguments };
            if (isActiveAndEnabled)
                Subscribe();
        }

        private void Awake() => m_Target = GetComponent<TMP_Text>();

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (m_Subscribed || m_String.IsEmpty)
                return;
            m_String.StringChanged += Apply;
            m_Subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!m_Subscribed)
                return;
            m_String.StringChanged -= Apply;
            m_Subscribed = false;
        }

        private void Apply(string value) => m_Target.SetThaiText(value);
    }
}
