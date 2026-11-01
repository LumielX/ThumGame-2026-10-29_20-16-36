using System;
using SpiritMovers.Core.Voice;
using UnityEngine;

namespace SpiritMovers.Voice
{
    /// <summary>
    /// The walkie-talkie sound on one teammate's radio voice (M2 Audio Tap test: high-pass 400 Hz → low-pass 3,200 Hz
    /// → distortion 0.35), plus the angry ghost's static and drop-outs (RadioInterference). Sits after the tap so
    /// the filters run on the voice; the last stage adds static and measures the level.
    /// </summary>
    public sealed class RadioFilter : MonoBehaviour
    {
        private const float HighPassHz = 400f;
        private const float LowPassHz = 3200f;
        private const float Distortion = 0.35f;
        private const float StaticLevel = 0.15f;

        private readonly System.Random m_Noise = new();
        private int m_Seed;
        // Read on the audio thread.
        private volatile float m_AudioCrackle;
        private volatile bool m_AudioDropped;
        private float m_Peak;

        public AudioSource Source { get; private set; }
        public float Crackle { get; private set; }
        public bool Dropped { get; private set; }

        /// <summary>Add the radio chain to a tapped voice; <paramref name="seed"/> keeps every listener's drop-outs alike.</summary>
        public static RadioFilter Attach(AudioSource source, int seed)
        {
            var go = source.gameObject;
            go.AddComponent<AudioHighPassFilter>().cutoffFrequency = HighPassHz;
            go.AddComponent<AudioLowPassFilter>().cutoffFrequency = LowPassHz;
            go.AddComponent<AudioDistortionFilter>().distortionLevel = Distortion;
            var filter = go.AddComponent<RadioFilter>();
            filter.Source = source;
            filter.m_Seed = seed;
            return filter;
        }

        public void Apply(int angerStage, float time)
        {
            (Crackle, Dropped) = RadioInterference.At(angerStage, time, m_Seed);
            m_AudioCrackle = Crackle;
            m_AudioDropped = Dropped;
        }

        /// <summary>Peak level after the radio chain since the last call (bots, tests).</summary>
        public float TakePeak()
        {
            var peak = m_Peak;
            m_Peak = 0f;
            return peak;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (m_AudioDropped)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }
            var crackle = m_AudioCrackle * StaticLevel;
            var peak = m_Peak;
            for (var i = 0; i < data.Length; i++)
            {
                if (crackle > 0f)
                    data[i] += (float)(m_Noise.NextDouble() * 2.0 - 1.0) * crackle;
                var level = data[i] < 0f ? -data[i] : data[i];
                if (level > peak)
                    peak = level;
            }
            m_Peak = peak;
        }
    }
}
