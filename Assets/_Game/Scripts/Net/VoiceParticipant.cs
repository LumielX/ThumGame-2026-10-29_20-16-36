namespace SpiritMovers.Net
{
    /// <summary>Someone in a voice channel: their UGS player id, the channel, and whether they are talking right now.</summary>
    public readonly struct VoiceParticipant
    {
        public readonly string PlayerId;
        public readonly string Channel;
        public readonly bool Speaking;
        public readonly double Energy;
        public readonly bool IsSelf;

        public VoiceParticipant(string playerId, string channel, bool speaking, double energy, bool isSelf)
        {
            PlayerId = playerId;
            Channel = channel;
            Speaking = speaking;
            Energy = energy;
            IsSelf = isSelf;
        }
    }
}
