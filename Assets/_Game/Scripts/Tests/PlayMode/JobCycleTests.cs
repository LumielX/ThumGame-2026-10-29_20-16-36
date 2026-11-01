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
            // Move everything the team had to tidy 2 m away, so a house that is not unpacked again shows.
            foreach (var moved in Director.Plan.Rooms.SelectMany(r => r.Slots))
            {
                var body = Director.House.Slot(moved.Slot.Id).Item.GetComponent<Rigidbody>();
                body.position += new Vector3(2f, 0f, 0f);
                body.transform.position = body.position;
            }
            Director.LeaveEarly();
            yield return Press(i => i.Next.ConfirmPressed = true);
            yield return WaitFor(() => ActiveScene == GameSession.LobbyScene, 3f, "van");

            yield return WaitFor(() => Object.FindAnyObjectByType<LobbyState>()?.Members.Count == 1, 3f, "lobby members");
            Assert.IsNull(Object.FindAnyObjectByType<SpiritCamera>(), "job tools stay in the job scene");
            // Final review: caught in a cabinet as the job closed, and the mouse freed by the results screen.
            Assert.IsFalse(PlayerMotor.Local.MovementLocked, "free to walk in the van");
            Assert.IsTrue(GameCursor.Captured, "the mouse looks around again in the van");
            yield return Press(i => i.Next.ReadyPressed = true);
            yield return Press(i => i.Next.ConfirmPressed = true);
            yield return WaitFor(() => ActiveScene == GameSession.JobScene && Director?.Phase == JobPhase.Exploring, 10f, "second job");
            yield return new WaitForFixedUpdate();

            Assert.Less(Director.AngerValue, 1f, "a calm ghost");
            Assert.IsFalse(PlayerMotor.Local.GetComponent<PlayerCapture>().IsCaptured, "nobody in a cabinet");
            var cameras = Object.FindObjectsByType<SpiritCamera>(FindObjectsSortMode.None);
            Assert.AreEqual(ToolConfig.SpiritCameras(1), cameras.Length, "only this job's cameras");
            Assert.AreEqual(6, cameras[0].Film, "a fresh camera");
            // Items start exactly on their spots; physics may then nudge two neighbours apart by a few centimetres
            // (a random seed decides who stands where), far less than the 2 m the first job moved them.
            foreach (var task in Director.Plan.Rooms.SelectMany(r => r.Slots))
            {
                var offset = Director.House.Slot(task.Slot.Id).Item.transform.position - task.StartPlacement.Position.ToUnity();
                Assert.Less(offset.magnitude, 0.3f, $"slot {task.Slot.Id} unpacked again (off by {offset})");
            }
        }

        [UnityTest]
        public IEnumerator CabinetUnloaded_FreesItsCaptive()
        {
            // Final review: the job scene can unload before the cabinet's next check lets its captive out, which left
            // that player unable to walk in the van and every later job.
            yield return SoloIntoJob();
            var cabinet = Object.FindObjectsByType<MemoryCabinet>(FindObjectsSortMode.None).First();
            Assert.IsTrue(cabinet.Capture(PlayerMotor.Local.GetComponent<PlayerCapture>()));
            yield return null;
            Assert.IsTrue(PlayerMotor.Local.MovementLocked);
            Session.Network.SceneManager.LoadScene(GameSession.LobbyScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
            yield return WaitFor(() => ActiveScene == GameSession.LobbyScene, 5f, "van");
            yield return null;
            Assert.IsFalse(PlayerMotor.Local.GetComponent<PlayerCapture>().IsCaptured);
            Assert.IsFalse(PlayerMotor.Local.MovementLocked, "free to walk in the van");
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
