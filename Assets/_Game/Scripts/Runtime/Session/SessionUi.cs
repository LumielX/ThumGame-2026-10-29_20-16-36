using System;
using SpiritMovers.Core.Text;
using SpiritMovers.Job;
using SpiritMovers.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SpiritMovers.Session
{
    /// <summary>Greybox screen UI for the session flow (menus, lobby): plain panels, buttons and a code field.</summary>
    internal static class SessionUi
    {
        private static readonly Color s_Paper = new(0.12f, 0.09f, 0.07f, 0.9f);
        private static readonly Color s_Button = new(0.32f, 0.22f, 0.12f, 1f);
        private static readonly Color s_Ink = new(1f, 0.95f, 0.85f);
        private static readonly Color s_Amber = new(0.95f, 0.65f, 0.2f);

        /// <summary>Localized, Thai-shaped text of a key (table "UI").</summary>
        public static string Text(string key, params object[] args) =>
            ThaiShaper.Shape(args.Length == 0 ? JobUi.Text(key) : JobUi.Format(key, args));

        public static Transform CreateCanvas(Transform parent, string name) => Rename(JobUi.CreateCanvas(parent), name);

        public static RectTransform CreateColumn(Transform canvas, Vector2 anchor, Vector2 size)
        {
            var column = new GameObject("Column", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            column.transform.SetParent(canvas, false);
            column.GetComponent<Image>().color = s_Paper;
            var layout = column.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 32, 32);
            layout.spacing = 16;
            layout.childForceExpandHeight = false;
            var rect = column.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = size;
            return rect;
        }

        public static TextMeshProUGUI CreateLabel(Transform parent, TMP_FontAsset font, float height, float fontSize = 30)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.lineSpacing = 20; // room for raised Thai tone marks (D-035)
            text.color = s_Ink;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        public static Button CreateButton(Transform parent, TMP_FontAsset font, string key, UnityAction onClick)
        {
            var go = new GameObject($"Button_{key}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = s_Button;
            go.GetComponent<LayoutElement>().preferredHeight = 64;
            var label = CreateLabel(go.transform, font, 64);
            label.alignment = TextAlignmentOptions.Center;
            label.text = Text(key);
            Stretch(label.rectTransform);
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            return button;
        }

        /// <summary>A row that lays its children out left to right.</summary>
        public static Transform CreateRow(Transform parent, float height)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;
            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childForceExpandWidth = false;
            layout.childControlWidth = true;
            return go.transform;
        }

        /// <summary>A horizontal slider (track, amber fill, handle).</summary>
        public static Slider CreateSlider(Transform parent, float min, float max, float value, UnityAction<float> onChange)
        {
            var go = new GameObject("Slider", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = 360;
            element.preferredHeight = 28;
            var track = new GameObject("Background", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(go.transform, false);
            track.GetComponent<Image>().color = s_Button;
            Stretch(track.GetComponent<RectTransform>());
            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            Stretch(fillArea.GetComponent<RectTransform>());
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            fill.GetComponent<Image>().color = s_Amber;
            fill.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            Stretch(handleArea.GetComponent<RectTransform>(), 12);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            handle.GetComponent<Image>().color = s_Ink;
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(24, 0);
            var slider = go.AddComponent<Slider>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(onChange);
            return slider;
        }

        /// <summary>A tick box.</summary>
        public static Toggle CreateToggle(Transform parent, bool value, UnityAction<bool> onChange)
        {
            var go = new GameObject("Toggle", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = s_Button;
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = 40;
            element.preferredHeight = 40;
            var check = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            check.transform.SetParent(go.transform, false);
            check.GetComponent<Image>().color = s_Amber;
            Stretch(check.GetComponent<RectTransform>(), 8);
            var toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = go.GetComponent<Image>();
            toggle.graphic = check.GetComponent<Image>();
            toggle.SetIsOnWithoutNotify(value);
            toggle.onValueChanged.AddListener(onChange);
            return toggle;
        }

        /// <summary>A single-line field for the player's name: Thai shaped while typing (read it back with Unshape).</summary>
        public static TMP_InputField CreateNameField(Transform parent, TMP_FontAsset font, string placeholderKey)
        {
            var field = CreateCodeField(parent, font, placeholderKey);
            field.name = "NameField";
            field.contentType = TMP_InputField.ContentType.Standard;
            field.lineType = TMP_InputField.LineType.SingleLine;
            // Room for combining marks: the name itself is cut to 20 characters when it is saved.
            field.characterLimit = 40;
            field.EnableThaiShaping();
            return field;
        }

        /// <summary>A single-line field for letters and digits (room codes).</summary>
        public static TMP_InputField CreateCodeField(Transform parent, TMP_FontAsset font, string placeholderKey)
        {
            var go = new GameObject("CodeField", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.95f, 0.9f, 0.8f);
            go.GetComponent<LayoutElement>().preferredHeight = 64;
            var area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(go.transform, false);
            Stretch(area.GetComponent<RectTransform>(), 16);
            var placeholder = CreateLabel(area.transform, font, 64);
            placeholder.text = Text(placeholderKey);
            placeholder.color = new Color(0.4f, 0.35f, 0.3f);
            Stretch(placeholder.rectTransform);
            var text = CreateLabel(area.transform, font, 64);
            text.color = new Color(0.1f, 0.08f, 0.06f);
            Stretch(text.rectTransform);
            var field = go.AddComponent<TMP_InputField>();
            field.textViewport = area.GetComponent<RectTransform>();
            field.textComponent = text;
            field.placeholder = placeholder;
            field.fontAsset = font;
            field.characterLimit = 12;
            field.contentType = TMP_InputField.ContentType.Alphanumeric;
            return field;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, 0f);
            rect.offsetMax = new Vector2(-inset, 0f);
        }

        private static Transform Rename(Transform transform, string name)
        {
            transform.name = name;
            return transform;
        }
    }
}
