using SpiritMovers.Core.Ghost;
using SpiritMovers.Core.Voice;
using SpiritMovers.Ghost;
using SpiritMovers.Player;
using SpiritMovers.Save;
using Unity.Netcode;
using UnityEngine;

namespace SpiritMovers.Voice
{
    /// <summary>
    /// The sharp-eared ghost hears a loud microphone (TECHNICAL_DESIGN 8.3, D-010). The owner checks its own voice
    /// level — only if the player allows it ("let the ghost hear my mic") and is actually sending — and reports at
    /// most 4 times a second; the host checks the rate again per player before the ghost hears it.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class MicNoise : NetworkBehaviour
    {
        private readonly MicNoiseGate m_OwnerGate = new();
        // Host side: the client judged the level; the host only holds the rate down.
        private readonly MicNoiseGate m_ServerGate = new(threshold: 0d);
        private PlayerMotor m_Motor;

        /// <summary>Reports this machine sent (tests: none at all when the option is off).</summary>
        public int ReportsSent { get; private set; }

        private void Awake() => m_Motor = GetComponent<PlayerMotor>();

        private void Update()
        {
            if (!IsSpawned || !IsOwner || NoiseHub.Current == null)
                return;
            var voice = VoiceDirector.Instance;
            if (voice == null || voice.State != VoiceState.On)
                return;
            var energy = voice.Service.LocalEnergy;
            if (!m_OwnerGate.TryReport(GameData.Settings.ghostHearsMic, voice.Transmitting, energy, Time.time))
                return;
            ReportsSent++;
            ReportVoiceRpc((float)energy);
        }

        [Rpc(SendTo.Server)]
        internal void ReportVoiceRpc(float energy)
        {
            var hub = NoiseHub.Current;
            if (hub == null)
                return;
            var loudness = float.IsNaN(energy) ? 0f : Mathf.Clamp01(energy);
            if (!m_ServerGate.TryReport(true, true, loudness, Time.time))
                return;
            hub.Report(new NoiseEvent(m_Motor.Head.position.ToNumerics(), loudness, NoiseSource.Voice));
        }
    }
}
