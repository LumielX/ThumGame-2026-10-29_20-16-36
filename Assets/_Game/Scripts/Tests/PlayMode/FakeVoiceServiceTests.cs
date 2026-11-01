using System;
using System.Collections;
using NUnit.Framework;
using SpiritMovers.Core.Voice;
using SpiritMovers.Dev;
using SpiritMovers.Net;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    /// <summary>The contract every IVoiceService keeps; the fake stands in for Vivox in all other PlayMode tests.</summary>
    public class FakeVoiceServiceTests
    {
        private static readonly VoiceChannel Van = new("van-AB12", VoiceChannelKind.Group, true);

        [UnityTest]
        public IEnumerator Fake_JoinLeaveTracksChannels()
        {
            var voice = new FakeVoiceService();
            yield return Wait(voice.LoginAsync("บีม"));
            yield return Wait(voice.JoinAsync(Van));
            CollectionAssert.AreEquivalent(new[] { "van-AB12" }, voice.Joined);
            yield return Wait(voice.LeaveAsync("van-AB12"));
            Assert.AreEqual(0, voice.Joined.Count);
        }

        [UnityTest]
        public IEnumerator Fake_TextEchoesToSender()
        {
            var voice = new FakeVoiceService();
            yield return Wait(voice.LoginAsync("บีม"));
            yield return Wait(voice.JoinAsync(Van));
            (string Sender, string Channel, string Text) got = default;
            voice.TextReceived += (sender, channel, text) => got = (sender, channel, text);
            yield return Wait(voice.SendTextAsync("van-AB12", "สวัสดี"));
            Assert.AreEqual((voice.PlayerId, "van-AB12", "สวัสดี"), got);
        }

        [UnityTest]
        public IEnumerator Fake_LoginFailureThrows()
        {
            var voice = new FakeVoiceService { FailLogin = true };
            var login = voice.LoginAsync("บีม");
            while (!login.IsCompleted)
                yield return null;
            Assert.IsTrue(login.IsFaulted);
            Assert.IsFalse(voice.IsLoggedIn);
        }

        [UnityTest]
        public IEnumerator Fake_DropAndRecoverRaiseEvents()
        {
            var voice = new FakeVoiceService();
            yield return Wait(voice.LoginAsync("บีม"));
            yield return Wait(voice.JoinAsync(Van));
            int lost = 0, recovered = 0;
            voice.ConnectionLost += () => lost++;
            voice.ConnectionRecovered += () => recovered++;
            voice.SimulateDrop();
            Assert.AreEqual(1, lost);
            Assert.AreEqual(0, voice.Joined.Count);
            voice.SimulateRecover();
            Assert.AreEqual(1, recovered);
        }

        [UnityTest]
        public IEnumerator OutputVolume_MapsToVivoxRange()
        {
            Assert.AreEqual(0, VivoxVoiceService.ToVivoxVolume(1f));
            Assert.AreEqual(-50, VivoxVoiceService.ToVivoxVolume(0f));
            Assert.That(VivoxVoiceService.ToVivoxVolume(0.5f), Is.InRange(-49, -1));
            Assert.AreEqual(0, VivoxVoiceService.ToVivoxVolume(3f), "no boost past the default");
            yield break;
        }

        private static IEnumerator Wait(System.Threading.Tasks.Task task)
        {
            while (!task.IsCompleted)
                yield return null;
            if (task.IsFaulted)
                throw new Exception("task failed", task.Exception);
        }
    }
}
