using System.Collections;
using System.Linq;
using NUnit.Framework;
using SpiritMovers.Core.Ghost;
using SpiritMovers.Core.Job;
using SpiritMovers.Ghost;
using SpiritMovers.Job;
using SpiritMovers.Player;
using SpiritMovers.Session;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class JobCycleTests : GameFlowFixture
    {
        private static JobDirector Director => Object.FindAnyObjectByType<JobDirector>();

        [UnityTest]
        public IEnumerator JobScene_StartsJobForTheTeam()
        {
            yield return SoloIntoJob();
            Assert.AreEqual(JobPhase.Exploring, Director.Phase);
            Assert.IsNotNull(Director.Plan);
        }

        [UnityTest]
        public IEnumerator SceneChange_TeleportsPlayersToSpawns()
        {
            yield return SoloIntoJob();
            var spawn = GameObject.Find("PlayerSpawn0").transform.position;
            Assert.Less(Vector3.Distance(spawn, PlayerMotor.Local.Feet), 0.5f);
        }

        [UnityTest]
        public IEnumerator Results_ReturnToLobbyAfterCountdown()
        {
            yield return SoloIntoJob();
            Director.LeaveEarly();
            yield return WaitFor(() => ActiveScene == GameSession.LobbyScene, 12f, "back in the van");
            yield return WaitFor(() => Object.FindAnyObjectByType<LobbyState>()?.Members.Count == 1, 3f, "lobby members");
            Assert.IsFalse(Object.FindAnyObjectByType<LobbyState>().Members[0].Ready, "everyone starts not ready again");
        }

        [UnityTest]
        public IEnumerator Results_HostConfirmGoesNow()
        {
            yield return SoloIntoJob();
            Director.LeaveEarly();
            yield return null;
            yield return Press(i => i.Next.ConfirmPressed = true);
            yield return WaitFor(() => ActiveScene == GameSession.LobbyScene, 2f, "back in the van at once");
        }

        [UnityTest]
        public IEnumerator SecondJob_StartsFresh()
        {
            yield return SoloIntoJob();
            Director.ApplyAngerForTest(85f);
            var cabinet = Object.FindObjectsByType<MemoryCabinet>(FindObjectsSortMode.None).First();
            Assert.IsTrue(cabinet.Capture(PlayerMotor.Local.GetComponent<PlayerCapture>()));
            Object.FindAnyObjectByType<SpiritCamera>().SetFilmForTest(2);
            Director.LeaveEarly();
            yield return Press(i => i.Next.ConfirmPressed = true);
            yield return WaitFor(() => ActiveScene == GameSession.LobbyScene, 3f, "van");

            yield return WaitFor(() => Object.FindAnyObjectByType<LobbyState>()?.Members.Count == 1, 3f, "lobby members");
            Assert.IsNull(Object.FindAnyObjectByType<SpiritCamera>(), "job tools stay in the job scene");
            yield return Press(i => i.Next.ReadyPressed = true);
            yield return Press(i => i.Next.ConfirmPressed = true);
            yield return WaitFor(() => ActiveScene == GameSession.JobScene && Director?.Phase == JobPhase.Exploring, 10f, "second job");
            yield return new WaitForFixedUpdate();

            Assert.Less(Director.AngerValue, 1f, "a calm ghost");
            Assert.IsFalse(PlayerMotor.Local.GetComponent<PlayerCapture>().IsCaptured, "nobody in a cabinet");
            var cameras = Object.FindObjectsByType<SpiritCamera>(FindObjectsSortMode.None);
            Assert.AreEqual(ToolConfig.SpiritCameras(1), cameras.Length, "only this job's cameras");
            Assert.AreEqual(6, cameras[0].Film, "a fresh camera");
            var task = Director.Plan.Rooms[0].Slots[0];
            var item = Director.House.Slot(task.Slot.Id).Item.transform.position;
            Assert.Less(Vector3.Distance(item, task.StartPlacement.Position.ToUnity()), 0.1f, "the house was unpacked again");
        }

        [UnityTest]
        public IEnumerator Loading_OverlayHiddenOnceJobStarts()
        {
            yield return SoloIntoLobby();
            var overlay = Object.FindAnyObjectByType<LoadingOverlay>();
            yield return Press(i => i.Next.ReadyPressed = true);
            yield return Press(i => i.Next.ConfirmPressed = true);
            var sawIt = false;
            var deadline = Time.realtimeSinceStartup + 10f;
            while (Director?.Phase != JobPhase.Exploring && Time.realtimeSinceStartup < deadline)
            {
                sawIt |= overlay.Visible;
                yield return null;
            }
            yield return null;
            Assert.IsTrue(sawIt, "shown while travelling");
            Assert.IsFalse(overlay.Visible, "gone once the job runs");
        }
    }
}
