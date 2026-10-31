using System.Collections;
using System.Linq;
using NUnit.Framework;
using SpiritMovers.Core.Job;
using SpiritMovers.Ghost;
using SpiritMovers.Interaction;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class CabinetTests : TestHouseFixture
    {
        private const float StageThree = 85f;

        private PlayerCapture Capture => Motor.GetComponent<PlayerCapture>();
        private ShyGhost Ghost => Object.FindAnyObjectByType<ShyGhost>();

        private static MemoryCabinet Cabinet(string roomId) =>
            Object.FindObjectsByType<MemoryCabinet>(FindObjectsSortMode.None).First(c => c.RoomId == roomId);

        private static IEnumerator Until(System.Func<bool> condition, float timeout)
        {
            var end = Time.time + timeout;
            while (!condition() && Time.time < end)
                yield return null;
        }

        private IEnumerator StartJob(float anger = 0f)
        {
            Director.BeginJob(3, JobDifficulty.Normal);
            if (anger > 0f)
                Director.ApplyAngerForTest(anger);
            yield return WaitForGrounded();
        }

        private IEnumerator CaptureIn(string roomId)
        {
            Assert.IsTrue(Cabinet(roomId).Capture(Capture));
            yield return null;
            yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator Caught_PlayerIsInCabinetAndCannotMove()
        {
            yield return StartJob(StageThree);
            Motor.Teleport(new Vector3(-5f, 0f, -4f), 180f);
            Ghost.SetPositionForTest(new Vector3(-5f, 0f, -2f));
            yield return Until(() => Capture.IsCaptured, 3f);
            Assert.IsTrue(Capture.IsCaptured, "the ghost caught the player from behind");
            yield return null;
            var inside = Motor.Feet;
            Input.Next.Move = Vector2.up;
            yield return new WaitForSeconds(1f);
            Input.Next = default;
            Assert.Less(Planar(inside, Motor.Feet), 0.05f);
        }

        [UnityTest]
        public IEnumerator Caught_DropsHeldItem()
        {
            yield return StartJob();
            var item = House.Slot(Director.Plan.Rooms[0].Slots[0].Slot.Id).Item;
            item.SetSingleCarrierForTest(NetworkManager.ServerClientId);
            yield return null;
            Assume.That(item.IsCarried);
            yield return CaptureIn("west");
            Assert.IsFalse(item.IsCarried);
        }

        [UnityTest]
        public IEnumerator Cabinet_LockedUntilOneMoreItemRight()
        {
            yield return StartJob();
            var west = Director.Plan.Rooms.First(r => r.RoomId == "west");
            yield return CaptureIn("west");
            Assert.IsFalse(Cabinet("west").IsUnlocked);
            yield return PutBack(west.Slots[0].Slot.Id);
            yield return Until(() => Cabinet("west").IsUnlocked, 1f);
            Assert.IsTrue(Cabinet("west").IsUnlocked);
        }

        [UnityTest]
        public IEnumerator Cabinet_FriendOpens_ReleasesInFront()
        {
            yield return StartJob();
            yield return CaptureIn("west");
            Cabinet("west").UnlockByCamera();
            Assert.IsTrue(Cabinet("west").Open(opener: 99));
            yield return Until(() => !Capture.IsCaptured, 1f);
            yield return null;
            Assert.IsFalse(Capture.IsCaptured);
            Assert.Less(Planar(Motor.Feet, Cabinet("west").FrontPoint), 0.3f);
            Assert.AreEqual(0, Director.SelfReleases);
        }

        [UnityTest]
        public IEnumerator Cabinet_SelfRelease_CountsAgainstPay()
        {
            yield return StartJob();
            var west = Director.Plan.Rooms.First(r => r.RoomId == "west");
            foreach (var task in west.Slots)
                yield return PutBack(task.Slot.Id);
            yield return Until(() => Director.AcceptedRooms.Contains("west"), 2f);
            Assume.That(Director.AcceptedRooms, Does.Contain("west"));
            yield return CaptureIn("west");
            Cabinet("west").ExpireNow();
            yield return Until(() => !Capture.IsCaptured, 1f);
            Assert.IsFalse(Capture.IsCaptured);
            Assert.AreEqual(1, Director.SelfReleases);
            Director.LeaveEarly();
            yield return null;
            Assert.AreEqual(100 - 15, Director.Result.TeamTotal, "1/3 of 300, less 5% of 300 for getting out alone");
        }

        [UnityTest]
        public IEnumerator Solo_Capture_CalmsGhostAndShortensTimer()
        {
            yield return StartJob(StageThree);
            Assume.That(Director.AngerStage, Is.EqualTo(3));
            yield return CaptureIn("west");
            Assert.AreEqual(2, Director.AngerStage, "solo: the ghost calms down one stage");
            Assert.AreEqual(20f, Capture.ReleaseAt - Time.time, 0.5f);
        }

        [UnityTest]
        public IEnumerator Released_IsImmuneBriefly()
        {
            yield return StartJob(StageThree);
            yield return CaptureIn("west");
            Director.ApplyAngerForTest(StageThree); // solo capture calmed it a stage
            Cabinet("west").ExpireNow();
            yield return Until(() => !Capture.IsCaptured, 1f);
            yield return null;
            Motor.Teleport(Motor.Feet, Motor.transform.eulerAngles.y + 180f); // back to the ghost
            Ghost.SetPositionForTest(Motor.Feet + Motor.Forward * -0.5f);
            Ghost.ThinkNow();
            yield return new WaitForSeconds(3f);
            Assert.IsFalse(Capture.IsCaptured);
        }

        [UnityTest]
        public IEnumerator JobEnd_ReleasesCaptives()
        {
            yield return StartJob();
            yield return CaptureIn("west");
            Director.LeaveEarly();
            yield return Until(() => !Capture.IsCaptured, 1f);
            Assert.IsFalse(Capture.IsCaptured);
            Assert.AreEqual(0, Director.SelfReleases);
        }

        [UnityTest]
        public IEnumerator CaptiveDisconnects_CabinetEmpties()
        {
            yield return StartJob();
            yield return CaptureIn("west");
            Motor.NetworkObject.Despawn(false);
            yield return null;
            yield return null;
            Assert.IsFalse(Cabinet("west").IsOccupied);
        }
    }
}
