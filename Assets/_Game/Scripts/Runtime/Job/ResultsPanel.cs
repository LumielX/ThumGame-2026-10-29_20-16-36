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
            Settings.SettingsApplier.LanguageChanged += Redraw;
        }

        private void OnDestroy() => Settings.SettingsApplier.LanguageChanged -= Redraw;

        /// <summary>The language changed: the report is written again on the next frame.</summary>
        private void Redraw() => m_Shown = false;

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
            // The production game's save (JobPay already added this job); the M3 test house has none.
            if (Save.GameData.IsLoaded)
                lines.Add(JobUi.Format("results.total", Save.GameData.Money));
            LastText = ThaiShaper.Shape(string.Join("\n", lines));
            m_Text.text = LastText;
            m_Text.transform.parent.gameObject.SetActive(true);
            GameCursor.Capture(false);
        }
    }
}
