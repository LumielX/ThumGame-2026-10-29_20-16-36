using System;
using System.Collections.Generic;

namespace SpiritMovers.Core.Voice
{
    public enum VoiceChannelKind
    {
        Group,
        Positional,
    }

    /// <summary>Where the team is, for voice: in the van everyone hears everyone, in the house only people nearby.</summary>
    public enum VoiceSpace
    {
        None,
        Van,
        House,
    }

    /// <summary>One Vivox channel: its name, whether it is positional, and whether it carries text chat.</summary>
    public readonly struct VoiceChannel : IEquatable<VoiceChannel>
    {
        public readonly string Name;
        public readonly VoiceChannelKind Kind;
        public readonly bool Text;

        public VoiceChannel(string name, VoiceChannelKind kind, bool text)
        {
            Name = name;
            Kind = kind;
            Text = text;
        }

        public bool Equals(VoiceChannel other) => Name == other.Name && Kind == other.Kind && Text == other.Text;
        public override bool Equals(object obj) => obj is VoiceChannel other && Equals(other);
        public override int GetHashCode() => Name?.GetHashCode() ?? 0;
        public override string ToString() => $"{Name} ({Kind}{(Text ? ", text" : "")})";
    }

    /// <summary>
    /// Which voice channels a room uses where (GDD 10, TECHNICAL_DESIGN 8.2, M4C plan): the van is one group channel;
    /// the house is a positional channel (joined first) plus the team radio, which also carries the job's chat.
    /// </summary>
    public static class VoiceRules
    {
        /// <summary>Full volume up to here, silent from SilentDistance (M2 prototype values).</summary>
        public const float FullVolumeDistance = 2f;
        public const float SilentDistance = 25f;
        /// <summary>Positions go to Vivox 4 times a second (TECHNICAL_DESIGN 8.2: 2–4).</summary>
        public const float PositionInterval = 0.25f;

        public static IReadOnlyList<VoiceChannel> ChannelsFor(VoiceSpace space, string roomCode)
        {
            if (string.IsNullOrEmpty(roomCode))
                return Array.Empty<VoiceChannel>();
            return space switch
            {
                VoiceSpace.Van => new[] { new VoiceChannel(Van(roomCode), VoiceChannelKind.Group, true) },
                VoiceSpace.House => new[]
                {
                    new VoiceChannel(House(roomCode), VoiceChannelKind.Positional, false),
                    new VoiceChannel(Radio(roomCode), VoiceChannelKind.Group, true),
                },
                _ => Array.Empty<VoiceChannel>(),
            };
        }

        /// <summary>Where this player's voice goes: talking over the radio is still heard by people nearby.</summary>
        public static IReadOnlyList<string> TransmitTo(VoiceSpace space, string roomCode, bool radioHeld)
        {
            if (string.IsNullOrEmpty(roomCode))
                return Array.Empty<string>();
            return space switch
            {
                VoiceSpace.Van => new[] { Van(roomCode) },
                VoiceSpace.House => radioHeld ? new[] { House(roomCode), Radio(roomCode) } : new[] { House(roomCode) },
                _ => Array.Empty<string>(),
            };
        }

        /// <summary>The channel text chat goes to: the van's, or the team radio's during a job.</summary>
        public static string ChatChannel(VoiceSpace space, string roomCode)
        {
            if (string.IsNullOrEmpty(roomCode))
                return null;
            return space switch
            {
                VoiceSpace.Van => Van(roomCode),
                VoiceSpace.House => Radio(roomCode),
                _ => null,
            };
        }

        private static string Van(string code) => $"van-{code}";
        private static string House(string code) => $"house-{code}";
        private static string Radio(string code) => $"radio-{code}";
    }
}
