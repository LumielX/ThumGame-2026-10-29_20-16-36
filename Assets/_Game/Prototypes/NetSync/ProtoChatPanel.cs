using System.Collections.Generic;
using SpiritMovers.Core.Text;
using SpiritMovers.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace SpiritMovers.Prototypes.NetSync
{
    /// <summary>
    /// Throwaway text chat (D-013 J3) for the Thai IME test: Enter opens the field, Enter sends, Esc cancels.
    /// Received lines and the field being typed in both go through the Thai shaper (D-035).
    /// Hints come from the UI string table; F2 switches Thai/English (M2 item 5).
    /// </summary>
    public sealed class ProtoChatPanel : MonoBehaviour
    {
        private const int MaxLines = 8;

        private readonly List<string> m_Lines = new();
        private VoiceProto m_Voice;
        private TMP_InputField m_Input;
        private TextMeshProUGUI m_Log;
        private int m_ClosedFrame = -1;

        public void Init(TMP_FontAsset font, ThaiGlyphMap glyphMap, VoiceProto voice)
        {
            m_Voice = voice;
            ThaiFontSupport.Register(font, glyphMap);
            voice.MessageReceived += (sender, text) => AddLine($"{sender}: {text}");
            BuildUi(font);

            // Screenshot runs: show what an unsent Thai draft looks like in the field.
            var preview = NetSyncProtoRunner.Arg("-proto-chat-preview");
            if (preview != null)
            {
                Open();
                m_Input.text = preview;
            }
        }

        private void BuildUi(TMP_FontAsset font)
        {
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasObject = new GameObject("ChatCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var logObject = new GameObject("ChatLog", typeof(RectTransform));
            logObject.transform.SetParent(canvasObject.transform, false);
            m_Log = logObject.AddComponent<TextMeshProUGUI>();
            m_Log.font = font;
            m_Log.fontSize = 30;
            m_Log.lineSpacing = 20; // room for raised tone marks (D-035 note)
            m_Log.alignment = TextAlignmentOptions.BottomLeft;
            m_Log.textWrappingMode = TextWrappingModes.Normal;
            Place(logObject.GetComponent<RectTransform>(), new Vector2(24, 96), new Vector2(820, 420));

            CreateHint(canvasObject.transform, font, "chat.hint", new Vector2(870, 64));
            CreateHint(canvasObject.transform, font, "language.switch", new Vector2(870, 24));

            var inputObject = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            inputObject.name = "ChatInput";
            inputObject.transform.SetParent(canvasObject.transform, false);
            inputObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            m_Input = inputObject.GetComponent<TMP_InputField>();
            m_Input.fontAsset = font;
            m_Input.pointSize = 30;
            m_Input.textComponent.color = Color.white;
            m_Input.characterLimit = 120;
            m_Input.lineType = TMP_InputField.LineType.SingleLine;
            m_Input.placeholder.gameObject.AddComponent<LocalizedThaiText>().Bind("UI", "chat.placeholder");
            m_Input.onSubmit.AddListener(Submit);
            m_Input.EnableThaiShaping();
            Place(inputObject.GetComponent<RectTransform>(), new Vector2(24, 24), new Vector2(820, 60));
            inputObject.SetActive(false);
        }

        private static void CreateHint(Transform parent, TMP_FontAsset font, string key, Vector2 offset)
        {
            var hint = new GameObject($"Hint {key}", typeof(RectTransform));
            hint.transform.SetParent(parent, false);
            var text = hint.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = 26;
            text.color = new Color(1f, 0.92f, 0.65f);
            text.outlineWidth = 0.25f;
            text.outlineColor = Color.black;
            hint.AddComponent<LocalizedThaiText>().Bind("UI", key);
            Place(hint.GetComponent<RectTransform>(), offset, new Vector2(820, 40));
        }

        private static void ToggleLanguage()
        {
            var locales = LocalizationSettings.AvailableLocales.Locales;
            if (locales.Count < 2)
                return;
            var next = (locales.IndexOf(LocalizationSettings.SelectedLocale) + 1) % locales.Count;
            LocalizationSettings.SelectedLocale = locales[next];
            Debug.Log($"[Proto] language {locales[next].Identifier.Code}");
        }

        private static void Place(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            var open = m_Input.gameObject.activeSelf;
            if (!open && Time.frameCount != m_ClosedFrame &&
                (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                Open();
            else if (open && keyboard.escapeKey.wasPressedThisFrame)
                Close();
            else if (!open && keyboard.f2Key.wasPressedThisFrame)
                ToggleLanguage();
        }

        private void Open()
        {
            m_Input.gameObject.SetActive(true);
            m_Input.text = "";
            m_Input.ActivateInputField();
            SetBlocked(true);
            Cursor.lockState = CursorLockMode.None;
        }

        private void Submit(string shapedText)
        {
            var text = ThaiShaper.Unshape(shapedText);
            if (!string.IsNullOrWhiteSpace(text))
                _ = m_Voice.SendAsync(text);
            Close();
        }

        private void Close()
        {
            m_Input.DeactivateInputField();
            m_Input.gameObject.SetActive(false);
            m_ClosedFrame = Time.frameCount;
            SetBlocked(false);
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void SetBlocked(bool blocked)
        {
            ProtoPlayer.InputBlocked = blocked;
            m_Voice.InputBlocked = blocked;
        }

        private void AddLine(string line)
        {
            m_Lines.Add(line);
            if (m_Lines.Count > MaxLines)
                m_Lines.RemoveAt(0);
            m_Log.SetThaiText(string.Join("\n", m_Lines));
        }
    }
}
