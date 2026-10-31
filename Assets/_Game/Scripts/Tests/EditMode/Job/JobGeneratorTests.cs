using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using SpiritMovers.Core.Job;

namespace SpiritMovers.Tests
{
    public class JobGeneratorTests
    {
        private static readonly ProblemType[] AllProblems =
            { ProblemType.Moved, ProblemType.Hidden, ProblemType.Order, ProblemType.Possessed };

        /// <summary>5 rooms × 8 slots, 10 scatter points, 6 hiding spots.</summary>
        private static HouseLayout TestHouse(int rooms = 5, int slotsPerRoom = 8, IReadOnlyList<ProblemType> supports = null)
        {
            supports ??= AllProblems;
            var roomList = new List<RoomLayout>();
            var scatter = new List<Placement>();
            var hiding = new List<Placement>();
            for (var r = 0; r < rooms; r++)
            {
                var roomId = $"r{r}";
                var slots = new List<SlotDefinition>();
                for (var i = 0; i < slotsPerRoom; i++)
                    slots.Add(new SlotDefinition($"{roomId}-s{i}", $"item{r}-{i}",
                        new Placement(roomId, new Vector3(r * 10 + i, 0f, 0f), Quaternion.Identity), Tolerance.Default, supports));
                roomList.Add(new RoomLayout(roomId, slots));
                scatter.Add(new Placement(roomId, new Vector3(r * 10, 0f, 7f), Quaternion.Identity));
                scatter.Add(new Placement(roomId, new Vector3(r * 10 + 3, 0f, 7f), Quaternion.Identity));
            }
            for (var h = 0; h < 6; h++)
                hiding.Add(new Placement($"r{h % rooms}", new Vector3(h * 10, 0f, -5f), Quaternion.Identity));
            return new HouseLayout("test", roomList, scatter, hiding);
        }

        private static string Signature(JobPlan plan) => string.Join("|", plan.Rooms.SelectMany(room =>
            room.Slots.Select(t => $"{room.RoomId}/{t.Slot.Id}/{t.Problem}/{t.StartPlacement.RoomId}/{t.StartPlacement.Position}")));

        private static IEnumerable<JobPlan> Plans(JobDifficulty difficulty, int players, HouseLayout house = null) =>
            Enumerable.Range(1, 30).Select(seed => JobGenerator.Generate(seed, house ?? TestHouse(), difficulty, players));

        [Test]
        public void Generate_SameSeed_GivesIdenticalPlan()
        {
            var a = JobGenerator.Generate(42, TestHouse(), JobDifficulty.Hard, 3);
            var b = JobGenerator.Generate(42, TestHouse(), JobDifficulty.Hard, 3);
            Assert.AreEqual(Signature(a), Signature(b));
            Assert.AreEqual(42, a.Seed);
        }

        [Test]
        public void Generate_DifferentSeed_GivesDifferentPlan()
        {
            var a = JobGenerator.Generate(1, TestHouse(), JobDifficulty.Normal, 2);
            var b = JobGenerator.Generate(2, TestHouse(), JobDifficulty.Normal, 2);
            Assert.AreNotEqual(Signature(a), Signature(b));
        }

        [Test]
        public void Generate_Normal_TwoPlayers_ThreeRoomsThreeToFourItems()
        {
            foreach (var plan in Plans(JobDifficulty.Normal, 2))
            {
                Assert.AreEqual(3, plan.Rooms.Count);
                Assert.That(plan.Rooms.Select(r => r.Slots.Count), Is.All.InRange(3, 4));
            }
        }

        [Test]
        public void Generate_Hard_FourToFiveRoomsFiveToSixItems()
        {
            foreach (var plan in Plans(JobDifficulty.Hard, 2))
            {
                Assert.That(plan.Rooms.Count, Is.InRange(4, 5));
                Assert.That(plan.Rooms.Select(r => r.Slots.Count), Is.All.InRange(5, 6));
            }
        }

        [Test]
        public void Generate_Solo_RemovesOneItemPerRoom()
        {
            foreach (var plan in Plans(JobDifficulty.Normal, 1))
                Assert.That(plan.Rooms.Select(r => r.Slots.Count), Is.All.InRange(2, 3));
        }

        [Test]
        public void Generate_OnlyUsesProblemsAllowedByDifficultyAndSlot()
        {
            var normal = Plans(JobDifficulty.Normal, 2).SelectMany(p => p.Rooms).SelectMany(r => r.Slots).Select(t => t.Problem);
            Assert.IsTrue(normal.All(p => p == ProblemType.Moved || p == ProblemType.Hidden));

            var movedOnly = TestHouse(supports: new[] { ProblemType.Moved });
            var hard = Plans(JobDifficulty.Hard, 2, movedOnly).SelectMany(p => p.Rooms).SelectMany(r => r.Slots).Select(t => t.Problem);
            Assert.That(hard, Is.All.EqualTo(ProblemType.Moved));
        }

        [Test]
        public void Generate_MovedItemsStartAwayFromTarget()
        {
            foreach (var task in Plans(JobDifficulty.Hard, 2).SelectMany(p => p.Rooms).SelectMany(r => r.Slots)
                         .Where(t => t.Problem == ProblemType.Moved))
                Assert.IsFalse(PlacementMath.IsCorrect(task.Slot.Target, task.StartPlacement, task.Slot.Tolerance), task.Slot.Id);
        }

        [Test]
        public void Generate_HiddenItemsStartAtHidingSpot()
        {
            var house = TestHouse();
            var hidden = Plans(JobDifficulty.Normal, 2, house).SelectMany(p => p.Rooms).SelectMany(r => r.Slots)
                .Where(t => t.Problem == ProblemType.Hidden).ToList();
            Assert.IsNotEmpty(hidden);
            foreach (var task in hidden)
                Assert.IsTrue(house.HidingSpots.Any(h => h.RoomId == task.StartPlacement.RoomId && h.Position == task.StartPlacement.Position),
                    task.Slot.Id);
        }

