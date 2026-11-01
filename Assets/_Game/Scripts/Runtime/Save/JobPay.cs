using SpiritMovers.Job;
using UnityEngine;

namespace SpiritMovers.Save
{
    /// <summary>
    /// In the job scene: once the job report is in, this player's share goes into their own save (GDD 12). Runs
    /// before the results screen so its total already includes this job.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class JobPay : MonoBehaviour
    {
        private JobDirector m_Director;

        private void Start() => m_Director = FindAnyObjectByType<JobDirector>();

        private void Update()
        {
            if (m_Director != null && m_Director.Result.IsSet)
                GameData.CreditPay(m_Director.Result.PerPlayer, m_Director);
        }
    }
}
