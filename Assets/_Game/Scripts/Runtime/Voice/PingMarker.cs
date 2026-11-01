using SpiritMovers.Core.Text;
using TMPro;
using UnityEngine;

namespace SpiritMovers.Voice
{
    /// <summary>An amber marker where someone pinged, with their name, gone after a few seconds (GDD 16 HUD "Ping").</summary>
    public sealed class PingMarker : MonoBehaviour
    {
        [SerializeField] private TextMeshPro m_Label;

        /// <summary>The name shown (shaped).</summary>
        public string Label => m_Label != null ? m_Label.text : "";

        public void Show(string sender, float seconds)
        {
            if (m_Label != null)
                m_Label.text = ThaiShaper.Shape(sender);
            Destroy(gameObject, seconds);
        }

        private void LateUpdate()
        {
            // The name faces whoever is looking.
            var view = Camera.main;
            if (m_Label != null && view != null)
                m_Label.transform.rotation = Quaternion.LookRotation(m_Label.transform.position - view.transform.position);
        }
    }
}
