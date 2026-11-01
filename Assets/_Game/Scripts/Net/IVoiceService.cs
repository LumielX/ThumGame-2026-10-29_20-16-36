using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SpiritMovers.Core.Voice;
using UnityEngine;

namespace SpiritMovers.Net
{
    /// <summary>
    /// Voice and text chat for the team (TECHNICAL_DESIGN 8.1, D-009): Vivox in the game, a fake in tests. Signing in
    /// to Unity Services happens first (UgsSessionService); voice uses the same player id.
    /// </summary>
    public interface IVoiceService
    {
        Task LoginAsync(string displayName);
        Task LogoutAsync();
        bool IsLoggedIn { get; }
        /// <summary>This machine's UGS player id (what other players see as our participant id).</summary>
        string PlayerId { get; }

        Task JoinAsync(VoiceChannel channel);
        Task LeaveAsync(string channelName);
        IReadOnlyCollection<string> Joined { get; }

        /// <summary>Send our voice to these channels only (empty = to none).</summary>
        Task TransmitAsync(IReadOnlyList<string> channels);

        bool MicMuted { get; set; }
        /// <summary>Everyone's voice, 0..1 of the default loudness.</summary>
        void SetOutputVolume(float gain01);
        void SetParticipantMuted(string playerId, bool muted);
        void SetParticipantVolume(string playerId, float gain01);

        /// <summary>Where we stand and look, for a positional channel.</summary>
        void UpdatePosition(Transform head, string channelName);

        /// <summary>How loud we are speaking now (0..1, before distance).</summary>
        double LocalEnergy { get; }
        IReadOnlyList<VoiceParticipant> Participants { get; }

        /// <summary>
        /// One teammate's voice on a channel as a Unity AudioSource (and no longer straight from Vivox), so it can go
        /// through filters — the walkie-talkie sound. Released with <see cref="ReleaseTap"/>.
        /// </summary>
        AudioSource TapParticipant(string playerId, string channelName);
        void ReleaseTap(string playerId, string channelName);

        Task SendTextAsync(string channelName, string text);
        /// <summary>(sender player id, channel, text) — our own messages come back too.</summary>
        event Action<string, string, string> TextReceived;

        IReadOnlyList<string> InputDevices { get; }
        IReadOnlyList<string> OutputDevices { get; }
        /// <summary>Use this device; an empty or unknown name keeps the system default.</summary>
        Task SetInputDeviceAsync(string name);
        Task SetOutputDeviceAsync(string name);

        /// <summary>Voice is gone and did not come back on its own; the channels must be joined again.</summary>
        event Action ConnectionLost;
        event Action ConnectionRecovered;
    }
}
