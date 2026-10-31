using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpiritMovers.Core.Job;

namespace SpiritMovers.Tests
{
    public class JobProgressTests
    {
        private static JobProgress NewJob()
        {
            var rooms = new[] { "a", "b", "c" }.Select(id => new RoomTask(id, Array.Empty<SlotTask>())).ToList();
            return new JobProgress(new JobPlan(1, "house", "normal", 2, rooms));
        }

        [Test]
        public void NewProgress_IsBriefing()
        {
            var job = NewJob();
            Assert.AreEqual(JobPhase.Briefing, job.Phase);
            Assert.IsNull(job.Ending);
            Assert.AreEqual(3, job.RoomsRequired);
        }

        [Test]
        public void Begin_MovesToExploring()
        {
            var job = NewJob();
            job.Begin();
            Assert.AreEqual(JobPhase.Exploring, job.Phase);
        }

        [Test]
        public void AcceptRoom_UnknownOrDuplicate_ReturnsFalse()
        {
            var job = NewJob();
            job.Begin();
            Assert.IsFalse(job.AcceptRoom("x"));
            Assert.IsTrue(job.AcceptRoom("a"));
            Assert.IsFalse(job.AcceptRoom("a"));
            Assert.AreEqual(1, job.AcceptedRooms.Count);
        }

        [Test]
        public void AcceptRoom_BeforeBegin_ReturnsFalse()
        {
            Assert.IsFalse(NewJob().AcceptRoom("a"));
        }

        [Test]
        public void AcceptingAllRooms_MovesToFarewell()
        {
            var job = NewJob();
            job.Begin();
            job.AcceptRoom("a");
            job.AcceptRoom("b");
            job.AcceptRoom("c");
            Assert.AreEqual(JobPhase.Farewell, job.Phase);
        }

        [Test]
        public void CompleteFarewell_EndsWithFullSuccess()
        {
            var job = NewJob();
            job.Begin();
            foreach (var room in new[] { "a", "b", "c" })
                job.AcceptRoom(room);
            job.CompleteFarewell();
            Assert.AreEqual(JobPhase.Ended, job.Phase);
            Assert.AreEqual(JobEnding.FullSuccess, job.Ending);
        }

        [Test]
        public void LeaveEarly_EndsWithLeftEarlyKeepingAcceptedRooms()
        {
            var job = NewJob();
            job.Begin();
            job.AcceptRoom("a");
            job.LeaveEarly();
            Assert.AreEqual(JobPhase.Ended, job.Phase);
            Assert.AreEqual(JobEnding.LeftEarly, job.Ending);
            CollectionAssert.AreEquivalent(new[] { "a" }, job.AcceptedRooms);
        }

        [Test]
        public void Evict_EndsWithEvicted()
        {
            var job = NewJob();
            job.Begin();
            job.Evict();
            Assert.AreEqual(JobPhase.Ended, job.Phase);
            Assert.AreEqual(JobEnding.Evicted, job.Ending);
        }

        [Test]
        public void CommandsAfterEnd_AreIgnored()
        {
            var job = NewJob();
            job.Begin();
            job.Evict();
            var changes = new List<JobPhase>();
            job.PhaseChanged += changes.Add;

            job.LeaveEarly();
            Assert.IsFalse(job.AcceptRoom("a"));
            job.Begin();
            job.CompleteFarewell();

            Assert.AreEqual(JobEnding.Evicted, job.Ending);
            Assert.AreEqual(JobPhase.Ended, job.Phase);
            Assert.IsEmpty(changes);
        }

        [Test]
        public void PhaseChanged_FiresForEachTransition()
        {
            var job = NewJob();
            var changes = new List<JobPhase>();
            job.PhaseChanged += changes.Add;
            job.Begin();
            foreach (var room in new[] { "a", "b", "c" })
                job.AcceptRoom(room);
            job.CompleteFarewell();
            CollectionAssert.AreEqual(new[] { JobPhase.Exploring, JobPhase.Farewell, JobPhase.Ended }, changes);
        }
    }
}
