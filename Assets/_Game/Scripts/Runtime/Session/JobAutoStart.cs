using SpiritMovers.Core.Job;
using SpiritMovers.Job;
using Unity.Netcode;
using UnityEngine;

namespace SpiritMovers.Session
{
    /// <summary>
    /// Host, production job scene only: once every player has loaded the house, start the job the van picked with a
    /// fresh random seed. (The test house has no auto start: tests and bots begin jobs themselves.)
    /// </summary>
    public sealed class JobAutoStart : MonoBehaviour
    {
        /// <summary>If someone never reports the scene loaded, start anyway after this long.</summary>
        private const float FallbackSeconds = 10f;

        private float m_StartBy;
        private bool m_Started;

        private void Start() => m_StartBy = Time.time + FallbackSeconds;

        private void Update()
        {
            var network = NetworkManager.Singleton;
            if (m_Started || network == null || !network.IsServer)
                return;
            var session = GameSession.Instance;
            if ((session != null && session.JobSceneReady) || Time.time >= m_StartBy)
                Begin();
        }

        private void Begin()
        {
            if (m_Started)
                return;
            var director = FindAnyObjectByType<JobDirector>();
            if (director == null || !director.IsSpawned)
                return;
            m_Started = true;
            var difficulty = LobbyState.ChosenDifficulty == JobDifficulty.Hard.Id ? JobDifficulty.Hard : JobDifficulty.Normal;
            director.BeginJob(Random.Range(1, int.MaxValue), difficulty);
        }
    }
}
