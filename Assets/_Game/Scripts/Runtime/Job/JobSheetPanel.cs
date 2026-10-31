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
}
