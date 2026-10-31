using System.Numerics;
using NUnit.Framework;
using SpiritMovers.Core.Ghost;

namespace SpiritMovers.Tests
{
    public class NoiseTests
    {
        private static NoiseEvent Noise(float loudness) => new(Vector3.Zero, loudness, NoiseSource.Footstep);

        [Test]
        public void NoiseEvent_ClampsLoudness()
        {
            Assert.AreEqual(1f, Noise(1.7f).Loudness);
            Assert.AreEqual(0f, Noise(-1f).Loudness);
        }

        [Test]
        public void LoudNoiseGate_AcceptsLoudOncePerCooldown()
        {
            var gate = new LoudNoiseGate(new NoiseConfig());
            Assert.IsTrue(gate.TryAccept(Noise(1f), now: 0f));
            Assert.IsFalse(gate.TryAccept(Noise(1f), now: 2f));
            Assert.IsTrue(gate.TryAccept(Noise(1f), now: 5.1f));
            Assert.IsFalse(gate.TryAccept(Noise(0.5f), now: 20f));
            Assert.IsFalse(gate.TryAccept(Noise(0.5f), now: 40f));
        }

        [Test]
        public void ImpactLoudness_ScalesAndCaps()
        {
            var config = new NoiseConfig();
            Assert.AreEqual(0f, config.ImpactLoudness(1.5f));
            Assert.AreEqual(0.5f, config.ImpactLoudness(3f), 1e-4f);
            Assert.AreEqual(1f, config.ImpactLoudness(12f));
        }
    }
}
