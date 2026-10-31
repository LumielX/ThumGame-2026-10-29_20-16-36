using System.Linq;
using SpiritMovers.Core.Job;
using SpiritMovers.Interaction;
using SpiritMovers.Player;
using Unity.Netcode;
using UnityEngine;

namespace SpiritMovers.Job
{
    /// <summary>
    /// The company van: the team closes the job here once everyone is back at the van (GDD 4.3 "ออกก่อน").
    /// Not offered during the farewell, which always ends as a full success.
    /// </summary>
    public sealed class VanExit : NetworkBehaviour, IInteractable
    {
        [Tooltip("Players count as at the van within this horizontal distance (m).")]
        [SerializeField] private float m_BoardRadius = 4f;

        private JobDirector m_Director;

        public string PromptKey => EveryoneAtVan() ? "job.leave" : "job.leave.wait";

        private void Awake() => m_Director = FindAnyObjectByType<JobDirector>();

        public bool CanInteract(PlayerCarrier by) =>
            by.Held == null && m_Director != null && m_Director.Phase is JobPhase.Briefing or JobPhase.Exploring;

        public void Interact(PlayerCarrier by)
        {
            if (by.IsOwner && EveryoneAtVan())
                RequestLeaveRpc();
        }

        /// <summary>Every player in the session stands by the van (the host checks again with its own view).</summary>
        public bool EveryoneAtVan()
        {
            var van = new Vector2(transform.position.x, transform.position.z);
            return FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None)
                .All(m => Vector2.Distance(new Vector2(m.Feet.x, m.Feet.z), van) <= m_BoardRadius);
        }

        [Rpc(SendTo.Server)]
        private void RequestLeaveRpc()
        {
            if (EveryoneAtVan())
                m_Director.LeaveEarly();
            else
                Debug.Log("[Job] close the job ignored: not everyone is at the van");
        }
    }
}
