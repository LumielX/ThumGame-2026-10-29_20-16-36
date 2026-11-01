using System.Linq;
using NUnit.Framework;
using SpiritMovers.Core.Ghost;
using SpiritMovers.Core.Voice;

namespace SpiritMovers.Tests
{
    public class VoiceRulesTests
    {
        [Test]
        public void ChannelsFor_VanHouseNone()
        {
            var van = VoiceRules.ChannelsFor(VoiceSpace.Van, "AB12");
            Assert.AreEqual(1, van.Count);
            Assert.AreEqual("van-AB12", van[0].Name);
            Assert.AreEqual(VoiceChannelKind.Group, van[0].Kind);
            Assert.IsTrue(van[0].Text);

            var house = VoiceRules.ChannelsFor(VoiceSpace.House, "AB12");
            Assert.AreEqual(2, house.Count);
            Assert.AreEqual("house-AB12", house[0].Name, "positional first (Vivox docs)");
            Assert.AreEqual(VoiceChannelKind.Positional, house[0].Kind);
            Assert.IsFalse(house[0].Text);
            Assert.AreEqual("radio-AB12", house[1].Name);
            Assert.AreEqual(VoiceChannelKind.Group, house[1].Kind);
            Assert.IsTrue(house[1].Text);

            Assert.AreEqual(0, VoiceRules.ChannelsFor(VoiceSpace.None, "AB12").Count);
            Assert.AreEqual(0, VoiceRules.ChannelsFor(VoiceSpace.Van, "").Count);
            Assert.AreEqual(0, VoiceRules.ChannelsFor(VoiceSpace.House, null).Count);
        }

        [Test]
        public void TransmitTo_RadioAddsRadioChannel()
        {
            CollectionAssert.AreEqual(new[] { "house-AB12" }, VoiceRules.TransmitTo(VoiceSpace.House, "AB12", false).ToArray());
            CollectionAssert.AreEqual(new[] { "house-AB12", "radio-AB12" }, VoiceRules.TransmitTo(VoiceSpace.House, "AB12", true).ToArray());
            CollectionAssert.AreEqual(new[] { "van-AB12" }, VoiceRules.TransmitTo(VoiceSpace.Van, "AB12", true).ToArray());
            Assert.AreEqual(0, VoiceRules.TransmitTo(VoiceSpace.None, "AB12", true).Count);
        }

        [Test]
        public void ChatChannel_VanOrRadio()
        {
            Assert.AreEqual("van-AB12", VoiceRules.ChatChannel(VoiceSpace.Van, "AB12"));
            Assert.AreEqual("radio-AB12", VoiceRules.ChatChannel(VoiceSpace.House, "AB12"));
            Assert.IsNull(VoiceRules.ChatChannel(VoiceSpace.None, "AB12"));
            Assert.IsNull(VoiceRules.ChatChannel(VoiceSpace.Van, ""));
        }

        [Test]
        public void Radios_ByCrew()
        {
            CollectionAssert.AreEqual(new[] { 0, 2, 2, 2, 3, 3 }, Enumerable.Range(1, 6).Select(ToolConfig.Radios).ToArray());
        }

        [Test]
        public void MicNoiseGate_OnlyWhenEnabledTransmittingLoud()
        {
            Assert.IsFalse(new MicNoiseGate().TryReport(enabled: false, transmitting: true, energy: 0.9, now: 0f));
            Assert.IsFalse(new MicNoiseGate().TryReport(enabled: true, transmitting: false, energy: 0.9, now: 0f));
            Assert.IsFalse(new MicNoiseGate().TryReport(enabled: true, transmitting: true, energy: 0.59, now: 0f));
            Assert.IsTrue(new MicNoiseGate().TryReport(enabled: true, transmitting: true, energy: 0.6, now: 0f));
        }

        [Test]
        public void MicNoiseGate_AtMostFourPerSecond()
        {
            var gate = new MicNoiseGate();
            var reports = 0;
            for (var t = 0f; t < 1f; t += 0.05f)
                if (gate.TryReport(true, true, 0.9, t))
                    reports++;
            Assert.LessOrEqual(reports, 4);
            Assert.GreaterOrEqual(reports, 3);
        }

        [Test]
        public void RadioInterference_ByStage()
        {
            for (var t = 0f; t < 10f; t += 0.13f)
            {
                Assert.AreEqual((0f, false), RadioInterference.At(0, t, 7));
                Assert.AreEqual((0f, false), RadioInterference.At(1, t, 7));
                Assert.AreEqual((0.3f, false), RadioInterference.At(2, t, 7));
            }
            Assert.AreEqual(0.5f, RadioInterference.At(3, 0f, 7).Crackle);
            Assert.AreEqual(0.7f, RadioInterference.At(4, 0f, 7).Crackle);
            var dropped = Enumerable.Range(0, 100).Count(i => RadioInterference.At(4, i * 0.4f + 0.2f, 7).Dropped);
            Assert.That(dropped, Is.InRange(40, 60), "about half of the 0.4 s slots go silent at stage 4");
            var some = Enumerable.Range(0, 100).Count(i => RadioInterference.At(3, i * 0.4f + 0.2f, 7).Dropped);
            Assert.That(some, Is.InRange(15, 35), "about a quarter at stage 3");
            Assert.AreEqual(RadioInterference.At(4, 3.3f, 7), RadioInterference.At(4, 3.3f, 7));
        }

        [Test]
        public void ChatText_CleansAndCaps()
        {
            Assert.AreEqual("สวัสดี ทุกคน", ChatText.Clean("  สวัสดี\u0007  ทุกคน "));
            Assert.AreEqual(ChatText.MaxLength, ChatText.Clean(new string('ก', 300)).Length);
            Assert.AreEqual("", ChatText.Clean("\n\n"));
            Assert.AreEqual("", ChatText.Clean(null));
            Assert.AreEqual("ไปกัน", ChatText.Clean("😀ไปกัน"));
            Assert.AreEqual("ok? 100% ไปเลย!", ChatText.Clean("ok? 100% ไปเลย!"));
            Assert.AreEqual("a b", ChatText.Clean("a\nb"), "line breaks become spaces");
        }

        [Test]
        public void QuickMessage_EveryKeyDistinct()
        {
            var keys = System.Enum.GetValues(typeof(QuickMessage)).Cast<QuickMessage>().Select(ChatText.KeyFor).ToList();
            Assert.AreEqual(6, keys.Count);
            Assert.AreEqual(6, keys.Distinct().Count());
            Assert.IsTrue(keys.All(k => k.StartsWith("quick.")));
        }

        [Test]
        public void PingRules_CooldownAndMaxActive()
        {
            var rules = new PingRules();
            Assert.IsTrue(rules.TryAdd(1, 0.0f));
            Assert.IsFalse(rules.TryAdd(1, 0.2f), "cooldown");
            Assert.IsTrue(rules.TryAdd(1, 0.6f));
            Assert.IsTrue(rules.TryAdd(1, 1.2f));
            Assert.IsFalse(rules.TryAdd(1, 1.8f), "three already up");
            Assert.AreEqual(3, rules.ActiveFor(1, 1.8f));
            Assert.IsTrue(rules.TryAdd(2, 1.8f), "someone else is not limited by player 1");
            Assert.IsTrue(rules.TryAdd(1, 6.1f), "the first one faded");
        }
    }
}
