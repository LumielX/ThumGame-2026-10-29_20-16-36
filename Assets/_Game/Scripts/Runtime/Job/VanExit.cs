using SpiritMovers.Core.Job;
using SpiritMovers.Interaction;
using Unity.Netcode;

namespace SpiritMovers.Job
{
    /// <summary>The company van: anyone can close the job here (GDD 4.3 "ออกก่อน").</summary>
    public sealed class VanExit : NetworkBehaviour, IInteractable
    {
        private JobDirector m_Director;

        public string PromptKey => "job.leave";

        private void Awake() => m_Director = FindAnyObjectByType<JobDirector>();

        public bool CanInteract(PlayerCarrier by) => by.Held == null && m_Director != null && m_Director.Phase != JobPhase.Ended;

        public void Interact(PlayerCarrier by)
        {
            if (by.IsOwner)
                RequestLeaveRpc();
        }

        [Rpc(SendTo.Server)]
        private void RequestLeaveRpc() => m_Director.LeaveEarly();
    }
}
