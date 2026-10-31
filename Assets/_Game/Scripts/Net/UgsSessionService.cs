using System;
using System.Threading.Tasks;
using SpiritMovers.Core;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace SpiritMovers.Net
{
    /// <summary>
    /// Thrown when a player tries to join a session hosted by a different game version (D-008).
    /// </summary>
    public sealed class VersionMismatchException : Exception
    {
        public GameVersion LocalVersion { get; }
        public string HostVersion { get; }

        public VersionMismatchException(GameVersion localVersion, string hostVersion)
            : base($"Host runs version '{hostVersion ?? "unknown"}' but this build is {localVersion}.")
        {
            LocalVersion = localVersion;
            HostVersion = hostVersion;
        }
    }

    /// <summary>
    /// Creates and joins Unity Multiplayer Services sessions (Lobby + Relay + join code).
    /// The session SDK starts Netcode for GameObjects as host or client on the scene's NetworkManager.
    /// </summary>
    public sealed class UgsSessionService : ISessionService
    {
        private const string VersionProperty = "ver";

        public ISession Session { get; private set; }

        /// <summary>Id of the current session, or null; also names its voice channels.</summary>
        public string SessionId => Session?.Id;

        /// <summary>
        /// Initializes Unity Services and signs in anonymously. A distinct <paramref name="profile"/> lets
        /// several game instances on one machine sign in as different players.
        /// </summary>
        public async Task InitializeAsync(string profile = null)
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                var options = new InitializationOptions();
                if (!string.IsNullOrEmpty(profile))
                    options.SetProfile(profile);
                await UnityServices.InitializeAsync(options);
            }

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            Debug.Log($"[Net] Signed in as {AuthenticationService.Instance.PlayerId}");
        }

        /// <summary>Hosts a private Relay session and returns its join code.</summary>
        public async Task<string> HostAsync(int maxPlayers, GameVersion version)
        {
            var options = new SessionOptions
            {
                MaxPlayers = maxPlayers,
                IsPrivate = true,
            }.WithRelayNetwork();
            options.SessionProperties[VersionProperty] = new SessionProperty(version.ToString());

            Session = await MultiplayerService.Instance.CreateSessionAsync(options);
            Debug.Log($"[Net] Hosting session {Session.Id} code={Session.Code} max={maxPlayers} v{version}");
            return Session.Code;
        }

        /// <summary>Joins a session by code; leaves again and throws if the host runs another version.</summary>
        public async Task JoinAsync(string code, GameVersion version)
        {
            Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());

            Session.Properties.TryGetValue(VersionProperty, out var hostProperty);
            var hostVersionText = hostProperty?.Value;
            if (!GameVersion.TryParse(hostVersionText, out var hostVersion) || !hostVersion.CanPlayWith(version))
            {
                await LeaveAsync();
                throw new VersionMismatchException(version, hostVersionText);
            }

            Debug.Log($"[Net] Joined session {Session.Id} host v{hostVersion} players={Session.PlayerCount}/{Session.MaxPlayers}");
        }

        public async Task LeaveAsync()
        {
            if (Session == null)
                return;

            var session = Session;
            Session = null;
            await session.LeaveAsync();
            Debug.Log("[Net] Left session");
        }
    }
}
