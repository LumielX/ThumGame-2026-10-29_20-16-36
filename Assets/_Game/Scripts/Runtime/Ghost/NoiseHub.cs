using System;
using System.Collections.Generic;
using SpiritMovers.Core.Ghost;
using Unity.Netcode;
using UnityEngine;

namespace SpiritMovers.Ghost
{
    /// <summary>
    /// Host: every sound the ghost may hear goes through here (TECHNICAL_DESIGN 5 <see cref="NoiseEvent"/>) —
    /// footsteps reported by their owners, item impacts seen by the host; voice and radio come in M4.
    /// Lives on the job director object.
    /// </summary>
    public sealed class NoiseHub : MonoBehaviour
    {
        private const int RecentCapacity = 10;

        private readonly List<NoiseEvent> m_Recent = new();

        public static NoiseHub Current { get; private set; }

        public NoiseConfig Config { get; } = new();
        /// <summary>Time of the last noise loud enough to break the silence (crouch-walking does not).</summary>
        public float LastNoiseTime { get; private set; } = float.NegativeInfinity;
        /// <summary>The last few noises, newest last (tests and logs).</summary>
        public IReadOnlyList<NoiseEvent> Recent => m_Recent;

        public event Action<NoiseEvent> Heard;

        private void Awake() => Current = this;

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        /// <summary>Host only; ignored elsewhere.</summary>
        public void Report(NoiseEvent e)
        {
            var network = NetworkManager.Singleton;
            if (network == null || !network.IsServer)
                return;
            if (m_Recent.Count == RecentCapacity)
                m_Recent.RemoveAt(0);
            m_Recent.Add(e);
            if (e.Loudness >= Config.SilenceLoudness)
                LastNoiseTime = Time.time;
            Heard?.Invoke(e);
        }
    }
}
