using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpiritMovers.Core.Job;
using SpiritMovers.Player;
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
        public IEnumerator PlanHashMismatch_IsReported()
        {
            LogAssert.Expect(LogType.Error, new Regex("plan mismatch"));
            Director.ApplyHostPlan(7, "deadbeef");
            Assert.IsFalse(Director.PlanMatchesHost);
            yield break;
        }
    }
}
