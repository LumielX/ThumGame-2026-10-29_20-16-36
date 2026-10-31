using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SpiritMovers.Core.Job
{
    /// <summary>One disturbed item: which slot, how it was disturbed and where it starts.</summary>
    public sealed class SlotTask
    {
        public SlotDefinition Slot { get; }
        public ProblemType Problem { get; }
        public Placement StartPlacement { get; }

        public SlotTask(SlotDefinition slot, ProblemType problem, Placement startPlacement)
        {
            Slot = slot;
            Problem = problem;
            StartPlacement = startPlacement;
        }
    }

    public sealed class RoomTask
    {
        public string RoomId { get; }
        public IReadOnlyList<SlotTask> Slots { get; }

        public RoomTask(string roomId, IReadOnlyList<SlotTask> slots)
        {
            RoomId = roomId;
            Slots = slots;
        }
    }

    /// <summary>Everything that makes one job different; the host shares the seed so every peer can rebuild it.</summary>
    public sealed class JobPlan
    {
        public int Seed { get; }
        public string HouseId { get; }
        public string DifficultyId { get; }
        public int PlayerCount { get; }
        public IReadOnlyList<RoomTask> Rooms { get; }

        public JobPlan(int seed, string houseId, string difficultyId, int playerCount, IReadOnlyList<RoomTask> rooms)
        {
            Seed = seed;
            HouseId = houseId;
            DifficultyId = difficultyId;
            PlayerCount = playerCount;
            Rooms = rooms;
        }
    }

    /// <summary>
    /// Picks which rooms and memory slots a job uses and how each item is disturbed (GDD 5.1, TECHNICAL_DESIGN 4.2).
    /// Deterministic: the same seed and inputs give the same plan on every machine, so all draws come from
    /// one <see cref="Random"/> in a fixed order.
    /// </summary>
    public static class JobGenerator
    {
        public static JobPlan Generate(int seed, HouseLayout house, JobDifficulty difficulty, int playerCount)
        {
            var random = new Random(seed);
            // Only rooms with a slot this difficulty can disturb: a room with nothing to fix could never be accepted.
            var rooms = house.Rooms.Where(r => r.Slots.Any(s => IsUsable(s, difficulty))).ToList();
            if (rooms.Count == 0)
                throw new InvalidOperationException(
                    $"House '{house.Id}' has no memory slot that supports a '{difficulty.Id}' problem; the job could never be won.");
            var roomCount = Math.Min(random.Next(difficulty.MinRooms, difficulty.MaxRooms + 1), rooms.Count);

            var picked = Shuffled(rooms, random).Take(roomCount).ToList();
            var spots = new SpotPool(house, random);
            var tasks = new List<RoomTask>();
            foreach (var room in rooms.Where(picked.Contains)) // keep the house's room order
                tasks.Add(new RoomTask(room.Id, PickSlots(room, difficulty, playerCount, random, spots)));

            return new JobPlan(seed, house.Id, difficulty.Id, playerCount, tasks);
        }

        private static bool IsUsable(SlotDefinition slot, JobDifficulty difficulty) => slot.Supports.Any(difficulty.Problems.Contains);

        private static List<SlotTask> PickSlots(RoomLayout room, JobDifficulty difficulty, int playerCount, Random random, SpotPool spots)
        {
            var wanted = random.Next(difficulty.MinItemsPerRoom, difficulty.MaxItemsPerRoom + 1) + PlayerCountScaling.ItemOffset(playerCount);
            wanted = Math.Clamp(wanted, 1, room.Slots.Count);

            var result = new List<SlotTask>();
            foreach (var slot in Shuffled(room.Slots, random))
            {
                if (result.Count == wanted)
                    break;
                var allowed = difficulty.Problems.Where(slot.Supports.Contains).ToList();
                if (allowed.Count == 0)
                    continue;
                var problem = allowed[random.Next(allowed.Count)];
                result.Add(new SlotTask(slot, problem, spots.Take(slot, problem)));
            }
            return result;
        }

        /// <summary>
        /// Hands out start spots without reuse until a list runs out (then it refills), so items do not
        /// spawn inside each other while there are spots to spare.
        /// </summary>
        private sealed class SpotPool
        {
            private readonly HouseLayout m_House;
            private readonly Random m_Random;
            private readonly List<Placement> m_Scatter;
            private readonly List<Placement> m_Hiding;

            public SpotPool(HouseLayout house, Random random)
            {
                m_House = house;
                m_Random = random;
                m_Scatter = house.ScatterPoints.ToList();
                m_Hiding = house.HidingSpots.ToList();
            }

            public Placement Take(SlotDefinition slot, ProblemType problem)
            {
                if (problem == ProblemType.Hidden && m_House.HidingSpots.Count > 0)
                    return Draw(m_Hiding, m_House.HidingSpots, _ => true);

                bool Away(Placement p) => !PlacementMath.IsCorrect(slot.Target, p, slot.Tolerance);
                if (m_House.ScatterPoints.Any(Away))
                    return Draw(m_Scatter, m_House.ScatterPoints, Away);

                var target = slot.Target;
                return new Placement(target.RoomId, target.Position + Vector3.UnitX, target.Rotation);
            }

            private Placement Draw(List<Placement> remaining, IReadOnlyList<Placement> all, Func<Placement, bool> allowed)
            {
                if (!remaining.Any(allowed))
                    remaining.AddRange(all.Where(p => !remaining.Contains(p)));
                var candidates = remaining.Where(allowed).ToList();
                var chosen = candidates[m_Random.Next(candidates.Count)];
                remaining.Remove(chosen);
                return chosen;
            }
        }

        /// <summary>Fisher–Yates on a copy; the input order is the tie-breaker, so the result depends only on the seed.</summary>
        private static List<T> Shuffled<T>(IReadOnlyList<T> items, Random random)
        {
            var list = items.ToList();
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }
    }
}
