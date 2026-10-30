using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpiritMovers.Prototypes.NetSync
{
    /// <summary>
    /// Throwaway M2 voice/text prototype on Vivox (D-009, D-010, D-013 J3).
    /// Proximity voice = positional channel; radio + text chat = group channel of the same session.
    /// Keys: hold R = talk on radio instead of proximity, V = mute mic.
    /// Headless automation (command line):
    ///   -proto-voice-inject WAV  feed a mono 16-bit PCM file as the microphone
    ///   -proto-voice-tx pos|radio  channel the injected audio goes to
    ///   -proto-chat TEXT  send one text message after joining
    /// </summary>
    public sealed class VoiceProto : MonoBehaviour
    {
        // House-scale hearing range (GDD: proximity voice inside one house).
        private const int AudibleDistance = 25;
        private const int ConversationalDistance = 2;
        private const float PositionInterval = 0.1f;
        private const float MetricsInterval = 5f;

        private readonly Dictionary<string, double> m_PeakEnergy = new();
        private string m_PositionalChannel;
        private string m_RadioChannel;
        private bool m_RadioHeld;
        private float m_NextPosition;
        private float m_NextMetrics;
        private double m_SummaryPeakRemoteEnergy;
        private int m_SummarySpeechSamples;
        private int m_SummaryMessages;

        public string Status { get; private set; } = "Voice: off";
        public bool Joined { get; private set; }
        public bool MicMuted { get; private set; }
        public bool VoiceEnabled { get; private set; } = true;

        /// <summary>D-010 opt-out: when false the ghost ignores this player's voice.</summary>
        public bool GhostHearsMe { get; set; } = true;

        /// <summary>Local mic level 0..1 as the sharp-eared ghost would hear it (0 when opted out or muted).</summary>
        public double GhostHearingLevel { get; private set; }

        public bool InputBlocked { get; set; }
        public string TransmitLabel => m_RadioHeld ? "radio" : "proximity";

        public event Action<string, string> MessageReceived;

        public async Task JoinAsync(string sessionId, string displayName)
        {
            m_PositionalChannel = "pos" + sessionId;
            m_RadioChannel = "team" + sessionId;
            var vivox = VivoxService.Instance;

            Status = "Voice: connecting…";
            await vivox.InitializeAsync();
            vivox.ParticipantAddedToChannel += p => Debug.Log($"[Voice] + {p.DisplayName} in {Kind(p.ChannelName)} self={p.IsSelf}");
            vivox.ParticipantRemovedFromChannel += p => Debug.Log($"[Voice] - {p.DisplayName} left {Kind(p.ChannelName)}");
            vivox.ChannelMessageReceived += OnMessage;
            vivox.ConnectionRecovering += () => Debug.LogWarning("[Voice] connection recovering");
            vivox.ConnectionFailedToRecover += () => Debug.LogError("[Voice] connection failed to recover");

            await vivox.LoginAsync(new LoginOptions { DisplayName = displayName });
            await vivox.JoinPositionalChannelAsync(m_PositionalChannel, ChatCapability.AudioOnly,
                new Channel3DProperties(AudibleDistance, ConversationalDistance, 1f, AudioFadeModel.InverseByDistance));
            await vivox.JoinGroupChannelAsync(m_RadioChannel, ChatCapability.TextAndAudio);
            await vivox.SetChannelTransmissionModeAsync(TransmissionMode.Single, m_PositionalChannel);
            Joined = true;
            Status = "Voice: on";
            Debug.Log($"[Voice] joined as '{displayName}' channels={string.Join(",", vivox.ActiveChannels.Keys.Select(Kind))} " +
                      $"input='{vivox.EffectiveInputDevice?.DeviceName}' output='{vivox.EffectiveOutputDevice?.DeviceName}'");

            await RunAutomationAsync();
        }

        private async Task RunAutomationAsync()
        {
            var tx = NetSyncProtoRunner.Arg("-proto-voice-tx");
            if (tx == "radio")
                await SetRadioAsync(true);

            var inject = NetSyncProtoRunner.Arg("-proto-voice-inject");
            if (string.IsNullOrEmpty(inject) && NetSyncProtoRunner.Arg("-proto-role") != null)
            {
                // Automated runs must never stream the real room microphone.
                VivoxService.Instance.MuteInputDevice();
                MicMuted = true;
            }
            else if (!string.IsNullOrEmpty(inject))
            {
                VivoxService.Instance.StartAudioInjection(inject);
                Debug.Log($"[Voice] injecting '{inject}' into {TransmitLabel}");
            }

            var chat = NetSyncProtoRunner.Arg("-proto-chat");
            if (!string.IsNullOrEmpty(chat))
            {
                await Task.Delay(3000); // let the other side finish joining
                await SendAsync(chat);
            }
        }

        public async Task SendAsync(string text)
        {
            if (!Joined || string.IsNullOrWhiteSpace(text))
                return;
            await VivoxService.Instance.SendChannelTextMessageAsync(m_RadioChannel, text.Trim());
            Debug.Log($"[Voice] sent text len={text.Trim().Length}");
        }

        private void OnMessage(VivoxMessage message)
        {
            m_SummaryMessages++;
            Debug.Log($"[Voice] text from {message.SenderDisplayName} self={message.FromSelf}: {message.MessageText}");
            MessageReceived?.Invoke(message.SenderDisplayName, message.MessageText);
        }

        public void ToggleMic()
        {
            MicMuted = !MicMuted;
            if (MicMuted) VivoxService.Instance.MuteInputDevice();
            else VivoxService.Instance.UnmuteInputDevice();
        }

        /// <summary>Per-player voice switch (D-010): turns off both hearing and speaking.</summary>
        public void ToggleVoice()
        {
            VoiceEnabled = !VoiceEnabled;
            if (VoiceEnabled)
            {
                VivoxService.Instance.UnmuteOutputDevice();
                if (!MicMuted) VivoxService.Instance.UnmuteInputDevice();
            }
            else
            {
                VivoxService.Instance.MuteOutputDevice();
                VivoxService.Instance.MuteInputDevice();
            }
            Status = VoiceEnabled ? "Voice: on" : "Voice: off (player choice)";
        }

        private void Update()
        {
            if (!Joined)
                return;

            var keyboard = Keyboard.current;
            if (keyboard != null && !InputBlocked && !Application.isBatchMode)
            {
                if (keyboard.vKey.wasPressedThisFrame)
                    ToggleMic();
                var radio = keyboard.rKey.isPressed;
                if (radio != m_RadioHeld)
                    _ = SetRadioAsync(radio);
            }

            var local = ProtoPlayer.Local;
            if (local != null && Time.realtimeSinceStartup >= m_NextPosition)
            {
                m_NextPosition = Time.realtimeSinceStartup + PositionInterval;
                VivoxService.Instance.Set3DPosition(local.Head.gameObject, m_PositionalChannel);
            }

            SampleEnergy();
            if (Time.realtimeSinceStartup >= m_NextMetrics)
            {
                m_NextMetrics = Time.realtimeSinceStartup + MetricsInterval;
                LogMetrics();
            }
        }

        private async Task SetRadioAsync(bool radio)
        {
            m_RadioHeld = radio;
            try
            {
                await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.Single,
                    radio ? m_RadioChannel : m_PositionalChannel);
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void SampleEnergy()
        {
            var self = 0.0;
            foreach (var (channel, participants) in VivoxService.Instance.ActiveChannels)
            {
                foreach (var p in participants)
                {
                    if (p.IsSelf)
                    {
                        self = Math.Max(self, p.AudioEnergy);
                        continue;
                    }
                    var key = $"{Kind(channel)}:{p.DisplayName}";
                    m_PeakEnergy[key] = Math.Max(m_PeakEnergy.GetValueOrDefault(key), p.AudioEnergy);
                    m_SummaryPeakRemoteEnergy = Math.Max(m_SummaryPeakRemoteEnergy, p.AudioEnergy);
                    if (p.SpeechDetected)
                        m_SummarySpeechSamples++;
                }
            }
            GhostHearingLevel = GhostHearsMe && VoiceEnabled && !MicMuted ? self : 0;
        }

        private void LogMetrics()
        {
            var peers = m_PeakEnergy.Count == 0 ? "none" : string.Join(" ", m_PeakEnergy.Select(kv => $"{kv.Key}={kv.Value:F2}"));
            Debug.Log($"[Voice] tx={TransmitLabel} mic={(MicMuted ? "muted" : "on")} ghostHears={GhostHearingLevel:F2} peakRemoteEnergy: {peers}");
            m_PeakEnergy.Clear();
        }

        public string Summary =>
            $"voiceJoined={Joined} peakRemoteEnergy={m_SummaryPeakRemoteEnergy:F2} speechFrames={m_SummarySpeechSamples} textReceived={m_SummaryMessages}";

        public IEnumerable<(string Name, string Channel, bool Speaking)> Participants =>
            Joined
                ? VivoxService.Instance.ActiveChannels.SelectMany(c => c.Value.Select(p => (p.DisplayName, Kind(c.Key), p.SpeechDetected)))
                : Enumerable.Empty<(string, string, bool)>();

        public async Task LeaveAsync()
        {
            if (!Joined)
                return;
            Joined = false;
            if (!string.IsNullOrEmpty(NetSyncProtoRunner.Arg("-proto-voice-inject")))
                VivoxService.Instance.StopAudioInjection();
            await VivoxService.Instance.LeaveAllChannelsAsync();
            await VivoxService.Instance.LogoutAsync();
            Debug.Log("[Voice] left");
        }

        private string Kind(string channel) => channel == m_PositionalChannel ? "proximity" : channel == m_RadioChannel ? "radio" : channel;
    }
}
