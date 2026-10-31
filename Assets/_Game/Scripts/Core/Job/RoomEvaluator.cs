using System.Collections.Generic;

namespace SpiritMovers.Core.Job
{
    /// <summary>Broken items that are not repaired are not in any slot; repaired ones count half (GDD 7).</summary>
    public enum ItemCondition
    {
        Intact,
        Glued,
    }

    /// <summary>One memory slot of a room as it is right now.</summary>
    public readonly struct SlotState
    {
        public string SlotId { get; }
        public Placement Target { get; }
        public Tolerance Tolerance { get; }
        public Placement Actual { get; }
        public ItemCondition Condition { get; }

        public SlotState(string slotId, Placement target, Tolerance tolerance, Placement actual, ItemCondition condition)
        {
            SlotId = slotId;
            Target = target;
            Tolerance = tolerance;
            Actual = actual;
            Condition = condition;
        }
    }

    public sealed class RoomRules
    {
        /// <summary>Share of the room's slots (weighted) that must be correct for the ghost to accept it ⚙.</summary>
        public float AcceptRatio { get; set; } = 0.8f;
        public float GluedWeight { get; set; } = 0.5f;

        public static RoomRules Default { get; } = new();
    }

    public readonly struct RoomResult
    {
        /// <summary>Items in place, glued ones included: what the ghost's nod reports (GDD 5.2).</summary>
        public int CorrectCount { get; }
        /// <summary>Weighted count used for acceptance: glued items count <see cref="RoomRules.GluedWeight"/>.</summary>
        public float Score { get; }
        public bool Accepted { get; }

        public RoomResult(int correctCount, float score, bool accepted)
        {
            CorrectCount = correctCount;
            Score = score;
            Accepted = accepted;
        }
    }

    public static class RoomEvaluator
    {
        public static RoomResult Evaluate(IReadOnlyList<SlotState> slots, RoomRules rules)
        {
            var correct = 0;
            var score = 0f;
            foreach (var slot in slots)
            {
                if (!PlacementMath.IsCorrect(slot.Target, slot.Actual, slot.Tolerance))
                    continue;
                correct++;
                score += slot.Condition == ItemCondition.Glued ? rules.GluedWeight : 1f;
            }

            var accepted = slots.Count > 0 && score >= rules.AcceptRatio * slots.Count;
            return new RoomResult(correct, score, accepted);
        }
    }
}
