using System;
using System.Threading.Tasks;
using SpiritMovers.Core;
using SpiritMovers.Net;
using SpiritMovers.Session;

namespace SpiritMovers.Tests
{
    /// <summary>Stands in for Unity Gaming Services: join either fails, hangs, or is never reached.</summary>
    public sealed class FakeSessionService : ISessionService
    {
        public Exception JoinFailure;
        public TaskCompletionSource<bool> JoinHang;
        /// <summary>The host's refusal reason arriving while the join call is still running (slow connections).</summary>
        public string RefusalDuringJoin;
        public int JoinCalls;
        public bool? Joinable;

        public Task InitializeAsync(string profile = null) => Task.CompletedTask;
        public Task LeaveAsync() => Task.CompletedTask;

        /// <summary>Hosts on this machine (no Relay) so the online host path runs without the internet.</summary>
        public Task<string> HostAsync(int maxPlayers, GameVersion version)
        {
            var network = Unity.Netcode.NetworkManager.Singleton;
            network.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>().SetConnectionData("127.0.0.1", 7777, "127.0.0.1");
            network.StartHost();
            return Task.FromResult("FAKE01");
        }

        public Task SetJoinableAsync(bool joinable)
        {
            Joinable = joinable;
            return Task.CompletedTask;
        }

        public Task JoinAsync(string code, GameVersion version)
        {
            JoinCalls++;
            if (RefusalDuringJoin != null)
                GameSession.Instance.NoteRefusal(RefusalDuringJoin);
            if (JoinHang != null)
                return JoinHang.Task;
            return Task.FromException(JoinFailure ?? new InvalidOperationException("relay down"));
        }
    }
}
