using SpiritMovers.Core.Job;
using SpiritMovers.Core.Session;
using SpiritMovers.Job;
using SpiritMovers.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpiritMovers.Session
{
    /// <summary>
    /// "On the way to the client's house…" (GDD 16 loading screen): a full-screen card from the moment the van sets off
    /// until the job in the new house is running. Lives on the persistent GameNetwork object.
    /// </summary>
    [RequireComponent(typeof(GameSession))]
    public sealed class LoadingOverlay : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset m_Font;
        [SerializeField] private ThaiGlyphMap m_GlyphMap;

        private GameSession m_Session;
        private GameObject m_Card;

        public bool Visible { get; private set; }

        private void Start()
        {
            m_Session = GetComponent<GameSession>();
            ThaiFontSupport.Register(m_Font, m_GlyphMap);
            var canvas = SessionUi.CreateCanvas(transform, "LoadingCanvas");
            canvas.GetComponent<Canvas>().sortingOrder = 100;
            var column = SessionUi.CreateColumn(canvas, new Vector2(0.5f, 0.5f), new Vector2(3000, 3000));
            var label = SessionUi.CreateLabel(column, m_Font, 1400, 40);
            label.alignment = TextAlignmentOptions.Center;
            label.text = SessionUi.Text("loading.house");
            m_Card = column.gameObject;
            m_Card.SetActive(false);
        }

        private void Update()
        {
            var visible = m_Session.Phase == SessionPhase.Loading || JobNotStarted();
            if (visible == Visible)
                return;
            Visible = visible;
            if (m_Card != null)
                m_Card.SetActive(visible);
        }

        private static bool JobNotStarted()
        {
            if (SceneManager.GetActiveScene().name != GameSession.JobScene)
                return false;
            var director = FindAnyObjectByType<JobDirector>();
            return director == null || director.Phase == JobPhase.Briefing;
        }
    }
}
