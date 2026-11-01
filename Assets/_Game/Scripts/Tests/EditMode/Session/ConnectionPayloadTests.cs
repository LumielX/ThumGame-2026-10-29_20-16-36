using System.Text;
using NUnit.Framework;
using SpiritMovers.Core.Save;
using SpiritMovers.Core.Session;

namespace SpiritMovers.Tests
{
    public class ConnectionPayloadTests
    {
        [Test]
        public void Payload_RoundTripsVersionAndName()
        {
            Assert.IsTrue(ConnectionPayload.TryRead(ConnectionPayload.Write("0.0.1", "บีม"), out var version, out var name));
            Assert.AreEqual("0.0.1", version);
            Assert.AreEqual("บีม", name);
        }

        [Test]
        public void Payload_OldFormatHasNoName()
        {
            Assert.IsTrue(ConnectionPayload.TryRead("0.0.1", out var version, out var name));
            Assert.AreEqual("0.0.1", version);
            Assert.AreEqual("", name);
        }

        [Test]
        public void Payload_HostileNameIsCleaned()
        {
            var text = ConnectionPayload.Write("0.0.1", new string('ก', 200) + "\n|x");
            Assert.IsTrue(ConnectionPayload.TryRead(text, out _, out var name));
            Assert.LessOrEqual(name.Length, PlayerName.MaxLength);
            // A client that skips Write still gets cleaned on the host.
            Assert.IsTrue(ConnectionPayload.TryRead("0.0.1\n" + new string('ข', 90) + "|\u0007\nตัวร้าย", out _, out var raw));
            Assert.AreEqual(PlayerName.Clean(new string('ข', 90) + "|\u0007\nตัวร้าย"), raw);
            Assert.IsFalse(ConnectionPayload.TryRead(new byte[] { 0xff, 0x00, 0xfe, 0x10 }, out _, out _));
            Assert.IsFalse(ConnectionPayload.TryRead((byte[])null, out _, out _));
            Assert.IsFalse(ConnectionPayload.TryRead("not a version\nบีม", out _, out _));
        }

        [Test]
        public void Payload_CarriesVoiceId()
        {
            Assert.IsTrue(ConnectionPayload.TryRead(ConnectionPayload.Write("0.0.2", "บีม", "abcDEF-12"), out var version, out var name, out var voiceId));
            Assert.AreEqual(("0.0.2", "บีม", "abcDEF-12"), (version, name, voiceId));
            Assert.IsTrue(ConnectionPayload.TryRead("0.0.2\nบีม\na b<c>", out _, out _, out var cleaned));
            Assert.AreEqual("abc", cleaned);
            Assert.IsTrue(ConnectionPayload.TryRead("0.0.2\nบีม", out _, out var twoLineName, out var none));
            Assert.AreEqual(("บีม", ""), (twoLineName, none));
            Assert.IsTrue(ConnectionPayload.TryRead("0.0.2\nบีม\n" + new string('x', 200), out _, out _, out var longId));
            Assert.AreEqual(64, longId.Length);
        }

        [Test]
        public void MemberName_FitsFixedStringWithoutSplitting()
        {
            var twenty = new string('ก', 20);
            Assert.AreEqual(twenty, PlayerName.FitUtf8(twenty, 61), "60 bytes fit");
            var fitted = PlayerName.FitUtf8(new string('ก', 25) + "ที่", 61);
            Assert.LessOrEqual(Encoding.UTF8.GetByteCount(fitted), 61);
            Assert.AreEqual(new string('ก', 20), fitted);
            Assert.AreEqual(new string('ก', 19), PlayerName.FitUtf8(new string('ก', 19) + "ที่ม", 61), "the letter goes with its marks");
            Assert.AreEqual("", PlayerName.FitUtf8(null, 61));
        }
    }
}
