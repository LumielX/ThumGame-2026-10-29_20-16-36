using System;
using Unity.Netcode;

namespace SpiritMovers.Net
{
    /// <summary>
    /// Counts payload bytes this peer receives from the transport. Excludes UDP/Relay headers,
    /// so real bandwidth is somewhat higher; good enough to compare against the Relay free tier.
    /// </summary>
    public sealed class TransportTrafficMeter : IDisposable
    {
        private readonly NetworkTransport m_Transport;
        private long m_WindowBytes;
        private double m_WindowStart;

        public long TotalBytesReceived { get; private set; }

        public TransportTrafficMeter(NetworkTransport transport, double now)
        {
            m_Transport = transport;
            m_WindowStart = now;
            m_Transport.OnTransportEvent += OnTransportEvent;
        }

        private void OnTransportEvent(NetworkEvent eventType, ulong clientId, ArraySegment<byte> payload, float receiveTime)
        {
            if (eventType != NetworkEvent.Data)
                return;
            TotalBytesReceived += payload.Count;
            m_WindowBytes += payload.Count;
        }

        /// <summary>Average kilobytes per second received since the previous call.</summary>
        public double SampleKilobytesPerSecond(double now)
        {
            var seconds = Math.Max(now - m_WindowStart, 0.001);
            var kbps = m_WindowBytes / 1024.0 / seconds;
            m_WindowBytes = 0;
            m_WindowStart = now;
            return kbps;
        }

        public void Dispose() => m_Transport.OnTransportEvent -= OnTransportEvent;
    }
}
