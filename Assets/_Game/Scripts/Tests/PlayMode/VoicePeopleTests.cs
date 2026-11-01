using System.Collections;
using System.Linq;
using NUnit.Framework;
using SpiritMovers.Core.Text;
using SpiritMovers.Dev;
using SpiritMovers.Net;
using SpiritMovers.Session;
using SpiritMovers.Voice;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class VoicePeopleTests : GameFlowFixture
    {
        private static VoiceHud Hud => Object.FindAnyObjectByType<VoiceHud>();

        private IEnumerator VanWithFriend(FakeVoiceService voice)
        {
            yield return OnlineIntoLobby(voice);
            yield return WaitFor(() => VoiceDirector.Instance.State == VoiceState.On, 5f, "voice on");
            yield return WaitFor(() => LobbyState.Roster.Count == 1, 3f, "roster");
            Assert.AreEqual(voice.PlayerId, LobbyState.Roster[0].VoiceId.ToString(), "the host's own voice id is on the roster");
            LobbyState.AddToRosterForTest(new LobbyMember(42, true, 2, "เพื่อน", "p2"));
        }

        [UnityTest]
        public IEnumerator Hud_ShowsWhoIsSpeaking()
        {
            var voice = new FakeVoiceService();
            yield return VanWithFriend(voice);
            voice.AddParticipant(new VoiceParticipant("p2", "van-FAKE01", true, 0.6, false));
            yield return WaitFor(() => ThaiShaper.Unshape(Hud.LastText).Contains("เพื่อน"), 1f, "the speaker on screen");
            voice.AddParticipant(new VoiceParticipant("p2", "van-FAKE01", false, 0, false));
            yield return WaitFor(() => !ThaiShaper.Unshape(Hud.LastText).Contains("เพื่อน"), 0.5f, "gone when quiet");
        }

        [UnityTest]
        public IEnumerator Hud_UnknownSpeakerShownAsPlayerN()
        {
            var voice = new FakeVoiceService();
            yield return VanWithFriend(voice);
            voice.AddParticipant(new VoiceParticipant("stranger", "van-FAKE01", true, 0.6, false));
            yield return WaitFor(() => ThaiShaper.Unshape(Hud.LastText).Contains("ผู้เล่น ?"), 1f, "an unknown speaker");
        }

        [UnityTest]
        public IEnumerator Mute_PerPersonAppliedAndKept()
        {
            var voice = new FakeVoiceService();
            yield return VanWithFriend(voice);
            var panel = Object.FindAnyObjectByType<PlayerVoicePanel>();
            panel.SetMuted("p2", true);
            Assert.IsTrue(voice.ParticipantMuted["p2"]);
            var before = voice.Calls.Count(c => c == "Mute p2 True");
            yield return SetOffFromVan();
            yield return WaitFor(() => voice.Joined.Contains("radio-FAKE01"), 5f, "house voice");
            yield return null;
            Assert.Greater(voice.Calls.Count(c => c == "Mute p2 True"), before, "applied again in the new channels");
            Assert.IsTrue(VoiceDirector.Instance.IsPlayerMuted("p2"));
        }

        [UnityTest]
        public IEnumerator Vivox_LogoutForgetsMutes()
        {
            // Final review: the Vivox service kept its own list of muted players after leaving a room, so a friend
            // muted once stayed silent in the next room while the Players page showed them unmuted.
            var vivox = new VivoxVoiceService();
            vivox.SetParticipantMuted("p2", true);
            vivox.SetParticipantVolume("p2", 0.3f);
            Assert.IsTrue(vivox.RemembersForTest("p2"));
            var logout = vivox.LogoutAsync();
            yield return WaitFor(() => logout.IsCompleted, 6f, "logout");
            Assert.IsFalse(vivox.RemembersForTest("p2"));
        }

        [UnityTest]
        public IEnumerator Volume_PerPersonApplied()
        {
            var voice = new FakeVoiceService();
            yield return VanWithFriend(voice);
            Object.FindAnyObjectByType<PlayerVoicePanel>().SetVolume("p2", 0.3f);
            Assert.AreEqual(0.3f, voice.ParticipantVolume["p2"], 0.001f);
        }

        [UnityTest]
        public IEnumerator Panel_ListsEveryoneButMe()
        {
            var voice = new FakeVoiceService();
            yield return VanWithFriend(voice);
            yield return Press(i => i.Next.PausePressed = true);
            Object.FindAnyObjectByType<PauseMenu>().ClickPlayers();
            yield return null;
            var panel = Object.FindAnyObjectByType<PlayerVoicePanel>();
            Assert.IsTrue(panel.IsOpen);
            var text = ThaiShaper.Unshape(panel.LastText);
            StringAssert.Contains("เพื่อน", text);
            Assert.IsFalse(text.Contains(SpiritMovers.Save.GameData.PlayerName), "no mute button for myself");
        }
    }
}
