using System;
using NUnit.Framework;
using SpiritMovers.Core;

namespace SpiritMovers.Tests
{
    public class GameVersionTests
    {
        [TestCase("0.1.0", 0, 1, 0)]
        [TestCase(" 1.2.3 ", 1, 2, 3)]
        [TestCase("10.0.25", 10, 0, 25)]
        public void TryParse_AcceptsMajorMinorPatch(string text, int major, int minor, int patch)
        {
            Assert.IsTrue(GameVersion.TryParse(text, out var version));
            Assert.AreEqual(new GameVersion(major, minor, patch), version);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("1.2")]
        [TestCase("1.2.3.4")]
        [TestCase("1.x.3")]
        [TestCase("-1.0.0")]
        public void TryParse_RejectsInvalidText(string text)
        {
            Assert.IsFalse(GameVersion.TryParse(text, out _));
        }

        [Test]
        public void Parse_InvalidText_Throws()
        {
            Assert.Throws<FormatException>(() => GameVersion.Parse("v1"));
        }

        [Test]
        public void CanPlayWith_RequiresExactSameVersion()
        {
            var host = GameVersion.Parse("0.1.0");

            Assert.IsTrue(host.CanPlayWith(GameVersion.Parse("0.1.0")));
            Assert.IsFalse(host.CanPlayWith(GameVersion.Parse("0.1.1")));
            Assert.IsFalse(host.CanPlayWith(GameVersion.Parse("0.2.0")));
        }

        [Test]
        public void ToString_RoundTrips()
        {
            Assert.AreEqual("2.10.7", GameVersion.Parse("2.10.7").ToString());
        }
    }
}
