using System.Collections;
using System.Linq;
using NUnit.Framework;
using SpiritMovers.Core.Job;
using SpiritMovers.Core.Text;
using SpiritMovers.Interaction;
using SpiritMovers.Job;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class JobEndingTests : TestHouseFixture
    {
        private PlayerCarrier Carrier => Motor.GetComponent<PlayerCarrier>();
        private VanExit Van => Object.FindAnyObjectByType<VanExit>();

        private static IEnumerator Until(System.Func<bool> condition, float timeout)
        {
            var end = Time.time + timeout;
            while (!condition() && Time.time < end)
                yield return null;
        }

        private IEnumerator AcceptRoom(RoomTask room)
        {
            foreach (var task in room.Slots)
                yield return PutBack(task.Slot.Id);
            yield return Until(() => Director.AcceptedRooms.Contains(room.RoomId), 1.5f);
            CollectionAssert.Contains(Director.AcceptedRooms, room.RoomId);
        }

        [UnityTest]
        public IEnumerator AllRoomsAccepted_EndsFullSuccessAfterFarewell()
        {
            Director.BeginJob(3, JobDifficulty.Normal);
            foreach (var room in Director.Plan.Rooms)
                yield return AcceptRoom(room);
            Assert.AreEqual(JobPhase.Farewell, Director.Phase);
            yield return Until(() => Director.Phase == JobPhase.Ended, 6f);
            Assert.AreEqual(JobEnding.FullSuccess, Director.Result.Ending);
            Assert.AreEqual(390, Director.Result.TeamTotal, "300 + 3 stars x 30");
            Assert.AreEqual(3, Director.Result.Stars);
        }

        [UnityTest]
        public IEnumerator VanLeave_EndsLeftEarlyWithProportionalPay()
        {
            Director.BeginJob(3, JobDifficulty.Normal);
            yield return AcceptRoom(Director.Plan.Rooms[0]);
            Van.Interact(Carrier);
            yield return Until(() => Director.Phase == JobPhase.Ended, 1f);
            Assert.AreEqual(JobEnding.LeftEarly, Director.Result.Ending);
            Assert.AreEqual(1, Director.Result.RoomsAccepted);
            Assert.AreEqual(3, Director.Result.RoomsRequired);
            Assert.AreEqual(100, Director.Result.TeamTotal);
        }

        [UnityTest]
        public IEnumerator LeaveTwice_KeepsFirstResult()
        {
            Director.BeginJob(3, JobDifficulty.Normal);
            yield return AcceptRoom(Director.Plan.Rooms[0]);
            Director.LeaveEarly();
            var first = Director.Result;
            Director.LeaveEarly();
            Van.Interact(Carrier);
            yield return Until(() => false, 0.3f);
            Assert.AreEqual(first, Director.Result);
        }

        [UnityTest]
        public IEnumerator AngerAtMax_EvictsWithHalfPay()
        {
            Director.BeginJob(3, JobDifficulty.Normal);
            yield return AcceptRoom(Director.Plan.Rooms[0]);
            Director.ApplyAngerForTest(200f);
            yield return Until(() => Director.Phase == JobPhase.Ended, 1f);
            Assert.AreEqual(JobEnding.Evicted, Director.Result.Ending);
            Assert.AreEqual(50, Director.Result.TeamTotal);
        }

        [UnityTest]
        public IEnumerator LeaveDuringBriefing_PaysNothing()
        {
            Director.LeaveEarly();
            yield return null;
            Assert.AreEqual(JobPhase.Ended, Director.Phase);
            Assert.AreEqual(JobEnding.LeftEarly, Director.Result.Ending);
            Assert.AreEqual(0, Director.Result.TeamTotal);
        }

        [UnityTest]
        public IEnumerator ResultsPanel_TextIsLocalizedAndShaped()
        {
            Director.LeaveEarly();
            var panel = Object.FindAnyObjectByType<ResultsPanel>();
            yield return Until(() => !string.IsNullOrEmpty(panel.LastText), 2f);
            StringAssert.Contains(ThaiShaper.Shape("สรุปงาน"), panel.LastText);
            StringAssert.Contains("0/0", panel.LastText);
        }
    }
}
