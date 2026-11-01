namespace SpiritMovers.Core.Voice
{
    /// <summary>
    /// An angry ghost spoils the radio (GDD 6.2 "วิทยุซ่า", D-010): static from stage 2, and from stage 3 the signal cuts
    /// out for whole 0.4 s slots (⚙ M4C plan). Deterministic from the time and a seed so every listener can agree.
    /// </summary>
    public static class RadioInterference
    {
        public const float SlotSeconds = 0.4f;

        public static (float Crackle, bool Dropped) At(int angerStage, float time, int seed)
        {
            var (crackle, dropRate) = angerStage switch
            {
                <= 1 => (0f, 0f),
                2 => (0.3f, 0f),
                3 => (0.5f, 0.25f),
                _ => (0.7f, 0.5f),
            };
            if (dropRate <= 0f)
                return (crackle, false);
            var slot = (int)System.Math.Floor(time / SlotSeconds);
            return (crackle, Unit(seed, slot) < dropRate);
        }

        /// <summary>A well-mixed number in [0, 1) for this seed and slot (SplitMix32-style).</summary>
        private static float Unit(int seed, int slot)
        {
            unchecked
            {
                var h = (uint)seed * 0x9E3779B9u + (uint)slot * 0x85EBCA6Bu;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return (h >> 8) / (float)(1 << 24);
            }
        }
    }
}
