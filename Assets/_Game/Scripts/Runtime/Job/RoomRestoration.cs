using System.Collections.Generic;
using UnityEngine;

namespace SpiritMovers.Job
{
    /// <summary>
    /// "Faded house → ghost at peace → colour returns" (ART_BIBLE 3.2) for one room: renderers inside the room and
    /// the items of its memory slots fade from washed out (saturation × 0.24, value × 0.6) to their real colour
    /// when the ghost accepts the room. Greybox version with a MaterialPropertyBlock; the shared shader comes in M5.
    /// </summary>
    [RequireComponent(typeof(RoomVolume))]
    public sealed class RoomRestoration : MonoBehaviour
    {
        private const float Seconds = 2.5f;
        private const float FadedSaturation = 0.24f;
        private const float FadedValue = 0.6f;
        private static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");

        private readonly List<(Renderer Renderer, Color Colour)> m_Targets = new();
        private MaterialPropertyBlock m_Block;
        private float m_Speed;

        /// <summary>0 = faded, 1 = full colour.</summary>
        public float Value { get; private set; }

        private void Start()
        {
            m_Block = new MaterialPropertyBlock();
            foreach (var renderer in GetComponentsInChildren<Renderer>())
                Add(renderer);
            foreach (var marker in GetComponentsInChildren<MemorySlotMarker>())
                if (marker.Item != null)
                    foreach (var renderer in marker.Item.GetComponentsInChildren<Renderer>())
                        Add(renderer);
            Apply();
        }

        /// <summary>Fade to full colour over a few seconds; <paramref name="instant"/> for late joiners.</summary>
        public void Restore(bool instant = false)
        {
            if (instant)
            {
                Value = 1f;
                Apply();
            }
            else
            {
                m_Speed = 1f / Seconds;
            }
        }

        private void Update()
        {
            if (m_Speed <= 0f || Value >= 1f)
                return;
            Value = Mathf.Min(1f, Value + m_Speed * Time.deltaTime);
            Apply();
        }

        private void Add(Renderer renderer)
        {
            var material = renderer.sharedMaterial;
            if (material != null && material.HasProperty(s_BaseColor))
                m_Targets.Add((renderer, material.GetColor(s_BaseColor)));
        }

        private void Apply()
        {
            if (m_Block == null)
                return;
            foreach (var (renderer, colour) in m_Targets)
            {
                if (renderer == null)
                    continue;
                Color.RGBToHSV(colour, out var h, out var s, out var v);
                var faded = Color.HSVToRGB(h, s * FadedSaturation, v * FadedValue);
                renderer.GetPropertyBlock(m_Block);
                m_Block.SetColor(s_BaseColor, Color.Lerp(faded, colour, Value));
                renderer.SetPropertyBlock(m_Block);
            }
        }
    }
}
