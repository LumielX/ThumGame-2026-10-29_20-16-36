using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpiritMovers.Core.Ghost;
using SpiritMovers.Core.Job;
using SpiritMovers.Ghost;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class GhostTests : TestHouseFixture
    {
        private const float StageOne = 30f, StageTwo = 55f, StageThree = 85f;

        private ShyGhost Ghost => Object.FindAnyObjectByType<ShyGhost>();

        private IEnumerator StartJob(float anger)
        {
            Director.BeginJob(3, JobDifficulty.Normal);
            Director.ApplyAngerForTest(anger);
            yield return WaitForGrounded();
        }

        /// <summary>Stand at <paramref name="feet"/> facing <paramref name="yaw"/> and let the host see it.</summary>
        private IEnumerator Stand(Vector3 feet, float yaw)
        {
            Motor.Teleport(feet, yaw);
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        private Dictionary<string, Vector3> PlannedItemPositions() =>
            Director.Plan.Rooms.SelectMany(r => r.Slots).ToDictionary(t => t.Slot.Id, t => House.Slot(t.Slot.Id).Item.transform.position);

        [UnityTest]
        public IEnumerator Ghost_OutsideExploring_DoesNotHunt()
        {
            var start = Ghost.transform.position;
            yield return new WaitForSeconds(1f);
            Assert.Less(Vector3.Distance(start, Ghost.transform.position), 0.01f);
            Assert.AreEqual(GhostMode.Idle, Ghost.Mode);
        }

        [UnityTest]
        public IEnumerator Ghost_Watched_StaysStill()
        {
            yield return StartJob(StageThree);
            Ghost.SetPositionForTest(new Vector3(-5f, 0f, 2f));
            yield return Stand(new Vector3(-5f, 0f, -4f), 0f);
            Motor.LookAt(Ghost.transform.position);
            Ghost.ThinkNow();
            var start = Ghost.transform.position;
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(Ghost.IsWatched);
            Assert.AreEqual(GhostMode.Hunt, Ghost.Mode);
            Assert.Less(Vector3.Distance(start, Ghost.transform.position), 0.05f);
        }

        [UnityTest]
        public IEnumerator Ghost_WatchedOverOwnCarriedItem_StaysStill()
        {
            // Final review: the box in your own hands must not hide the ghost you are staring at.
            yield return StartJob(StageThree);
            yield return Stand(new Vector3(-5f, 0f, -2f), 0f);
            var box = Director.Plan.Rooms.SelectMany(r => r.Slots).Select(t => House.Slot(t.Slot.Id).Item).First(i => i.Mass < 10f);
            var hands = Motor.GetComponent<SpiritMovers.Interaction.PlayerCarrier>();
            box.SetSingleCarrierForTest(Unity.Netcode.NetworkManager.ServerClientId);
            box.GetComponent<Rigidbody>().position = hands.HoldPoint;
            yield return new WaitForSeconds(0.5f);
            Assume.That(box.IsCarried, "still in hand");
            Assume.That(Vector3.Distance(box.transform.position, hands.HoldPoint), Is.LessThan(0.2f));
            Ghost.SetPositionForTest(new Vector3(-5f, 0f, 1f));
            Motor.LookAt(Ghost.transform.position);
            Ghost.ThinkNow();
            var start = Ghost.transform.position;
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(Ghost.IsWatched, "watched over the carried box");
            Assert.Less(Vector3.Distance(start, Ghost.transform.position), 0.05f);
        }

        [UnityTest]
        public IEnumerator Ghost_Unwatched_HuntsTowardPlayer()
        {
            yield return StartJob(StageThree);
            Ghost.SetPositionForTest(new Vector3(-5f, 0f, 6f));
            yield return Stand(new Vector3(-5f, 0f, -3f), 180f);
            Ghost.ThinkNow();
            var before = Planar(Ghost.transform.position, Motor.Feet);
            yield return new WaitForSeconds(1f);
            Assert.IsFalse(Ghost.IsWatched);
            Assert.LessOrEqual(Planar(Ghost.transform.position, Motor.Feet), before - 1.5f);
        }

        [UnityTest]
        public IEnumerator Ghost_LookingThroughWall_DoesNotFreeze()
        {
            yield return StartJob(StageThree);
            Ghost.SetPositionForTest(new Vector3(5f, 0f, 6f));
            yield return Stand(new Vector3(-3f, 0f, 6f), 90f); // facing the dividing wall, the ghost behind it
            Motor.LookAt(Ghost.transform.position);
            Ghost.ThinkNow();
            var start = Ghost.transform.position;
            yield return new WaitForSeconds(1f);
            Assert.IsFalse(Ghost.IsWatched, "a wall is in between");
            Assert.Greater(Vector3.Distance(start, Ghost.transform.position), 1f);
        }

        [UnityTest]
        public IEnumerator Ghost_ReachesLoftByStairs()
        {
            yield return StartJob(StageThree);
            Ghost.SetPositionForTest(new Vector3(-5f, 0f, 6f));
            yield return Stand(new Vector3(8.5f, 1.2f, -6f), 90f);
            var deadline = Time.time + 15f;
            while (Vector3.Distance(Ghost.Ground, Motor.Feet) > 1.5f && Time.time < deadline)
                yield return null;
            Assert.LessOrEqual(Vector3.Distance(Ghost.Ground, Motor.Feet), 1.5f, "through the doorway and up the stairs");
        }

        [UnityTest]
        public IEnumerator Prank_SwapsTwoItemsInGhostRoom()
        {
            yield return StartJob(StageOne);
            Ghost.SetPositionForTest(new Vector3(-5f, 0f, 0f));
            yield return Stand(new Vector3(-8f, 0f, -8f), 180f);
            var before = PlannedItemPositions();
            Assume.That(before.Values.Count(p => House.RoomAt(p) == "west"), Is.GreaterThanOrEqualTo(2));
            Ghost.NextPrankAt = 0f;
            Ghost.ThinkNow();
            yield return new WaitForFixedUpdate();
            var after = PlannedItemPositions();
            var moved = before.Keys.Where(id => Vector3.Distance(before[id], after[id]) > 0.1f).ToList();
            Assert.AreEqual(2, moved.Count);
            Assert.Less(Vector3.Distance(before[moved[0]], after[moved[1]]), 0.05f, "they swapped places");
            Assert.AreEqual("west", House.RoomAt(after[moved[0]]));
        }

        [UnityTest]
        public IEnumerator Prank_LeavesAcceptedRoomAlone()
        {
            yield return StartJob(0f);
            var west = Director.Plan.Rooms.First(r => r.RoomId == "west");
            foreach (var task in west.Slots)
                yield return PutBack(task.Slot.Id);
            var deadline = Time.time + 2f;
            while (!Director.AcceptedRooms.Contains("west") && Time.time < deadline)
                yield return null;
            Assume.That(Director.AcceptedRooms, Does.Contain("west"));
            Director.ApplyAngerForTest(StageOne);
            Ghost.SetPositionForTest(new Vector3(-5f, 0f, 0f));
            yield return Stand(new Vector3(-8f, 0f, -8f), 180f);
            var before = PlannedItemPositions();
            Ghost.NextPrankAt = 0f;
            Ghost.ThinkNow();
            yield return new WaitForFixedUpdate();
            var after = PlannedItemPositions();
            Assert.IsTrue(before.Keys.All(id => Vector3.Distance(before[id], after[id]) < 0.05f));
        }

        [UnityTest]
        public IEnumerator Disturb_PushesItemAndDimsLights()
        {
            yield return StartJob(StageTwo);
            Ghost.SetPositionForTest(new Vector3(-5f, 0f, 0f));
            yield return Stand(new Vector3(-8f, 0f, -8f), 180f);
            var lights = Object.FindAnyObjectByType<GhostLights>();
            var bodies = Director.Plan.Rooms.SelectMany(r => r.Slots).Select(t => House.Slot(t.Slot.Id).Item.GetComponent<Rigidbody>()).ToList();
            Ghost.NextDisturbAt = 0f;
            Ghost.ThinkNow();
            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.LessOrEqual(lights.Factor, 0.2f);
            Assert.IsTrue(bodies.Any(b => b.linearVelocity.magnitude > 0.5f), "one item was pushed");
            yield return new WaitForSeconds(3.5f);
            Assert.AreEqual(1f, lights.Factor, 0.01f);
        }

        [UnityTest]
        public IEnumerator Ghost_ColourFollowsStage()
        {
            yield return StartJob(0f);
            yield return null;
            var calm = Ghost.CurrentColour;
            Assert.Greater(calm.r, 0.9f);
            Assert.Greater(calm.g, 0.6f, "amber when calm");
            Director.ApplyAngerForTest(StageThree);
            yield return null;
            var angry = Ghost.CurrentColour;
            Assert.Greater(angry.r, 0.8f);
            Assert.Less(angry.g, 0.35f, "red when hunting");
        }
    }
}
