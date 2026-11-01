using System.Collections;
using System.Linq;
using NUnit.Framework;
using SpiritMovers.Core.Voice;
using SpiritMovers.Dev;
using SpiritMovers.Save;
using SpiritMovers.Session;
using SpiritMovers.Voice;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class VoiceDirectorTests : GameFlowFixture
    {
        private static VoiceDirector Director => VoiceDirector.Instance;

        private static IEnumerator Until(System.Func<bool> condition, string what) => WaitFor(condition, 5f, what);

        [UnityTest]
        public IEnumerator Channels_VanThenHouseThenVan()
        {
            var voice = new FakeVoiceService();
            yield return OnlineIntoLobby(voice);
            yield return Until(() => Director.State == VoiceState.On, "voice on in the van");
            CollectionAssert.AreEquivalent(new[] { "van-FAKE01" }, voice.Joined);
            Assert.AreEqual(VoiceSpace.Van, Director.Space);

            yield return SetOffFromVan();
            yield return Until(() => voice.Joined.Contains("radio-FAKE01"), "house channels");
            CollectionAssert.AreEquivalent(new[] { "house-FAKE01", "radio-FAKE01" }, voice.Joined);
            var joins = voice.Calls.Where(c => c.StartsWith("Join ")).ToList();
            Assert.Less(joins.IndexOf("Join house-FAKE01"), joins.IndexOf("Join radio-FAKE01"), "positional first");
            Assert.IsTrue(voice.Calls.Contains("Leave van-FAKE01"));

            Object.FindAnyObjectByType<SpiritMovers.Job.JobDirector>().LeaveEarly();
            yield return Press(i => i.Next.ConfirmPressed = true);
            yield return Until(() => ActiveScene == GameSession.LobbyScene && voice.Joined.Contains("van-FAKE01"), "back in the van");
            CollectionAssert.AreEquivalent(new[] { "van-FAKE01" }, voice.Joined);
            Assert.AreEqual(1, voice.Calls.Count(c => c == "Join house-FAKE01"), "no second join");
        }

        [UnityTest]
        public IEnumerator Channels_LeaveRoomLogsOut()
        {
            var voice = new FakeVoiceService();
            yield return OnlineIntoLobby(voice);
            yield return Until(() => Director.State == VoiceState.On, "voice on");
            _ = Session.LeaveAsync();
            yield return Until(() => ActiveScene == GameSession.MainMenuScene && voice.Calls.Contains("Logout"), "logged out");
            Assert.AreEqual(0, voice.Joined.Count);
            Assert.AreEqual(1, voice.Calls.Count(c => c == "Logout"));
            Assert.AreEqual(VoiceState.Off, Director.State);
        }

        [UnityTest]
        public IEnumerator Solo_NoVoice()
        {
            var voice = new FakeVoiceService();
            Director.ServiceOverride = voice;
            yield return SoloIntoLobby();
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(0, voice.Calls.Count, "solo never touches voice");
            Assert.AreEqual(VoiceState.Off, Director.State);
        }

        [UnityTest]
        public IEnumerator PushToTalk_MutesUntilHeld()
        {
            var voice = new FakeVoiceService();
            yield return OnlineIntoLobby(voice);
            yield return Until(() => Director.State == VoiceState.On, "voice on");
            GameData.Settings.pushToTalk = true;
            yield return null;
            yield return null;
            Assert.IsTrue(voice.MicMuted, "push-to-talk, key up");
            LocalInput.Next.TalkHeld = true;
            yield return null;
            yield return null;
            Assert.IsFalse(voice.MicMuted, "key held");
            LocalInput.Next.TalkHeld = false;
            yield return null;
            yield return null;
            Assert.IsTrue(voice.MicMuted, "key released");
            GameData.Settings.pushToTalk = false;
            yield return null;
            yield return null;
            Assert.IsFalse(voice.MicMuted, "open mic");
        }

        [UnityTest]
        public IEnumerator Voice_LoginFailsGameGoesOn()
        {
            var voice = new FakeVoiceService { FailLogin = true };
            yield return OnlineIntoLobby(voice);
            yield return Until(() => Director.State == VoiceState.Lost, "voice lost");
            StringAssert.Contains("voice.lost", Object.FindAnyObjectByType<VoiceStatus>().LastKey);
            yield return SetOffFromVan();
            Assert.AreEqual(GameSession.JobScene, ActiveScene, "the job started without voice");
        }

        [UnityTest]
        public IEnumerator Voice_DropMidJob_Reconnects()
        {
            var voice = new FakeVoiceService();
            yield return OnlineIntoJob(voice);
            yield return Until(() => Director.State == VoiceState.On && voice.Joined.Count == 2, "voice on in the house");
            voice.SimulateDrop();
            Assert.AreEqual(VoiceState.Lost, Director.State);
            Director.RetryNow();
            yield return Until(() => Director.State == VoiceState.On, "rejoined");
            CollectionAssert.AreEquivalent(new[] { "house-FAKE01", "radio-FAKE01" }, voice.Joined);
            Assert.AreEqual(2, voice.Calls.Count(c => c == "Join house-FAKE01"), "joined once before the drop and once after");
        }

        [UnityTest]
        public IEnumerator Voice_GivesUpAfterRetries()
        {
            var voice = new FakeVoiceService { FailLogin = true };
            yield return OnlineIntoLobby(voice);
            yield return Until(() => Director.State == VoiceState.Lost, "voice lost");
            for (var i = 0; i < VoiceDirector.MaxRetries; i++)
            {
                Director.RetryNow();
                yield return null;
            }
            yield return Until(() => Director.State == VoiceState.Unavailable, "gave up");
            StringAssert.Contains("voice.unavailable", Object.FindAnyObjectByType<VoiceStatus>().LastKey);
            Assert.IsTrue(NetworkManager.Singleton.IsListening, "the game itself is untouched");
        }

        [UnityTest]
        public IEnumerator OutputVolume_FollowsVoiceSetting()
        {
            var voice = new FakeVoiceService();
            yield return OnlineIntoLobby(voice);
            yield return Until(() => Director.State == VoiceState.On, "voice on");
            GameData.Settings.SetVolume(Core.Save.AudioGroup.Voice, 0.5f);
            yield return null;
            yield return null;
            Assert.AreEqual(0.5f, voice.OutputVolume, 0.001f);
        }

        [UnityTest]
        public IEnumerator Devices_SavedChoiceApplied()
        {
            var voice = new FakeVoiceService();
            voice.FakeInputs.AddRange(new[] { "Mic A", "Mic B" });
            GameData.Settings.micDevice = "Mic B";
            GameData.Settings.speakerDevice = "Speaker gone";
            yield return OnlineIntoLobby(voice);
            yield return Until(() => Director.State == VoiceState.On, "voice on");
            Assert.AreEqual("Mic B", voice.InputDevice);
            Assert.AreEqual("", voice.OutputDevice, "an unplugged device falls back to the system default");
        }
    }
}
