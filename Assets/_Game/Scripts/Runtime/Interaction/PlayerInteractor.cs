using SpiritMovers.Core.Player;
using SpiritMovers.Ghost;
using SpiritMovers.Player;
using SpiritMovers.Settings;
using SpiritMovers.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace SpiritMovers.Interaction
{
    /// <summary>
    /// Owner-side aiming and use: finds what the player looks at, shows the hint, and turns
    /// interact / throw / rotate presses into requests to the host.
    /// </summary>
    [RequireComponent(typeof(PlayerCarrier))]
    public sealed class PlayerInteractor : NetworkBehaviour
    {
        private const float AimRadius = 0.25f;

        [SerializeField] private TMP_FontAsset m_PromptFont;
        [SerializeField] private ThaiGlyphMap m_PromptGlyphMap;

        private readonly CarryConfig m_Config = CarryConfig.Default;
        private PlayerCarrier m_Carrier;
        private PlayerCapture m_Capture;
        private LocalizedThaiText m_Prompt;
        private string m_PromptKey;

        public IInteractable Focus { get; private set; }

        private void Awake()
        {
            m_Carrier = GetComponent<PlayerCarrier>();
            m_Capture = GetComponent<PlayerCapture>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner && !Application.isBatchMode && m_PromptFont != null)
                m_Prompt = CreatePrompt();
        }

        private void LateUpdate()
        {
            if (!IsSpawned || !IsOwner)
                return;

            if (m_Capture != null && m_Capture.IsCaptured)
            {
                Focus = null;
                ShowPrompt(null); // pressing interact knocks instead (PlayerCapture)
                return;
            }

            var motor = m_Carrier.Motor;
            var held = m_Carrier.Held;
            Focus = held == null ? FindFocus(motor.Head) : null;

            var input = motor.CurrentInput;
            if (input.InteractPressed)
            {
                if (held != null)
                    held.RequestDropRpc();
                else
                    Focus?.Interact(m_Carrier);
            }
            // Use and Throw share the right mouse button: a held tool is used (put it down with Interact to pass it on),
            // anything else is thrown (M4C final review — a walkie flew away when its owner pressed to talk).
            var heldTool = held != null && held.TryGetComponent<IUsable>(out var tool) ? tool : null;
            if (heldTool != null && input.UsePressed)
                heldTool.Use(m_Carrier);
            if (held != null && heldTool == null && input.ThrowPressed && CarryRules.CanThrow(held.Mass, held.CarrierCount, m_Config))
                held.RequestThrowRpc(motor.Head.forward);
            if (held != null && input.RotateSteps != 0f)
                held.RequestRotateRpc(input.RotateSteps);

            ShowPrompt(held == null ? Focus?.PromptKey
                : held.TryGetComponent<IUsable>(out var usable) ? usable.UsePromptKey : "interact.drop");
        }

        private IInteractable FindFocus(Transform head)
        {
            // Rays starting inside the player's own capsule ignore it, so no layer mask is needed yet.
            if (!Physics.SphereCast(head.position, AimRadius, head.forward, out var hit, m_Config.ReachMeters,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return null;
            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            return interactable != null && interactable.CanInteract(m_Carrier) ? interactable : null;
        }

        private void ShowPrompt(string key)
        {
            if (m_Prompt == null || key == m_PromptKey)
                return;
            m_PromptKey = key;
            m_Prompt.gameObject.SetActive(key != null);
            if (key != null)
                m_Prompt.Bind("UI", key, KeyNames.ArgsFor(key));
        }

        private LocalizedThaiText CreatePrompt()
        {
            ThaiFontSupport.Register(m_PromptFont, m_PromptGlyphMap);
            var canvas = new GameObject("InteractionPrompt", typeof(Canvas), typeof(CanvasScaler));
            // A child of the player: it survives scene changes with it and is destroyed with it (M3B carry-over 5).
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var label = new GameObject("Prompt", typeof(RectTransform));
            label.transform.SetParent(canvas.transform, false);
            var text = label.AddComponent<TextMeshProUGUI>();
            text.font = m_PromptFont;
            text.fontSize = 32;
            text.alignment = TextAlignmentOptions.Center;
            text.outlineWidth = 0.25f;
            text.outlineColor = Color.black;
            var rect = label.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.42f);
            rect.sizeDelta = new Vector2(600, 60);
            label.SetActive(false);
            return label.AddComponent<LocalizedThaiText>();
        }
    }
}
