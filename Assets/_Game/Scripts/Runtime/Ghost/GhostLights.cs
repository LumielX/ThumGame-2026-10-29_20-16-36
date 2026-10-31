using System.Collections;
using UnityEngine;

namespace SpiritMovers.Ghost
{
    /// <summary>The house's main light; an angry ghost makes it go dark for a few seconds (GDD 6.2 stage 2).</summary>
    [RequireComponent(typeof(Light))]
    public sealed class GhostLights : MonoBehaviour
    {
        private Light m_Light;
        private float m_BaseIntensity;
        private Coroutine m_Dimming;

        /// <summary>Current brightness relative to normal (1 = normal).</summary>
        public float Factor { get; private set; } = 1f;

        private void Awake()
        {
            m_Light = GetComponent<Light>();
            m_BaseIntensity = m_Light.intensity;
        }

        public void Dim(float seconds, float factor)
        {
            if (m_Dimming != null)
                StopCoroutine(m_Dimming);
            m_Dimming = StartCoroutine(DimFor(seconds, factor));
        }

        private IEnumerator DimFor(float seconds, float factor)
        {
            SetFactor(factor);
            yield return new WaitForSeconds(seconds);
            SetFactor(1f);
            m_Dimming = null;
        }

        private void SetFactor(float factor)
        {
            Factor = factor;
            m_Light.intensity = m_BaseIntensity * factor;
        }
    }
}
