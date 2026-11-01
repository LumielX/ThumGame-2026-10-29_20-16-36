using System.Collections;
using System.Linq;
using NUnit.Framework;
using SpiritMovers.Dev;
using SpiritMovers.Interaction;
using SpiritMovers.Job;
using SpiritMovers.Net;
using SpiritMovers.Player;
using SpiritMovers.Session;
using SpiritMovers.Voice;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class WalkieTests : GameFlowFixture
    {
        private const string Radio = "radio-FAKE01";
        private static JobDirector Director => Object.FindAnyObjectByType<JobDirector>();
        private static Walkie[] Walkies => Object.FindObjectsByType<Walkie>(FindObjectsSortMode.InstanceID);
        private static VoiceDirector Voice => VoiceDirector.Instance;

        /// <summary>An online job (fake services) with two walkie-talkies, as a team of two would get.</summary>
        private IEnumerator JobWithRadios(FakeVoiceService voice)
        {
            yield return OnlineIntoJob(voice);
            yield return WaitFor(() => Voice.State == VoiceState.On && voice.Joined.Count == 2, 5f, "voice in the house");
            Director.SpawnToolsForTest(2);
            yield return null;
            yield return new WaitForFixedUpdate();
        }

        private static IEnumerator Hold(Walkie walkie)
        {
            walkie.GetComponent<Carryable>().SetSingleCarrierForTest(NetworkManager.ServerClientId);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Radio_CountByCrew()
        {
            var voice = new FakeVoiceService();
            yield return OnlineIntoJob(voice);
            Assert.AreEqual(0, Walkies.Length, "nobody to call when alone");
            Director.SpawnToolsForTest(2);
            yield return null;
            Assert.AreEqual(2, Walkies.Length);
        }

        [UnityTest]
        public IEnumerator Radio_TransmitsOnlyWhileHeldAndPressed()
        {
            var voice = new FakeVoiceService();
            yield return JobWithRadios(voice);
            yield return Hold(Walkies[0]);
            LocalInput.Next.UseHeld = true;
            yield return null;
            yield return null;
            Assert.IsTrue(Voice.RadioHeld);
            CollectionAssert.AreEqual(new[] { "house-FAKE01", Radio }, voice.Transmitting.ToArray());
            LocalInput.Next.UseHeld = false;
            yield return null;
            yield return null;
            Assert.IsFalse(Voice.RadioHeld);
            CollectionAssert.AreEqual(new[] { "house-FAKE01" }, voice.Transmitting.ToArray());
        }

        [UnityTest]
        public IEnumerator Radio_DropWhileTalkingStops()
        {
            var voice = new FakeVoiceService();
            yield return JobWithRadios(voice);
            var walkie = Walkies[0];
            yield return Hold(walkie);
            LocalInput.Next.UseHeld = true;
            yield return null;
            yield return null;
            Assert.IsTrue(Voice.RadioHeld);
            walkie.GetComponent<Carryable>().ReleaseCarrier(NetworkManager.ServerClientId);
            yield return null;
            Assert.IsFalse(Voice.RadioHeld, "dropped with the button still down");
            yield return null;
            CollectionAssert.AreEqual(new[] { "house-FAKE01" }, voice.Transmitting.ToArray());
            LocalInput.Next.UseHeld = false;
        }

        [UnityTest]
        public IEnumerator Radio_OnlyHoldersHear()
        {
            var voice = new FakeVoiceService();
            yield return JobWithRadios(voice);
            voice.AddParticipant(new VoiceParticipant("p2", Radio, true, 0.7, false));
            voice.AddParticipant(new VoiceParticipant(voice.PlayerId, Radio, false, 0, true));
            yield return null;
            yield return null;
            Assert.AreEqual(1, voice.Taps.Count, "one tap for the teammate, none for ourselves");
            var source = voice.TapParticipant("p2", Radio);
            Assert.IsTrue(source.mute, "no walkie in hand, no radio voice");
            Assert.IsNotNull(source.GetComponent<RadioFilter>());
            yield return Hold(Walkies[0]);
            yield return null;
            Assert.IsFalse(source.mute);
        }

        [UnityTest]
        public IEnumerator Radio_TapsCleanedUpOnLeave()
        {
            var voice = new FakeVoiceService();
            yield return JobWithRadios(voice);
            voice.AddParticipant(new VoiceParticipant("p2", Radio, true, 0.7, false));
            yield return null;
            yield return null;
            Assert.AreEqual(1, voice.Taps.Count);
            voice.RemoveParticipant("p2", Radio);
            yield return null;
            yield return null;
            Assert.AreEqual(0, voice.Taps.Count, "left the radio");

            voice.AddParticipant(new VoiceParticipant("p2", Radio, true, 0.7, false));
            yield return null;
            Director.LeaveEarly();
            yield return Press(i => i.Next.ConfirmPressed = true);
            yield return WaitFor(() => ActiveScene == GameSession.LobbyScene, 5f, "van");
            yield return null;
            Assert.AreEqual(0, voice.Taps.Count, "no radio taps in the van");

            yield return SetOffFromVan();
            yield return WaitFor(() => voice.Taps.Count > 0, 5f, "tap again on the next job");
            yield return null;
            Assert.AreEqual(1, voice.Taps.Count, "not doubled");
        }

        [UnityTest]
        public IEnumerator Radio_GhostInterferenceFollowsAnger()
        {
            var voice = new FakeVoiceService();
            yield return JobWithRadios(voice);
            voice.AddParticipant(new VoiceParticipant("p2", Radio, true, 0.7, false));
            yield return null;
            yield return null;
            var filter = voice.TapParticipant("p2", Radio).GetComponent<RadioFilter>();
            Assert.AreEqual(0f, filter.Crackle);
            Director.ApplyAngerForTest(50f);
            yield return WaitFor(() => Director.AngerStage == 2, 2f, "stage 2");
            yield return null;
            Assert.AreEqual(0.3f, filter.Crackle);
            Director.ApplyAngerForTest(75f);
            yield return WaitFor(() => Director.AngerStage == 3, 2f, "stage 3");
            yield return null;
            Assert.AreEqual(0.5f, filter.Crackle);
        }

        [UnityTest]
        public IEnumerator Radio_SecondJobFresh()
        {
            var voice = new FakeVoiceService();
            yield return JobWithRadios(voice);
            Assert.AreEqual(2, Walkies.Length);
            Director.LeaveEarly();
            yield return Press(i => i.Next.ConfirmPressed = true);
            yield return WaitFor(() => ActiveScene == GameSession.LobbyScene, 5f, "van");
            yield return null;
            Assert.AreEqual(0, Walkies.Length, "walkies stay in the house");
            yield return SetOffFromVan();
            Assert.AreEqual(0, Walkies.Length, "a one-player job gets none, nothing left over");
        }
    }
}
