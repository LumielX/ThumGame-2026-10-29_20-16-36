using SpiritMovers.Core.Voice;
using SpiritMovers.Job;
using SpiritMovers.Player;
using SpiritMovers.Session;
using SpiritMovers.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpiritMovers.Voice
{
    /// <summary>
    /// The quick-message wheel (GDD 10, D-013): hold its key, point the mouse at one of six messages, let go to send.
    /// Slot 0 is to the right and the rest follow counter-clockwise; letting go near the middle sends nothing.
    /// </summary>
    public sealed class QuickWheel : MonoBehaviour
    {
        private const float WheelRadius = 220f;
        private const float DeadZone = 0.2f;

        [SerializeField] private TMP_FontAsset m_Font;
        [SerializeField] private ThaiGlyphMap m_GlyphMap;

        private Transform m_Canvas;
        private Vector2 m_Pointer;
        private bool m_PointerFromTest;

        public bool IsOpen { get; private set; }

        /// <summary>Which message a direction from the middle of the wheel picks (null in the middle).</summary>
        public static QuickMessage? PickAt(Vector2 direction)
        {
            if (direction.magnitude < DeadZone)
                return null;
            var degrees = Mathf.Repeat(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 30f, 360f);
            return (QuickMessage)Mathf.FloorToInt(degrees / 60f);
        }

        /// <summary>Tests: point the wheel this way (as the mouse would, in wheel radii).</summary>
        internal void PointForTest(Vector2 direction)
        {
            m_Pointer = direction;
            m_PointerFromTest = true;
        }

        private void Start() => ThaiFontSupport.Register(m_Font, m_GlyphMap);

        private void Update()
        {
            var local = PlayerMotor.Local;
            var held = local != null && local.CurrentInput.QuickWheelHeld;
            if (held && !IsOpen)
                Open();
            else if (!held && IsOpen)
                Release();
            if (IsOpen && !m_PointerFromTest && Mouse.current != null)
            {
                var centre = new Vector2(Screen.width, Screen.height) * 0.5f;
                m_Pointer = (Mouse.current.position.ReadValue() - centre) / WheelRadius;
            }
        }

        private void Open()
        {
            IsOpen = true;
            m_Pointer = Vector2.zero;
            m_PointerFromTest = false;
            GameCursor.Capture(false);
            m_Canvas = SessionUi.CreateCanvas(transform, "QuickWheelCanvas");
            m_Canvas.GetComponent<Canvas>().sortingOrder = 45;
            for (var i = 0; i < 6; i++)
            {
                var angle = i * 60f * Mathf.Deg2Rad;
                var label = SessionUi.CreateLabel(m_Canvas, m_Font, 60, 28);
                label.alignment = TextAlignmentOptions.Center;
                label.text = SessionUi.Text(ChatText.KeyFor((QuickMessage)i));
                var rect = label.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(320f, 60f);
                rect.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * WheelRadius;
            }
        }

        private void Release()
        {
            IsOpen = false;
            if (m_Canvas != null)
                Destroy(m_Canvas.gameObject);
            var local = PlayerMotor.Local;
            GameCursor.Capture(local != null && local.InputEnabled);
            var pick = PickAt(m_Pointer);
            if (pick.HasValue && CommsHub.Current != null)
                CommsHub.Current.SendQuick(pick.Value);
        }
    }
}
