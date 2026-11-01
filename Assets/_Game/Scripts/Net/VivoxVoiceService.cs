using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpiritMovers.Core.Voice;
using Unity.Services.Vivox;
using Unity.Services.Vivox.AudioTaps;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpiritMovers.Net
{
    /// <summary>
    /// IVoiceService on Vivox 16 (D-009; proven in the M2 prototype). The van and the radio are group channels, the
    /// house a positional one. Scripted runs never open the real microphone (M2 lesson 5): batch mode and dev bots
    /// stay muted unless a test WAV is injected with -dev-wav (Development builds only).
    /// </summary>
    public sealed class VivoxVoiceService : IVoiceService
    {
        private const int LogoutTimeoutMs = 5000; // a stuck logout once kept the process alive (M2 lesson 4)

        private readonly Dictionary<(string, string), GameObject> m_Taps = new();
        private readonly HashSet<string> m_MutedPlayers = new();
        private readonly Dictionary<string, int> m_PlayerVolumes = new();
        private bool m_Subscribed;
        private bool m_MicMuted;

        private static IVivoxService Vivox => VivoxService.Instance;

        public event Action<string, string, string> TextReceived;
        public event Action ConnectionLost;
        public event Action ConnectionRecovered;

        public bool IsLoggedIn => VivoxService.Instance != null && Vivox.IsLoggedIn;
        public string PlayerId => Vivox?.SignedInPlayerId;
        public IReadOnlyCollection<string> Joined => IsLoggedIn ? Vivox.ActiveChannels.Keys.ToList() : new List<string>();

        public IReadOnlyList<VoiceParticipant> Participants => !IsLoggedIn
            ? Array.Empty<VoiceParticipant>()
            : Vivox.ActiveChannels.SelectMany(c => c.Value.Select(p =>
                new VoiceParticipant(p.PlayerId, c.Key, p.SpeechDetected, p.AudioEnergy, p.IsSelf))).ToList();

        public double LocalEnergy => !IsLoggedIn ? 0d
            : Vivox.ActiveChannels.SelectMany(c => c.Value).Where(p => p.IsSelf).Select(p => p.AudioEnergy).DefaultIfEmpty(0d).Max();

        public IReadOnlyList<string> InputDevices => Vivox?.AvailableInputDevices.Select(d => d.DeviceName).ToList() ?? new List<string>();
        public IReadOnlyList<string> OutputDevices => Vivox?.AvailableOutputDevices.Select(d => d.DeviceName).ToList() ?? new List<string>();

        public bool MicMuted
        {
            get => m_MicMuted;
            set
            {
                m_MicMuted = value || NeverOpenTheMicrophone;
                if (Vivox == null)
                    return;
                if (m_MicMuted)
                    Vivox.MuteInputDevice();
                else
                    Vivox.UnmuteInputDevice();
            }
        }

        public async Task LoginAsync(string displayName)
        {
            if (Vivox.InitializationState != VivoxInitializationState.Initialized)
                await Vivox.InitializeAsync();
            if (!m_Subscribed)
            {
                Vivox.ChannelMessageReceived += m => TextReceived?.Invoke(m.SenderPlayerId, m.ChannelName, m.MessageText);
                Vivox.ConnectionFailedToRecover += () => ConnectionLost?.Invoke();
                Vivox.ConnectionRecovered += () => ConnectionRecovered?.Invoke();
                Vivox.ParticipantAddedToChannel += Reapply;
                m_Subscribed = true;
            }
            if (!Vivox.IsLoggedIn)
                await Vivox.LoginAsync(new LoginOptions { DisplayName = displayName });
            MicMuted = m_MicMuted;
            Debug.Log($"[Voice] logged in as {Vivox.SignedInPlayerId} (mic {(NeverOpenTheMicrophone ? "kept off for a scripted run" : "available")})");
        }

        public async Task LogoutAsync()
        {
            foreach (var tap in m_Taps.Values)
                if (tap != null)
                    Object.Destroy(tap);
            m_Taps.Clear();
            if (!IsLoggedIn)
                return;
            try
            {
                if (Vivox.IsInjectingAudio)
                    Vivox.StopAudioInjection();
                var logout = LeaveAndLogoutAsync();
                if (await Task.WhenAny(logout, Task.Delay(LogoutTimeoutMs)) == logout)
                    await logout; // surfaces a failure; a logout that hangs is left behind
                else
                    Debug.LogWarning("[Voice] logout did not finish in time; going on without it");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Voice] logout failed: {e.Message}");
            }
        }

        private static async Task LeaveAndLogoutAsync()
        {
            await Vivox.LeaveAllChannelsAsync();
            await Vivox.LogoutAsync();
        }

        public async Task JoinAsync(VoiceChannel channel)
        {
            if (Vivox.ActiveChannels.ContainsKey(channel.Name))
                return;
            var capability = channel.Text ? ChatCapability.TextAndAudio : ChatCapability.AudioOnly;
            if (channel.Kind == VoiceChannelKind.Positional)
                await Vivox.JoinPositionalChannelAsync(channel.Name, capability, new Channel3DProperties(
                    (int)VoiceRules.SilentDistance, (int)VoiceRules.FullVolumeDistance, 1f, AudioFadeModel.InverseByDistance));
            else
                await Vivox.JoinGroupChannelAsync(channel.Name, capability);
            StartInjectionIfAsked();
        }

        public async Task LeaveAsync(string channelName)
        {
            foreach (var key in m_Taps.Keys.Where(k => k.Item2 == channelName).ToList())
                ReleaseTap(key.Item1, key.Item2);
            if (IsLoggedIn && Vivox.ActiveChannels.ContainsKey(channelName))
                await Vivox.LeaveChannelAsync(channelName);
        }

        public async Task TransmitAsync(IReadOnlyList<string> channels)
        {
            var joined = channels.Where(c => Vivox.ActiveChannels.ContainsKey(c)).ToList();
            if (joined.Count == 0)
                await Vivox.SetChannelTransmissionModeAsync(TransmissionMode.None);
            else if (joined.Count == 1)
                await Vivox.SetChannelTransmissionModeAsync(TransmissionMode.Single, joined[0]);
            else
                await Vivox.SetChannelTransmissionModeAsync(TransmissionMode.All);
        }

        public void SetOutputVolume(float gain01) => Vivox?.SetOutputDeviceVolume(ToVivoxVolume(gain01));

        public void SetParticipantMuted(string playerId, bool muted)
        {
            if (muted)
                m_MutedPlayers.Add(playerId);
            else
                m_MutedPlayers.Remove(playerId);
            foreach (var p in Find(playerId))
                Apply(p);
        }

        public void SetParticipantVolume(string playerId, float gain01)
        {
            m_PlayerVolumes[playerId] = ToVivoxVolume(gain01);
            foreach (var p in Find(playerId))
                Apply(p);
        }

        public void UpdatePosition(Transform head, string channelName)
        {
            if (head != null && IsLoggedIn && Vivox.ActiveChannels.ContainsKey(channelName))
                Vivox.Set3DPosition(head.gameObject, channelName);
        }

        public AudioSource TapParticipant(string playerId, string channelName)
        {
            if (m_Taps.TryGetValue((playerId, channelName), out var existing) && existing != null)
                return existing.GetComponent<AudioSource>();
            // Built inactive so the tap has its channel and participant before OnEnable registers it (M2).
            var go = new GameObject($"VoiceTap {playerId}");
            go.SetActive(false);
            Object.DontDestroyOnLoad(go);
            var source = go.AddComponent<AudioSource>();
            source.spatialBlend = 0f;
            var tap = go.AddComponent<VivoxParticipantTap>();
            tap.ChannelName = channelName;
            tap.ParticipantName = playerId;
            tap.SilenceInChannelAudioMix = true;
            go.SetActive(true);
            m_Taps[(playerId, channelName)] = go;
            return source;
        }

        public void ReleaseTap(string playerId, string channelName)
        {
            if (m_Taps.Remove((playerId, channelName), out var go) && go != null)
                Object.Destroy(go);
        }

        public async Task SendTextAsync(string channelName, string text)
        {
            if (IsLoggedIn && Vivox.ActiveChannels.ContainsKey(channelName))
                await Vivox.SendChannelTextMessageAsync(channelName, text);
        }

        public async Task SetInputDeviceAsync(string name)
        {
            var device = Vivox.AvailableInputDevices.FirstOrDefault(d => d.DeviceName == name);
            if (!string.IsNullOrEmpty(name) && device != null)
                await Vivox.SetActiveInputDeviceAsync(device);
        }

        public async Task SetOutputDeviceAsync(string name)
        {
            var device = Vivox.AvailableOutputDevices.FirstOrDefault(d => d.DeviceName == name);
            if (!string.IsNullOrEmpty(name) && device != null)
                await Vivox.SetActiveOutputDeviceAsync(device);
        }

        /// <summary>0..1 of the default loudness → Vivox's −50..0 (decibel-like); never boosted past the default.</summary>
        public static int ToVivoxVolume(float gain01)
        {
            if (gain01 <= 0.0001f)
                return -50;
            return Mathf.Clamp(Mathf.RoundToInt(20f * Mathf.Log10(Mathf.Min(gain01, 1f))), -50, 0);
        }

        private IEnumerable<VivoxParticipant> Find(string playerId) => !IsLoggedIn
            ? Enumerable.Empty<VivoxParticipant>()
            : Vivox.ActiveChannels.SelectMany(c => c.Value).Where(p => p.PlayerId == playerId && !p.IsSelf);

        /// <summary>A player muted or turned down in one channel stays so in every channel they join later.</summary>
        private void Reapply(VivoxParticipant participant)
        {
            if (!participant.IsSelf)
                Apply(participant);
        }

        private void Apply(VivoxParticipant participant)
        {
            if (m_MutedPlayers.Contains(participant.PlayerId))
                participant.MutePlayerLocally();
            else
                participant.UnmutePlayerLocally();
            participant.SetLocalVolume(m_PlayerVolumes.TryGetValue(participant.PlayerId, out var volume) ? volume : 0);
        }

        /// <summary>Batch mode and dev bots never use the machine's microphone; only an injected WAV speaks.</summary>
        private static bool NeverOpenTheMicrophone =>
            (Application.isBatchMode || DevArg("-dev-flow") != null || DevArg("-dev-voice") != null) && DevArg("-dev-wav") == null;

        private static void StartInjectionIfAsked()
        {
            var wav = DevArg("-dev-wav");
            if (wav != null)
            {
                // Injection belongs to the channels it started in: leaving the van ends it, so start again on every join.
                if (Vivox.IsInjectingAudio)
                    Vivox.StopAudioInjection();
                Vivox.StartAudioInjection(wav);
                Debug.Log($"[Voice] speaking from {System.IO.Path.GetFileName(wav)} instead of a microphone");
            }
        }

        private static string DevArg(string name)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
                if (args[i] == name)
                    return i + 1 < args.Length ? args[i + 1] : "";
#endif
            return null;
        }
    }
}
