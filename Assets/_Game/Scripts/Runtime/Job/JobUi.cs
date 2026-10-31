using System.Collections.Generic;
using System.Linq;
using System.Text;
using SpiritMovers.Core.Job;
using SpiritMovers.Core.Text;
using SpiritMovers.Player;
using SpiritMovers.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace SpiritMovers.Job
{
    /// <summary>Greybox screen UI for M3: one canvas holding the work order and the job report.</summary>
    internal static class JobUi
    {
        public static string Text(string key) => LocalizationSettings.StringDatabase.GetLocalizedString("UI", key);

        public static string Format(string key, params object[] args) => string.Format(Text(key), args);

        public static TextMeshProUGUI CreatePanel(Transform canvas, TMP_FontAsset font, string name, Vector2 anchor, Vector2 size)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas, false);
            panel.GetComponent<Image>().color = new Color(0.12f, 0.09f, 0.07f, 0.85f);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = size;

            var label = new GameObject("Text", typeof(RectTransform));
            label.transform.SetParent(panel.transform, false);
            var text = label.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = 30;
            text.lineSpacing = 20; // room for raised Thai tone marks (D-035)
            text.color = new Color(1f, 0.95f, 0.85f);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(24, 20);
            labelRect.offsetMax = new Vector2(-24, -20);
            panel.SetActive(false);
            return text;
        }

        public static Transform CreateCanvas(Transform parent)
        {
            var canvas = new GameObject("JobCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvas.transform.SetParent(parent, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            return canvas.transform;
        }
    }
}
