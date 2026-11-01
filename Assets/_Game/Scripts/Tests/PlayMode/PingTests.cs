using System.Collections;
using System.Linq;
using NUnit.Framework;
using SpiritMovers.Core.Text;
using SpiritMovers.Core.Voice;
using SpiritMovers.Dev;
using SpiritMovers.Player;
using SpiritMovers.Save;
using SpiritMovers.Session;
using SpiritMovers.Settings;
using SpiritMovers.Voice;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class PingTests : GameFlowFixture
    {
        private static CommsHub Hub => CommsHub.Current;
        private static string Chat => ThaiShaper.Unshape(Object.FindAnyObjectByType<ChatPanel>().LastText);

        [UnityTest]
        public IEnumerator Ping_ShownToEveryoneAndFades()
        {
            yield return SoloIntoJob();
            PlayerMotor.Local.SetPitchForTest(60f); // look at the floor
            yield return Press(i => i.Next.PingPressed = true);
            yield return null;
            Assert.AreEqual(1, Hub.Markers.Count);
            var marker = Hub.Markers[0];
            Assert.Less(marker.transform.position.y, 0.5f, "on the floor in front of us");
            StringAssert.Contains(GameData.PlayerName, ThaiShaper.Unshape(marker.Label));
            yield return new WaitForSeconds(6.5f);
            Assert.AreEqual(0, Hub.Markers.Count, "faded after 6 seconds");
        }

        [UnityTest]
        public IEnumerator Ping_RateLimitedOnHost()
        {
            yield return SoloIntoJob();
            for (var i = 0; i < 10; i++)
                Hub.PingRpc(Vector3.zero);
            yield return null;
            yield return null;
            Assert.AreEqual(1, Hub.Markers.Count, "ten in one frame make one");
            for (var i = 0; i < 4; i++)
            {
                yield return new WaitForSeconds(0.6f);
                Hub.PingRpc(new Vector3(i, 0f, 0f));
            }
            yield return null;
            yield return null;
            Assert.AreEqual(3, Hub.Markers.Count, "at most three up at once");
        }

        [UnityTest]
        public IEnumerator Quick_NumberKeySendsMessage()
        {
            yield return SoloIntoLobby();
            yield return Press(i => i.Next.QuickPressed = 2);
            yield return null;
            StringAssert.Contains($"{GameData.PlayerName}: ผีอยู่ตรงนี้!", Chat);
            GameData.Settings.language = "en";
            SettingsApplier.ApplyLanguage();
            yield return new WaitForSeconds(0.6f);
            yield return Press(i => i.Next.QuickPressed = 2);
            yield return null;
            StringAssert.Contains("Ghost here!", Chat);
            GameData.Settings.language = "th";
            SettingsApplier.ApplyLanguage();
        }

        [UnityTest]
        public IEnumerator Quick_WheelPicksByDirection()
        {
            Assert.AreEqual(QuickMessage.NeedHands, QuickWheel.PickAt(new Vector2(1f, 0f)), "right");
            Assert.AreEqual(QuickMessage.Watch, QuickWheel.PickAt(new Vector2(-0.5f, 0.86f)), "upper left");
            Assert.AreEqual(QuickMessage.ToVan, QuickWheel.PickAt(new Vector2(-1f, 0f)), "left");
            Assert.IsNull(QuickWheel.PickAt(Vector2.zero), "the middle sends nothing");
            Assert.IsNull(QuickWheel.PickAt(new Vector2(0.05f, 0.05f)));

            yield return SoloIntoLobby();
            LocalInput.Next.QuickWheelHeld = true;
            yield return null;
            yield return null;
            var wheel = Object.FindAnyObjectByType<QuickWheel>();
            Assert.IsTrue(wheel.IsOpen);
            wheel.PointForTest(new Vector2(1f, 0f));
            LocalInput.Next.QuickWheelHeld = false;
            yield return null;
            yield return null;
            Assert.IsFalse(wheel.IsOpen);
            StringAssert.Contains("มาช่วยยกหน่อย", Chat);
        }

        [UnityTest]
        public IEnumerator Ping_WorksWithoutVoice()
        {
            var voice = new FakeVoiceService { FailLogin = true };
            yield return OnlineIntoJob(voice);
            yield return WaitFor(() => VoiceDirector.Instance.State == VoiceState.Lost, 5f, "voice down");
            yield return Press(i => i.Next.PingPressed = true);
            yield return null;
            Assert.AreEqual(1, Hub.Markers.Count);
            yield return Press(i => i.Next.QuickPressed = 5);
            yield return null;
            StringAssert.Contains("โอเค", Chat);
        }

        [UnityTest]
        public IEnumerator Comms_SecondJobFresh()
        {
            yield return SoloIntoJob();
            yield return Press(i => i.Next.PingPressed = true);
            yield return null;
            Assert.AreEqual(1, Hub.Markers.Count);
            Object.FindAnyObjectByType<Job.JobDirector>().LeaveEarly();
            yield return Press(i => i.Next.ConfirmPressed = true);
            yield return WaitFor(() => ActiveScene == GameSession.LobbyScene, 5f, "van");
            yield return null;
            Assert.AreEqual(0, Object.FindObjectsByType<PingMarker>(FindObjectsSortMode.None).Length, "markers stay in the house");
            Assert.IsNotNull(Hub, "the van has its own hub");
        }
    }
}
