using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpiritMovers.Core.Save;
using SpiritMovers.Core.Voice;
using SpiritMovers.Net;
using SpiritMovers.Player;
using SpiritMovers.Save;
using SpiritMovers.Session;
using SpiritMovers.Settings;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpiritMovers.Voice
{
    public enum VoiceState
    {
        Off,
        Connecting,
        On,
        Lost,
        Unavailable,
    }

    /// <summary>
    /// Voice for the whole game (GDD 10, TECHNICAL_DESIGN 8, D-009), on the persistent GameNetwork object: in an online
    /// room it follows the team — the van's channel, then the house (positional) and the radio — and leaves and logs
    /// out with the room. Solo never uses voice. Voice trouble never touches the game: it retries every 5 s, 12 times.
    /// </summary>
    public sealed class VoiceDirector : MonoBehaviour
    {
        public const float RetrySeconds = 5f;
        public const int MaxRetries = 12;

        private IVoiceService m_Vivox;
        private IVoiceService m_Override;
        private IVoiceService m_Subscribed;
        private VoiceSpace m_TargetSpace;
        private string m_TargetCode;
        private bool m_Syncing;
        private int m_Retries;
        private float m_RetryAt;
        private float m_NextPosition;
        private string m_LastTransmit;
        private bool? m_LastMute;
        private float m_LastVolume = -1f;
        private readonly Dictionary<string, bool> m_PlayerMuted = new();
        private readonly Dictionary<string, float> m_PlayerVolume = new();

        public static VoiceDirector Instance { get; private set; }

        /// <summary>Tests and dev bots: use this service instead of Vivox (set before entering a room).</summary>
        public IVoiceService ServiceOverride
        {
            set => m_Override = value;
        }

        public IVoiceService Service => m_Override ?? (m_Vivox ??= new VivoxVoiceService());
        public VoiceSpace Space { get; private set; }
        public string RoomCode { get; private set; }
        public VoiceState State { get; private set; }
        /// <summary>The local player is holding a walkie-talkie and pressing to talk (set by Walkie).</summary>
        public bool RadioHeld { get; set; }
        /// <summary>Our voice is going out right now.</summary>
        public bool Transmitting => State == VoiceState.On && Space != VoiceSpace.None && !Service.MicMuted;

        public event Action StateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
                return;
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            Unsubscribe();
        }

        private void Update()
        {
            var (space, code) = Desired();
            if (!m_Syncing && (space != m_TargetSpace || code != m_TargetCode))
            {
                m_TargetSpace = space;
                m_TargetCode = code;
                m_Retries = 0;
                _ = SyncAsync();
            }
            if (State == VoiceState.Lost && !m_Syncing && Time.unscaledTime >= m_RetryAt)
                RetryNow();
            if (State == VoiceState.On)
                Drive();
        }

        /// <summary>Try to get voice back now (the 5 s timer calls this; tests call it directly).</summary>
        internal void RetryNow()
        {
            if (m_Syncing || State != VoiceState.Lost)
                return;
            m_Retries++;
            _ = SyncAsync();
        }

        /// <summary>Mute one player for this session (D-009); applied again whenever voice joins new channels.</summary>
        public void SetPlayerMuted(string playerId, bool muted)
        {
            m_PlayerMuted[playerId] = muted;
            if (State == VoiceState.On)
                Service.SetParticipantMuted(playerId, muted);
        }

        public void SetPlayerVolume(string playerId, float gain01)
        {
            m_PlayerVolume[playerId] = Mathf.Clamp01(gain01);
            if (State == VoiceState.On)
                Service.SetParticipantVolume(playerId, m_PlayerVolume[playerId]);
        }

        public bool IsPlayerMuted(string playerId) => m_PlayerMuted.TryGetValue(playerId, out var muted) && muted;
        public float PlayerVolume(string playerId) => m_PlayerVolume.TryGetValue(playerId, out var gain) ? gain : 1f;

        /// <summary>That player (or we ourselves) is talking right now.</summary>
        public bool IsSpeaking(string playerId)
        {
            if (State != VoiceState.On || string.IsNullOrEmpty(playerId))
                return false;
            foreach (var p in Service.Participants)
                if (p.PlayerId == playerId && p.Speaking)
                    return true;
            return false;
        }

        /// <summary>The settings page changed the microphone or speaker.</summary>
        public void ApplyDevices()
        {
            if (State == VoiceState.On)
                _ = Guard(ApplyDevicesAsync());
        }

        /// <summary>Online room + van or house scene → that space; anything else (menu, solo, no room) → none.</summary>
        private static (VoiceSpace, string) Desired()
        {
            var session = GameSession.Instance;
            var network = NetworkManager.Singleton;
            if (session == null || session.IsSolo || string.IsNullOrEmpty(session.JoinCode) || network == null || !network.IsListening)
                return (VoiceSpace.None, null);
            var scene = SceneManager.GetActiveScene().name;
            if (scene == GameSession.LobbyScene)
                return (VoiceSpace.Van, session.JoinCode);
            return scene == GameSession.JobScene ? (VoiceSpace.House, session.JoinCode) : (VoiceSpace.None, null);
        }

        private async Task SyncAsync()
        {
            m_Syncing = true;
            try
            {
                var target = VoiceRules.ChannelsFor(m_TargetSpace, m_TargetCode);
                if (target.Count == 0)
                {
                    // Only a service we used is told to log out: solo and the menu never touch voice.
                    if (State != VoiceState.Off)
                        await Service.LogoutAsync();
                    Space = VoiceSpace.None;
                    RoomCode = null;
                    m_PlayerMuted.Clear();
                    m_PlayerVolume.Clear();
                    SetState(VoiceState.Off);
                    return;
                }
                Subscribe();
                if (!Service.IsLoggedIn)
                {
                    SetState(VoiceState.Connecting);
                    await Service.LoginAsync(GameData.PlayerName);
                    await ApplyDevicesAsync();
                }
                foreach (var name in Service.Joined.Where(n => target.All(c => c.Name != n)).ToList())
                    await Service.LeaveAsync(name);
                foreach (var channel in target)
                    if (!Service.Joined.Contains(channel.Name))
                        await Service.JoinAsync(channel);
                Space = m_TargetSpace;
                RoomCode = m_TargetCode;
                m_Retries = 0;
                m_LastTransmit = null;
                m_LastMute = null;
                m_LastVolume = -1f;
                foreach (var (id, muted) in m_PlayerMuted)
                    Service.SetParticipantMuted(id, muted);
                foreach (var (id, gain) in m_PlayerVolume)
                    Service.SetParticipantVolume(id, gain);
                SetState(VoiceState.On);
                Debug.Log($"[Voice] {Space}: {string.Join(", ", target.Select(c => c.Name))}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Voice] {e.GetType().Name}: {e.Message} (try {m_Retries}/{MaxRetries})");
                Lose();
            }
            finally
            {
                m_Syncing = false;
            }
        }

        /// <summary>Microphone, where our voice goes, how loud others are, and where we stand — every frame.</summary>
        private void Drive()
        {
            var local = PlayerMotor.Local;
            var talkHeld = local != null && local.CurrentInput.TalkHeld;
            var mute = GameData.Settings.pushToTalk && !talkHeld && !RadioHeld;
            if (m_LastMute != mute)
            {
                m_LastMute = mute;
                Service.MicMuted = mute;
            }
            var transmit = VoiceRules.TransmitTo(Space, RoomCode, RadioHeld);
            var key = string.Join(",", transmit);
            if (key != m_LastTransmit)
            {
                m_LastTransmit = key;
                _ = Guard(Service.TransmitAsync(transmit));
            }
            var gain = GameAudio.Gain(AudioGroup.Voice);
            if (Mathf.Abs(gain - m_LastVolume) > 0.001f)
            {
                m_LastVolume = gain;
                Service.SetOutputVolume(gain);
            }
            if (Space == VoiceSpace.House && local != null && Time.unscaledTime >= m_NextPosition)
            {
                m_NextPosition = Time.unscaledTime + VoiceRules.PositionInterval;
                Service.UpdatePosition(local.Head, VoiceRules.ChannelsFor(VoiceSpace.House, RoomCode)[0].Name);
            }
        }

        private async Task ApplyDevicesAsync()
        {
            await Service.SetInputDeviceAsync(GameData.Settings.micDevice);
            await Service.SetOutputDeviceAsync(GameData.Settings.speakerDevice);
        }

        private void Lose()
        {
            m_RetryAt = Time.unscaledTime + RetrySeconds;
            SetState(m_Retries >= MaxRetries ? VoiceState.Unavailable : VoiceState.Lost);
        }

        private void OnConnectionLost()
        {
            Debug.LogWarning("[Voice] connection lost; the game goes on");
            Lose();
        }

        private void SetState(VoiceState state)
        {
            if (State == state)
                return;
            State = state;
            StateChanged?.Invoke();
        }

        private void Subscribe()
        {
            if (m_Subscribed == Service)
                return;
            Unsubscribe();
            m_Subscribed = Service;
            m_Subscribed.ConnectionLost += OnConnectionLost;
        }

        private void Unsubscribe()
        {
            if (m_Subscribed != null)
                m_Subscribed.ConnectionLost -= OnConnectionLost;
            m_Subscribed = null;
        }

        /// <summary>A failed voice call is logged, never thrown into the game.</summary>
        private static async Task Guard(Task task)
        {
            try
            {
                await task;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Voice] {e.GetType().Name}: {e.Message}");
            }
        }
    }
}
