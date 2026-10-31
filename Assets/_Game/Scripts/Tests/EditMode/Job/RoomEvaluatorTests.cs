using System;
using System.Numerics;
using NUnit.Framework;
using SpiritMovers.Core.Job;

namespace SpiritMovers.Tests
{
    public class RoomEvaluatorTests
    {
        private static readonly Placement Target = new("living", new Vector3(1f, 0f, 2f), Quaternion.Identity);

        private static Quaternion Yaw(float degrees) =>
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, degrees * MathF.PI / 180f);

        private static SlotState Slot(string id, Placement actual, ItemCondition condition = ItemCondition.Intact) =>
            new(id, Target, Tolerance.Default, actual, condition);

        private static readonly Placement Correct = Target;
        private static readonly Placement Wrong = new("living", new Vector3(5f, 0f, 2f), Quaternion.Identity);

        [Test]
        public void IsCorrect_WithinTolerance_IsTrue()
        {
            var actual = new Placement("living", new Vector3(1.2f, 0f, 2f), Yaw(15f));
            Assert.IsTrue(PlacementMath.IsCorrect(Target, actual, Tolerance.Default));
        }

        [Test]
        public void IsCorrect_TooFar_IsFalse()
        {
            var actual = new Placement("living", new Vector3(1.3f, 0f, 2f), Quaternion.Identity);
            Assert.IsFalse(PlacementMath.IsCorrect(Target, actual, Tolerance.Default));
        }

        [Test]
        public void IsCorrect_RotatedTooMuch_IsFalse()
        {
            var actual = new Placement("living", Target.Position, Yaw(25f));
            Assert.IsFalse(PlacementMath.IsCorrect(Target, actual, Tolerance.Default));
        }

        [Test]
        public void IsCorrect_WrongRoom_IsFalse()
        {
            var actual = new Placement("kitchen", Target.Position, Target.Rotation);
            Assert.IsFalse(PlacementMath.IsCorrect(Target, actual, Tolerance.Default));
        }

        [Test]
        public void IsCorrect_WithNegatedQuaternion_IsTrue()
        {
            // q and -q describe the same orientation; physics may return either.
            var negated = new Quaternion(-0f, -0f, -0f, -1f);
            var actual = new Placement("living", Target.Position, negated);
            Assert.IsTrue(PlacementMath.IsCorrect(Target, actual, Tolerance.Default));
        }

        [Test]
        public void IsCorrect_ZeroQuaternionCountsAsIdentity()
        {
            // A slot authored with a default (0,0,0,0) rotation must still be satisfiable.
            var target = new Placement("living", Target.Position, new Quaternion(0f, 0f, 0f, 0f));
            Assert.IsTrue(PlacementMath.IsCorrect(target, Target, Tolerance.Default));
        }

        [Test]
        public void Evaluate_ThreeCorrect_IsAcceptedWithCountThree()
        {
            var result = RoomEvaluator.Evaluate(new[] { Slot("a", Correct), Slot("b", Correct), Slot("c", Correct) }, RoomRules.Default);
            Assert.AreEqual(3, result.CorrectCount);
            Assert.AreEqual(3f, result.Score, 1e-5f);
            Assert.IsTrue(result.Accepted);
        }

        [Test]
        public void Evaluate_TwoCorrectOneGlued_IsAccepted()
        {
            var result = RoomEvaluator.Evaluate(
                new[] { Slot("a", Correct), Slot("b", Correct), Slot("c", Correct, ItemCondition.Glued) }, RoomRules.Default);
            Assert.AreEqual(3, result.CorrectCount);
            Assert.AreEqual(2.5f, result.Score, 1e-5f);
            Assert.IsTrue(result.Accepted, "2.5 >= 0.8 x 3 = 2.4");
        }

        [Test]
        public void Evaluate_TwoCorrectOneWrong_IsNotAccepted()
        {
            var result = RoomEvaluator.Evaluate(new[] { Slot("a", Correct), Slot("b", Correct), Slot("c", Wrong) }, RoomRules.Default);
            Assert.AreEqual(2, result.CorrectCount);
            Assert.AreEqual(2f, result.Score, 1e-5f);
            Assert.IsFalse(result.Accepted);
        }

        [Test]
        public void Evaluate_EveryItemInPlaceWithGlue_IsAccepted()
        {
            // A small room must stay winnable after a breakage: 1.5 < 0.8 x 2 and 0.5 < 0.8 x 1.
            Assert.IsTrue(RoomEvaluator.Evaluate(new[] { Slot("a", Correct), Slot("b", Correct, ItemCondition.Glued) }, RoomRules.Default).Accepted);
            Assert.IsTrue(RoomEvaluator.Evaluate(new[] { Slot("a", Correct, ItemCondition.Glued) }, RoomRules.Default).Accepted);
        }

        [Test]
        public void Defaults_AreFreshInstances()
        {
            Tolerance.Default.PositionMeters = 5f;
            RoomRules.Default.AcceptRatio = 0.1f;
            Assert.AreEqual(0.25f, Tolerance.Default.PositionMeters);
            Assert.AreEqual(0.8f, RoomRules.Default.AcceptRatio);
        }

        [Test]
        public void Evaluate_EmptyRoom_IsNotAccepted()
        {
            var result = RoomEvaluator.Evaluate(Array.Empty<SlotState>(), RoomRules.Default);
            Assert.AreEqual(0, result.CorrectCount);
            Assert.IsFalse(result.Accepted);
        }
    }
}
