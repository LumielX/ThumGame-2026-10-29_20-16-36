using System;
using System.Collections.Generic;
using System.Linq;

namespace SpiritMovers.Core.Job
{
    /// <summary>Phases of one job (TECHNICAL_DESIGN 3).</summary>
    public enum JobPhase
    {
        Briefing,
        Exploring,
        /// <summary>Every room is accepted; the farewell ceremony is running.</summary>
        Farewell,
        Ended,
    }

    /// <summary>
    /// Host-side state of a job: which rooms the ghost accepted and how the job ended.
    /// Commands that do not fit the current phase are ignored, because RPCs may arrive late or twice.
    /// </summary>
    public sealed class JobProgress
    {
        private readonly HashSet<string> m_PlanRooms;
        private readonly HashSet<string> m_Accepted = new();

        public JobPhase Phase { get; private set; } = JobPhase.Briefing;
        public JobEnding? Ending { get; private set; }
        public IReadOnlyCollection<string> AcceptedRooms => m_Accepted;
        public int RoomsRequired => m_PlanRooms.Count;

        public event Action<JobPhase> PhaseChanged;
        public event Action<string> RoomAccepted;

        public JobProgress(JobPlan plan) => m_PlanRooms = plan.Rooms.Select(r => r.RoomId).ToHashSet();

        public void Begin()
        {
            if (Phase == JobPhase.Briefing)
                SetPhase(JobPhase.Exploring);
        }

        public bool AcceptRoom(string roomId)
        {
            if (Phase != JobPhase.Exploring || !m_PlanRooms.Contains(roomId) || !m_Accepted.Add(roomId))
                return false;

            RoomAccepted?.Invoke(roomId);
            if (m_Accepted.Count == m_PlanRooms.Count)
                SetPhase(JobPhase.Farewell);
            return true;
        }

        public void CompleteFarewell()
        {
            if (Phase == JobPhase.Farewell)
                End(JobEnding.FullSuccess);
        }

        /// <summary>The team closes the job at the van; allowed from the briefing too (pays nothing then).</summary>
        public void LeaveEarly()
        {
            if (Phase != JobPhase.Ended)
                End(JobEnding.LeftEarly);
        }

        public void Evict() => EndIfPlaying(JobEnding.Evicted);

        private void EndIfPlaying(JobEnding ending)
        {
            if (Phase == JobPhase.Exploring || Phase == JobPhase.Farewell)
                End(ending);
        }

        private void End(JobEnding ending)
        {
            Ending = ending;
            SetPhase(JobPhase.Ended);
        }

        private void SetPhase(JobPhase phase)
        {
            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }
    }
}
