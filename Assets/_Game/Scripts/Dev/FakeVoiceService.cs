using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpiritMovers.Core.Voice;
using SpiritMovers.Net;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpiritMovers.Dev
{
    /// <summary>
    /// IVoiceService without Vivox or the internet: PlayMode tests (and -dev-voice-fail bots) drive voice through it
    /// and read back what the game asked for. Text sent comes straight back, as Vivox echoes our own messages.
    /// </summary>
    public sealed class FakeVoiceService : IVoiceService
    {
        private readonly HashSet<string> m_Joined = new();
        private readonly List<VoiceParticipant> m_Participants = new();
        private readonly Dictionary<(string, string), AudioSource> m_Taps = new();

        /// <summary>Every call the game made, in order, e.g. "Join van-AB12", "Transmit house-AB12,radio-AB12".</summary>
        public readonly List<string> Calls = new();
        public readonly Dictionary<string, bool> ParticipantMuted = new();
        public readonly Dictionary<string, float> ParticipantVolume = new();

        public bool FailLogin { get; set; }
        /// <summary>Joins fail too while true (a voice server that is down).</summary>
        public bool FailJoin { get; set; }
        public float OutputVolume { get; private set; } = 1f;
        public IReadOnlyList<string> Transmitting { get; private set; } = Array.Empty<string>();
        public string InputDevice { get; private set; } = "";
        public string OutputDevice { get; private set; } = "";
        public List<string> FakeInputs { get; } = new();
        public List<string> FakeOutputs { get; } = new();

        public event Action<string, string, string> TextReceived;
        public event Action ConnectionLost;
        public event Action ConnectionRecovered;

        public bool IsLoggedIn { get; private set; }
        public string PlayerId { get; set; } = "self-player";
        public IReadOnlyCollection<string> Joined => m_Joined.ToList();
        public bool MicMuted { get; set; }
        public double LocalEnergy { get; private set; }
        public IReadOnlyList<VoiceParticipant> Participants => m_Participants.ToList();
        public IReadOnlyList<string> InputDevices => FakeInputs;
        public IReadOnlyList<string> OutputDevices => FakeOutputs;

        public Task LoginAsync(string displayName)
        {
            Calls.Add($"Login {displayName}");
            if (FailLogin)
                return Task.FromException(new InvalidOperationException("voice login refused (fake)"));
            IsLoggedIn = true;
            return Task.CompletedTask;
        }

        public Task LogoutAsync()
        {
            Calls.Add("Logout");
            foreach (var tap in m_Taps.Values)
                if (tap != null)
                    Object.Destroy(tap.gameObject);
            m_Taps.Clear();
            m_Joined.Clear();
            IsLoggedIn = false;
            return Task.CompletedTask;
        }

        public Task JoinAsync(VoiceChannel channel)
        {
            Calls.Add($"Join {channel.Name}");
            if (FailJoin || !IsLoggedIn)
                return Task.FromException(new InvalidOperationException("voice join refused (fake)"));
            m_Joined.Add(channel.Name);
            return Task.CompletedTask;
        }

        public Task LeaveAsync(string channelName)
        {
            Calls.Add($"Leave {channelName}");
            foreach (var key in m_Taps.Keys.Where(k => k.Item2 == channelName).ToList())
                ReleaseTap(key.Item1, key.Item2);
            m_Joined.Remove(channelName);
            return Task.CompletedTask;
        }

        public Task TransmitAsync(IReadOnlyList<string> channels)
        {
            Transmitting = channels.ToList();
            Calls.Add($"Transmit {string.Join(",", channels)}");
            return Task.CompletedTask;
        }

        public void SetOutputVolume(float gain01) => OutputVolume = gain01;
        public void SetParticipantMuted(string playerId, bool muted) => ParticipantMuted[playerId] = muted;
        public void SetParticipantVolume(string playerId, float gain01) => ParticipantVolume[playerId] = gain01;
        public void UpdatePosition(Transform head, string channelName) => PositionUpdates++;
        public int PositionUpdates { get; private set; }

        public AudioSource TapParticipant(string playerId, string channelName)
        {
            if (m_Taps.TryGetValue((playerId, channelName), out var existing) && existing != null)
                return existing;
            var go = new GameObject($"FakeTap {playerId}");
            Object.DontDestroyOnLoad(go);
            var source = go.AddComponent<AudioSource>();
            m_Taps[(playerId, channelName)] = source;
            return source;
        }

        public void ReleaseTap(string playerId, string channelName)
        {
            if (m_Taps.Remove((playerId, channelName), out var source) && source != null)
                Object.Destroy(source.gameObject);
        }

        public IReadOnlyCollection<(string PlayerId, string Channel)> Taps => m_Taps.Keys.ToList();

        public Task SendTextAsync(string channelName, string text)
        {
            Calls.Add($"Text {channelName} {text}");
            if (m_Joined.Contains(channelName))
                TextReceived?.Invoke(PlayerId, channelName, text);
            return Task.CompletedTask;
        }

        public Task SetInputDeviceAsync(string name)
        {
            InputDevice = !string.IsNullOrEmpty(name) && FakeInputs.Contains(name) ? name : "";
            Calls.Add($"Input {InputDevice}");
            return Task.CompletedTask;
        }

        public Task SetOutputDeviceAsync(string name)
        {
            OutputDevice = !string.IsNullOrEmpty(name) && FakeOutputs.Contains(name) ? name : "";
            Calls.Add($"Output {OutputDevice}");
            return Task.CompletedTask;
        }

        // ---- test controls ----

        public void SetEnergy(double energy) => LocalEnergy = energy;

        public void AddParticipant(VoiceParticipant participant)
        {
            m_Participants.RemoveAll(p => p.PlayerId == participant.PlayerId && p.Channel == participant.Channel);
            m_Participants.Add(participant);
        }

        public void RemoveParticipant(string playerId, string channel) =>
            m_Participants.RemoveAll(p => p.PlayerId == playerId && p.Channel == channel);

        public void DeliverText(string senderPlayerId, string channel, string text) => TextReceived?.Invoke(senderPlayerId, channel, text);

        /// <summary>The voice server is gone: channels are lost.</summary>
        public void SimulateDrop()
        {
            m_Joined.Clear();
            ConnectionLost?.Invoke();
        }

        public void SimulateRecover() => ConnectionRecovered?.Invoke();
    }
}
