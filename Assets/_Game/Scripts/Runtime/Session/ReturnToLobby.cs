using SpiritMovers.Job;
using SpiritMovers.Player;
using SpiritMovers.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace SpiritMovers.Session
{
    /// <summary>
    /// Production job scene: after the job report the team drives back to the van (GDD 9: งาน → Results → Lobby) —
    /// after a countdown, or at once when the host presses Enter. Loading the van and later a fresh job scene resets
    /// the house for the next job (M3C carry-over 5).
    /// </summary>
    public sealed class ReturnToLobby : MonoBehaviour
    {
        public const float ResultsSeconds = 10f;

        [SerializeField] private TMP_FontAsset m_Font;
        [SerializeField] private ThaiGlyphMap m_GlyphMap;

        private JobDirector m_Director;
        private TextMeshProUGUI m_Label;
        private float m_BackAt = -1f;
        private bool m_Leaving;

        public float SecondsLeft => m_BackAt < 0f ? -1f : Mathf.Max(0f, m_BackAt - Time.time);

        private void Start()
        {
            m_Director = FindAnyObjectByType<JobDirector>();
            ThaiFontSupport.Register(m_Font, m_GlyphMap);
            var canvas = SessionUi.CreateCanvas(transform, "BackCanvas");
            var column = SessionUi.CreateColumn(canvas, new Vector2(0.5f, 0.12f), new Vector2(900, 110));
            m_Label = SessionUi.CreateLabel(column, m_Font, 60, 28);
            m_Label.alignment = TextAlignmentOptions.Center;
            column.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (m_Director == null || !m_Director.Result.IsSet)
                return;
            if (m_BackAt < 0f)
            {
                m_BackAt = Time.time + ResultsSeconds;
                m_Label.transform.parent.gameObject.SetActive(true);
            }
            m_Label.text = SessionUi.Text("results.back_in", Mathf.CeilToInt(SecondsLeft));

            var network = NetworkManager.Singleton;
            if (m_Leaving || network == null || !network.IsServer)
                return;
            var confirm = PlayerMotor.Local != null && PlayerMotor.Local.CurrentInput.ConfirmPressed;
            if (Time.time < m_BackAt && !confirm)
                return;
            m_Leaving = true;
            Debug.Log("[Lobby] back to the van");
            network.SceneManager.LoadScene(GameSession.LobbyScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
}
