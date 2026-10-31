using Unity.Services.Vivox.AudioTaps;
using UnityEngine;

namespace SpiritMovers.Prototypes.NetSync
{
    /// <summary>
    /// Throwaway M2 Audio Tap test (PROJECT_PLAN M2 item 4): routes one teammate's radio voice through Unity's
    /// audio so it sounds like a walkie-talkie (band-pass + light distortion). One per remote radio participant:
    /// a Channel Audio Tap only works on the first channel joined (proximity), so it cannot carry the radio.
    /// Proximity voice stays on Vivox's own output. Sits last in the filter chain and measures the processed level.
    /// </summary>
    public sealed class RadioVoiceEffect : MonoBehaviour
    {
        private const float HighPassHz = 400f;
        private const float LowPassHz = 3200f;
        private const float Distortion = 0.35f;

        private float m_PeakIn;   // written on the audio thread
        private int m_Blocks;

        public bool IsRunning => m_Tap != null && m_Tap.IsRunning;
        private VivoxParticipantTap m_Tap;

        /// <param name="playerId">Vivox participant PlayerId (not the display name).</param>
        public static RadioVoiceEffect Create(Transform parent, string channelName, string playerId)
        {
            // Built inactive so the tap gets its channel and participant before OnEnable registers it.
            var go = new GameObject($"RadioVoice {playerId}");
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            go.AddComponent<AudioSource>().spatialBlend = 0f; // radio is heard "in the ear", not in the world
            var tap = go.AddComponent<VivoxParticipantTap>();
            tap.ChannelName = channelName;
            tap.ParticipantName = playerId;
            tap.SilenceInChannelAudioMix = true; // heard only through the radio filter, not also straight from Vivox
            // Filters run in component order, after the tap has written the channel audio.
            go.AddComponent<AudioHighPassFilter>().cutoffFrequency = HighPassHz;
            go.AddComponent<AudioLowPassFilter>().cutoffFrequency = LowPassHz;
            go.AddComponent<AudioDistortionFilter>().distortionLevel = Distortion;
            var effect = go.AddComponent<RadioVoiceEffect>();
            effect.m_Tap = tap;
            go.SetActive(true);
            return effect;
        }

        /// <summary>Peak sample level after the radio filters since the last call; resets it.</summary>
        public float TakePeak()
        {
            var peak = m_PeakIn;
            m_PeakIn = 0f;
            return peak;
        }

        public int AudioBlocks => m_Blocks;

        private void OnAudioFilterRead(float[] data, int channels)
        {
            var peak = m_PeakIn;
            for (var i = 0; i < data.Length; i++)
            {
                var v = data[i] < 0 ? -data[i] : data[i];
                if (v > peak) peak = v;
            }
            m_PeakIn = peak;
            m_Blocks++;
        }
    }
}
