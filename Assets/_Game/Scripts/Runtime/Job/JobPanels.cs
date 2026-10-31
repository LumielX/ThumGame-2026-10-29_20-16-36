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

    /// <summary>
    /// The work order (GDD 16, D-023), held up with Tab: rooms of the job, the ghost's stamp on accepted ones,
    /// the last bell answer per room and the ghost's mood.
    /// </summary>
    public sealed class JobSheetPanel : MonoBehaviour
    {
        private const float RefreshSeconds = 0.25f;

        [SerializeField] private TMP_FontAsset m_Font;
        [SerializeField] private ThaiGlyphMap m_GlyphMap;

        private JobDirector m_Director;
        private TextMeshProUGUI m_Text;
        private float m_NextRefresh;

        public string LastText { get; private set; } = "";

        private void Start()
        {
            m_Director = FindAnyObjectByType<JobDirector>();
            ThaiFontSupport.Register(m_Font, m_GlyphMap);
            m_Text = JobUi.CreatePanel(JobUi.CreateCanvas(transform), m_Font, "JobSheet", new Vector2(0.5f, 0.5f), new Vector2(760, 620));
        }

        private void Update()
        {
            var motor = PlayerMotor.Local;
            var show = motor != null && motor.CurrentInput.JobSheetHeld && m_Director.Plan != null && m_Director.Phase != JobPhase.Ended;
            m_Text.transform.parent.gameObject.SetActive(show);
            if (!show || Time.unscaledTime < m_NextRefresh)
                return;
            m_NextRefresh = Time.unscaledTime + RefreshSeconds;
            Refresh();
        }

        /// <summary>Builds the sheet text (also used by tests).</summary>
        public void Refresh()
        {
            var bells = FindObjectsByType<GhostBell>(FindObjectsSortMode.None);
            var text = new StringBuilder(JobUi.Text("job.sheet.title")).Append('\n');
            foreach (var room in m_Director.Plan.Rooms)
            {
                text.Append(m_Director.AcceptedRooms.Contains(room.RoomId) ? "(/) " : "( ) ")
                    .Append(JobUi.Text($"room.{room.RoomId}"));
                if (m_Director.AcceptedRooms.Contains(room.RoomId))
                    text.Append(" — ").Append(JobUi.Text("job.accepted"));
                var bell = bells.FirstOrDefault(b => b.RoomId == room.RoomId);
                if (bell != null && bell.LastNod >= 0)
                    text.Append(" — ").Append(JobUi.Format("bell.nod", bell.LastNod));
                text.Append('\n');
            }
            text.Append(JobUi.Text($"ghost.mood.{Mathf.Clamp(m_Director.AngerStage, 0, 4)}"));
            LastText = ThaiShaper.Shape(text.ToString());
            m_Text.text = LastText;
        }
    }

    /// <summary>The job report shown to everyone when a job ends (GDD 4.3, 16).</summary>
    public sealed class ResultsPanel : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset m_Font;
        [SerializeField] private ThaiGlyphMap m_GlyphMap;

        private JobDirector m_Director;
        private TextMeshProUGUI m_Text;
        private bool m_Shown;

        public string LastText { get; private set; } = "";

        private void Start()
        {
            m_Director = FindAnyObjectByType<JobDirector>();
            ThaiFontSupport.Register(m_Font, m_GlyphMap);
            m_Text = JobUi.CreatePanel(JobUi.CreateCanvas(transform), m_Font, "Results", new Vector2(0.5f, 0.5f), new Vector2(760, 460));
        }

        private void Update()
        {
            if (m_Shown || m_Director == null || !m_Director.Result.IsSet)
                return;
            m_Shown = true;
            var result = m_Director.Result;
            var lines = new List<string>
            {
                JobUi.Text("results.title"),
                JobUi.Text(result.Ending switch
                {
                    JobEnding.FullSuccess => "results.full",
                    JobEnding.Evicted => "results.evicted",
                    _ => "results.early",
                }),
                JobUi.Format("results.rooms", result.RoomsAccepted, result.RoomsRequired),
                JobUi.Format("results.pay", result.PerPlayer),
            };
            if (result.Ending == JobEnding.FullSuccess)
                lines.Add(JobUi.Format("results.stars", result.Stars));
            LastText = ThaiShaper.Shape(string.Join("\n", lines));
            m_Text.text = LastText;
            m_Text.transform.parent.gameObject.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