        [Test]
        public void Generate_NeverUsesSameSlotTwice()
        {
            foreach (var plan in Plans(JobDifficulty.Hard, 6))
            {
                var ids = plan.Rooms.SelectMany(r => r.Slots).Select(t => t.Slot.Id).ToList();
                Assert.AreEqual(ids.Count, ids.Distinct().Count());
            }
        }

        [Test]
        public void Generate_WhenHouseHasFewerSlotsThanRequested_UsesAllAvailable()
        {
            var plan = JobGenerator.Generate(7, TestHouse(rooms: 2, slotsPerRoom: 2), JobDifficulty.Hard, 6);
            Assert.AreEqual(2, plan.Rooms.Count);
            Assert.That(plan.Rooms.Select(r => r.Slots.Count), Is.All.EqualTo(2));
        }

        /// <summary>5 rooms; r0 and r1 only support Order, which Normal does not use.</summary>
        private static HouseLayout HouseWithUnusableRooms()
        {
            var house = TestHouse();
            var rooms = house.Rooms.Select((room, i) => i >= 2 ? room : new RoomLayout(room.Id,
                room.Slots.Select(sl => new SlotDefinition(sl.Id, sl.ItemId, sl.Target, sl.Tolerance, new[] { ProblemType.Order })).ToList())).ToList();
            return new HouseLayout(house.Id, rooms, house.ScatterPoints, house.HidingSpots);
        }

        [Test]
        public void Generate_SkipsRoomsWithNoUsableSlotsBeforePicking()
        {
            foreach (var plan in Plans(JobDifficulty.Normal, 2, HouseWithUnusableRooms()))
            {
                Assert.AreEqual(3, plan.Rooms.Count, $"seed {plan.Seed}");
                Assert.IsFalse(plan.Rooms.Any(r => r.RoomId == "r0" || r.RoomId == "r1"));
            }
        }

        [Test]
        public void Generate_HouseWithEmptyRoom_IgnoresIt()
        {
            var house = TestHouse(rooms: 3);
            var rooms = house.Rooms.Append(new RoomLayout("empty", Array.Empty<SlotDefinition>())).ToList();
            var plan = JobGenerator.Generate(3, new HouseLayout("h", rooms, house.ScatterPoints, house.HidingSpots), JobDifficulty.Normal, 2);
            Assert.AreEqual(3, plan.Rooms.Count);
            Assert.IsFalse(plan.Rooms.Any(r => r.RoomId == "empty"));
        }

        [Test]
        public void Generate_NoUsableRooms_Throws()
        {
            var possessedOnly = TestHouse(supports: new[] { ProblemType.Possessed });
            Assert.Throws<InvalidOperationException>(() => JobGenerator.Generate(1, possessedOnly, JobDifficulty.Normal, 2));
            var noRooms = new HouseLayout("h", Array.Empty<RoomLayout>(), Array.Empty<Placement>(), Array.Empty<Placement>());
            Assert.Throws<InvalidOperationException>(() => JobGenerator.Generate(1, noRooms, JobDifficulty.Normal, 2));
        }

        [Test]
        public void Generate_StartPlacementsAreDistinctWhileSpotsLast()
        {
            // Items dropped on the same spot would spawn inside each other.
            foreach (var plan in Plans(JobDifficulty.Normal, 2))
            {
                var tasks = plan.Rooms.SelectMany(r => r.Slots).ToList();
                var scattered = tasks.Where(t => t.Problem != ProblemType.Hidden).Select(t => t.StartPlacement.Position).ToList();
                var hidden = tasks.Where(t => t.Problem == ProblemType.Hidden).Select(t => t.StartPlacement.Position).ToList();
                Assert.AreEqual(Math.Min(scattered.Count, 10), scattered.Distinct().Count(), $"seed {plan.Seed}");
                Assert.AreEqual(Math.Min(hidden.Count, 6), hidden.Distinct().Count(), $"seed {plan.Seed}");
            }
        }

        [Test]
        public void Generate_HiddenNeverStartsAlreadyCorrect()
        {
            // One hiding spot is exactly slot s0's remembered place (e.g. its home is the cupboard).
            var target = new Placement("r0", Vector3.Zero, Quaternion.Identity);
            var slots = new[]
            {
                new SlotDefinition("s0", "i0", target, Tolerance.Default, new[] { ProblemType.Hidden }),
                new SlotDefinition("s1", "i1", new Placement("r0", new Vector3(3f, 0f, 0f), Quaternion.Identity), Tolerance.Default, new[] { ProblemType.Hidden }),
            };
            var house = new HouseLayout("h", new[] { new RoomLayout("r0", slots) },
                new[] { new Placement("r0", new Vector3(8f, 0f, 8f), Quaternion.Identity) },
                new[] { target, new Placement("r0", new Vector3(-6f, 0f, 0f), Quaternion.Identity) });
            foreach (var plan in Plans(JobDifficulty.Normal, 2, house))
                foreach (var task in plan.Rooms.SelectMany(r => r.Slots))
                    Assert.IsFalse(PlacementMath.IsCorrect(task.Slot.Target, task.StartPlacement, task.Slot.Tolerance), $"seed {plan.Seed} {task.Slot.Id}");
        }

        [Test]
        public void PlayerCountScaling_ClampsOutOfRange()
        {
            Assert.AreEqual(PlayerCountScaling.ItemOffset(1), PlayerCountScaling.ItemOffset(0));
            Assert.AreEqual(PlayerCountScaling.AngerMultiplier(6), PlayerCountScaling.AngerMultiplier(9));
        }
    }
}
