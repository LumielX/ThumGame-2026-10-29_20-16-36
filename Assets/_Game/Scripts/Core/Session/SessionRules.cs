using System.Collections.Generic;
using System.Linq;
using SpiritMovers.Core.Job;

namespace SpiritMovers.Core.Session
{
    /// <summary>Where the team is in the session (TECHNICAL_DESIGN 3: Lobby → Loading → Job → Results → Lobby).</summary>
    public enum SessionPhase
    {
        Lobby,
        Loading,
        Job,
        Results,
    }

    public enum JoinRefusal
    {
        None,
        VersionMismatch,
        /// <summary>Players join only in the van between jobs (GDD 9).</summary>
        JobInProgress,
        Full,
    }

    /// <summary>Crew size as the lobby shows it: 1–4 supported, 5–6 experimental (GDD 9, D-026).</summary>
    public enum CrewSize
    {
        Supported,
        Experimental,
        TooMany,
    }

    /// <summary>Rules of the session flow: who may join, when a job may start, what a player keeps if the host leaves.</summary>
    public static class SessionRules
    {
        public const int SupportedPlayers = 4;
        public const int MaxPlayers = 6;

        /// <summary>Checked in this order: game version, room full, job in progress.</summary>
        public static JoinRefusal CanJoin(SessionPhase phase, int players, int maxPlayers, bool versionMatches)
        {
            if (!versionMatches)
                return JoinRefusal.VersionMismatch;
            if (players >= maxPlayers)
                return JoinRefusal.Full;
            return phase == SessionPhase.Lobby ? JoinRefusal.None : JoinRefusal.JobInProgress;
        }

        /// <summary>A job starts only when there is someone in the van and everyone is ready.</summary>
        public static bool CanStart(IReadOnlyCollection<bool> ready) => ready.Count > 0 && ready.All(r => r);

        public static CrewSize SizeLabel(int players) =>
            players <= SupportedPlayers ? CrewSize.Supported : players <= MaxPlayers ? CrewSize.Experimental : CrewSize.TooMany;

        /// <summary>
        /// Each player's pay when the host leaves (GDD 9: "ได้ค่าจ้างของห้องที่ผีรับแล้ว"): the rooms accepted so far,
        /// paid like leaving early (no stars); nothing before the job has started.
        /// </summary>
        public static int HostLeftPay(SessionPhase phase, int roomsAccepted, int roomsRequired, int basePay, int players)
        {
            if (phase != SessionPhase.Job && phase != SessionPhase.Results)
                return 0;
            return PayoutCalculator.Calculate(JobEnding.LeftEarly, basePay, roomsAccepted, roomsRequired, stars: 0, hintsUsed: 0,
                selfReleases: 0, players, PayoutRules.Default).PerPlayer;
        }

        /// <summary>A join code as typed: spaces removed, upper case; null when empty or not just letters and digits.</summary>
        public static string NormalizeCode(string raw)
        {
            if (raw == null)
                return null;
            var code = new string(raw.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
            return code.Length > 0 && code.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9') ? code : null;
        }
    }
}
