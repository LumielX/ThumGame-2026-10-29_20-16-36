using System.Threading.Tasks;
using SpiritMovers.Core;

namespace SpiritMovers.Net
{
    /// <summary>Online sessions (create / join by code / leave); <see cref="UgsSessionService"/> in the game, fakes in tests.</summary>
    public interface ISessionService
    {
        Task InitializeAsync(string profile = null);

        /// <summary>This machine's UGS player id once signed in ("" before) — voice chat knows players by it.</summary>
        string PlayerId { get; }

        /// <summary>Creates a session, starts this machine as the network host and returns the join code.</summary>
        Task<string> HostAsync(int maxPlayers, GameVersion version);

        /// <summary>Joins by code and starts this machine as a network client; throws <see cref="VersionMismatchException"/>.</summary>
        Task JoinAsync(string code, GameVersion version);

        Task LeaveAsync();

        /// <summary>Host: open or close the room to new players (closed while the team is on a job, GDD 9).</summary>
        Task SetJoinableAsync(bool joinable);
    }
}
