using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpiritMovers.Core.Job;
using SpiritMovers.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class JobStartTests : TestHouseFixture
    {
        private static string Signature(JobPlan plan) => string.Join("|", plan.Rooms.SelectMany(room =>
            room.Slots.Select(t => $"{room.RoomId}/{t.Slot.Id}/{t.Problem}/{t.StartPlacement.Position}")));

        [UnityTest]
        public IEnumerator BuildLayout_FollowsHierarchyOrder()
        {
            var layout = House.BuildLayout();
            CollectionAssert.AreEqual(new[] { "west", "east", "loft" }, layout.Rooms.Select(r => r.Id));
            CollectionAssert.AreEqual(new[] { 5, 4, 3 }, layout.Rooms.Select(r => r.Slots.Count));
            yield break;
        }

        [UnityTest]
        public IEnumerator RoomAt_FindsRoomOrEmpty()
        {
            Assert.AreEqual("west", House.RoomAt(new Vector3(-5f, 0.3f, 0f)));
            Assert.AreEqual("loft", House.RoomAt(new Vector3(8f, 1.4f, -6f)));
            Assert.AreEqual("east", House.RoomAt(new Vector3(5f, 0.3f, 3f)));
            Assert.AreEqual("", House.RoomAt(new Vector3(0f, 0f, 30f)));
            yield break;
        }

        [UnityTest]
        public IEnumerator BeginJob_UnpacksHouse()
        {
            Director.BeginJob(1, JobDifficulty.Normal);
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(JobPhase.Exploring, Director.Phase);
            foreach (var task in Director.Plan.Rooms.SelectMany(r => r.Slots))
            {
                var item = House.Slot(task.Slot.Id).Item.transform.position;
                var start = task.StartPlacement.Position.ToUnity();
                Assert.Less(Vector2.Distance(new Vector2(item.x, item.z), new Vector2(start.x, start.z)), 0.05f, task.Slot.Id);
                var actual = new Placement(House.RoomAt(item), item.ToNumerics(), task.StartPlacement.Rotation);
                Assert.IsFalse(PlacementMath.IsCorrect(task.Slot.Target, actual, task.Slot.Tolerance), task.Slot.Id);
            }
        }

        [UnityTest]
        public IEnumerator BeginJob_SameSeed_SamePlan()
        {
            Director.BeginJob(7, JobDifficulty.Normal);
            yield return null;
            Assert.AreEqual(Signature(JobGenerator.Generate(7, House.BuildLayout(), JobDifficulty.Normal, 1)), Signature(Director.Plan));
        }

        [UnityTest]
        public IEnumerator BeginJob_AfterLeaveEarly_IsIgnored()
        {
            Director.LeaveEarly();
            Director.BeginJob(1, JobDifficulty.Normal);
            yield return null;
            Assert.AreEqual(JobPhase.Ended, Director.Phase);
            Assert.IsNull(Director.Plan);
        }

        [UnityTest]
        public IEnumerator BeginJob_Twice_KeepsFirstPlan()
        {
            Director.BeginJob(1, JobDifficulty.Normal);
            var plan = Director.Plan;
            Director.BeginJob(2, JobDifficulty.Normal);
            yield return null;
            Assert.AreSame(plan, Director.Plan);
        }

        [UnityTest]
        public IEnumerator BeginJob_ReleasesCarriedItems()
        {
            var task = JobGenerator.Generate(5, House.BuildLayout(), JobDifficulty.Normal, 1).Rooms[0].Slots[0];
            var item = House.Slot(task.Slot.Id).Item;
            item.SetSingleCarrierForTest(NetworkManager.ServerClientId);
            Director.BeginJob(5, JobDifficulty.Normal);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(item.IsCarried);
            var start = task.StartPlacement.Position.ToUnity();
            var at = item.transform.position;
            Assert.Less(Vector2.Distance(new Vector2(at.x, at.z), new Vector2(start.x, start.z)), 0.05f);
        }

        private JobPlan PlanFor(int seed) => JobGenerator.Generate(seed, House.BuildLayout(), JobDifficulty.Normal, 1);

        [UnityTest]
        public IEnumerator HostSpec_MatchingPlan_IsAccepted()
        {
            var plan = PlanFor(7);
            Director.ApplyHostPlan(7, "normal", plan.LayoutHash, plan.ContentHash());
            Assert.IsTrue(Director.PlanMatchesHost);
            Assert.IsNotNull(Director.Plan);
            yield break;
        }

        [UnityTest]
        public IEnumerator PlanHashMismatch_IsReported()
        {
            LogAssert.Expect(LogType.Error, new Regex("plan mismatch"));
            Director.ApplyHostPlan(7, "normal", "deadbeef", PlanFor(7).ContentHash());
            Assert.IsFalse(Director.PlanMatchesHost);
            Assert.IsNull(Director.Plan, "no work order from a plan the host does not have");
            yield break;
        }

        [UnityTest]
        public IEnumerator PlanRulesMismatch_IsReported()
        {
            LogAssert.Expect(LogType.Error, new Regex("plan mismatch"));
            Director.ApplyHostPlan(7, "normal", PlanFor(7).LayoutHash, "deadbeef");
            Assert.IsFalse(Director.PlanMatchesHost);
            yield break;
        }

        [UnityTest]
        public IEnumerator UnknownDifficulty_IsReported()
        {
            LogAssert.Expect(LogType.Error, new Regex("plan mismatch"));
            var plan = PlanFor(7);
            Director.ApplyHostPlan(7, "nightmare", plan.LayoutHash, plan.ContentHash());
            Assert.IsFalse(Director.PlanMatchesHost);
            yield break;
        }
    }
}
