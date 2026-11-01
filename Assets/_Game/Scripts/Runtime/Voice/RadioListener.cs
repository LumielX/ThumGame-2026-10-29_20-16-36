using System.Collections.Generic;
using System.Linq;
using SpiritMovers.Core.Voice;
using SpiritMovers.Job;
using SpiritMovers.Net;
using UnityEngine;

namespace SpiritMovers.Voice
{
    /// <summary>
    /// In the job scene: every teammate on the radio is heard through a tap and the walkie-talkie filter (TECHNICAL_DESIGN
    /// 8.2) — but only while this player holds a walkie. A channel tap only works on the first channel joined (M2), so
    /// it is one participant tap per speaker; taps go when the speaker leaves the radio, voice drops, or the job ends.
    /// </summary>
    public sealed class RadioListener : MonoBehaviour
    {
        private readonly Dictionary<string, RadioFilter> m_Filters = new();
        private IVoiceService m_Service;
        private string m_Channel;
        private JobDirector m_Director;

        /// <summary>The filter on a teammate's radio voice, if they are tapped (tests, bots).</summary>
        public RadioFilter FilterFor(string playerId) => m_Filters.TryGetValue(playerId, out var filter) ? filter : null;

        private void Start() => m_Director = FindAnyObjectByType<JobDirector>();

        private void Update()
        {
            var voice = VoiceDirector.Instance;
            var on = voice != null && voice.State == VoiceState.On && voice.Space == VoiceSpace.House;
            var channel = on ? VoiceRules.ChannelsFor(VoiceSpace.House, voice.RoomCode)[1].Name : null;
            var service = on ? voice.Service : null;
            if (channel != m_Channel || service != m_Service)
            {
                ReleaseAll();
                m_Channel = channel;
                m_Service = service;
            }
            if (m_Service == null)
                return;

            var speakers = m_Service.Participants.Where(p => p.Channel == m_Channel && !p.IsSelf).Select(p => p.PlayerId).ToHashSet();
            foreach (var gone in m_Filters.Keys.Where(id => !speakers.Contains(id)).ToList())
            {
                m_Service.ReleaseTap(gone, m_Channel);
                m_Filters.Remove(gone);
            }
            foreach (var id in speakers)
                if (!m_Filters.ContainsKey(id))
                    m_Filters[id] = RadioFilter.Attach(m_Service.TapParticipant(id, m_Channel), Seed(voice.RoomCode));

            var hear = Walkie.LocalHolds;
            var stage = m_Director != null ? m_Director.AngerStage : 0;
            foreach (var filter in m_Filters.Values)
            {
                if (filter == null)
                    continue;
                filter.Source.mute = !hear;
                filter.Apply(stage, Time.time);
            }
        }

        private void OnDestroy() => ReleaseAll();

        private void ReleaseAll()
        {
            if (m_Service != null)
                foreach (var id in m_Filters.Keys)
                    m_Service.ReleaseTap(id, m_Channel);
            m_Filters.Clear();
        }

        /// <summary>The same seed on every machine of the room, so the ghost's drop-outs line up.</summary>
        private static int Seed(string roomCode)
        {
            var seed = 17;
            foreach (var c in roomCode ?? "")
                seed = unchecked(seed * 31 + c);
            return seed;
        }
    }
}
