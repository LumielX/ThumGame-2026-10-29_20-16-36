using System.Collections;
using System.Linq;
using NUnit.Framework;
using SpiritMovers.Core.Text;
using SpiritMovers.Core.Voice;
using SpiritMovers.Dev;
using SpiritMovers.Player;
using SpiritMovers.Session;
using SpiritMovers.Settings;
using SpiritMovers.Voice;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class ChatTests : GameFlowFixture
    {
        private static ChatPanel Chat => Object.FindAnyObjectByType<ChatPanel>();
        private static string Shown => ThaiShaper.Unshape(Chat.LastText);

        private IEnumerator VanWithFriend(FakeVoiceService voice)
        {
            yield return OnlineIntoLobby(voice);
            yield return WaitFor(() => VoiceDirector.Instance.State == VoiceState.On, 5f, "voice on");
            LobbyState.AddToRosterForTest(new LobbyMember(42, true, 2, "เพื่อน", "p2"));
        }

        [UnityTest]
        public IEnumerator Chat_ThaiMessageSentOnRightChannel()
        {
            var voice = new FakeVoiceService();
            yield return VanWithFriend(voice);
            Chat.Type("ไปกันเลย ผู้ใหญ่บ้าน");
            Assert.IsTrue(voice.Calls.Contains("Text van-FAKE01 ไปกันเลย ผู้ใหญ่บ้าน"), "plain text, not shaped glyphs");
            StringAssert.Contains("ไปกันเลย ผู้ใหญ่บ้าน", Shown, "our own message comes back into the box");

            yield return SetOffFromVan();
            yield return WaitFor(() => voice.Joined.Contains("radio-FAKE01"), 5f, "house voice");
            yield return new WaitForSeconds(0.6f);
            Chat.Type("อยู่ห้องครัว");
            Assert.IsTrue(voice.Calls.Contains("Text radio-FAKE01 อยู่ห้องครัว"), "in the house, chat rides the radio channel");
        }

        [UnityTest]
        public IEnumerator Chat_IncomingShownWithName()
        {
            var voice = new FakeVoiceService();
            yield return VanWithFriend(voice);
            voice.DeliverText("p2", "van-FAKE01", "ช่วยด้วย");
            yield return null;
            StringAssert.Contains("เพื่อน: ช่วยด้วย", Shown);
        }

        [UnityTest]
        public IEnumerator Chat_TypingStopsWalkingAndEscCancels()
        {
            var voice = new FakeVoiceService();
            yield return VanWithFriend(voice);
            yield return Press(i => i.Next.ChatPressed = true);
            yield return null;
            Assert.IsTrue(Chat.IsTyping);
            Assert.IsFalse(PlayerMotor.Local.InputEnabled, "no walking while typing");
            var sent = voice.Calls.Count(c => c.StartsWith("Text "));
            yield return Press(i => i.Next.PausePressed = true);
            Assert.IsFalse(Chat.IsTyping, "Esc closes the chat field");
            Assert.IsFalse(Object.FindAnyObjectByType<PauseMenu>().IsOpen, "and does not open the pause menu");
            Assert.IsTrue(PlayerMotor.Local.InputEnabled);
            Assert.AreEqual(sent, voice.Calls.Count(c => c.StartsWith("Text ")), "nothing sent");
        }

        [UnityTest]
        public IEnumerator Chat_RateLimitedAndCleaned()
        {
            var voice = new FakeVoiceService();
            yield return VanWithFriend(voice);
            for (var i = 0; i < 5; i++)
                Chat.Type("เร็ว");
            Assert.AreEqual(1, voice.Calls.Count(c => c.StartsWith("Text ")), "one message per half second");
            yield return new WaitForSeconds(0.6f);
            Chat.Type("    ");
            Assert.AreEqual(1, voice.Calls.Count(c => c.StartsWith("Text ")), "blank is not sent");
            voice.DeliverText("p2", "van-FAKE01", new string('ก', 300));
            yield return null;
            var line = Shown.Split('\n').First(l => l.StartsWith("เพื่อน: "));
            Assert.LessOrEqual(line.Length - "เพื่อน: ".Length, ChatText.MaxLength, "an incoming message is cleaned too");
        }

        [UnityTest]
        public IEnumerator Chat_UnavailableWhenVoiceDown()
        {
            var voice = new FakeVoiceService();
            yield return VanWithFriend(voice);
            voice.FailJoin = true;
            voice.SimulateDrop();
            yield return null;
            Chat.Type("ได้ยินไหม");
            Assert.IsFalse(voice.Calls.Any(c => c.StartsWith("Text ")), "nothing sent");
            StringAssert.Contains(ThaiShaper.Unshape(SessionUi.Text("chat.unavailable")), Shown);
        }

        [UnityTest]
        public IEnumerator Chat_RebindableKey()
        {
            var voice = new FakeVoiceService();
            yield return VanWithFriend(voice);
            SettingsApplier.Actions.FindAction("Gameplay/Chat", true).ApplyBindingOverride(0, "<Keyboard>/y");
            yield return WaitFor(() => Shown.Contains("กด Y เพื่อพิมพ์"), 2f, "the hint shows the new key");
        }
    }
}
