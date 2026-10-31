using System;
using System.Numerics;
using NUnit.Framework;
using SpiritMovers.Core.Player;

namespace SpiritMovers.Tests
{
    public class CarryRulesTests
    {
        private static readonly CarryConfig Config = CarryConfig.Default;

        private static void AssertNear(Vector3 expected, Vector3 actual) =>
            Assert.Less(Vector3.Distance(expected, actual), 1e-4f, $"expected {expected} got {actual}");

        [Test]
        public void LoadFor_ByMassAndCarriers()
        {
            Assert.AreEqual(CarryLoad.None, CarryRules.LoadFor(2f, 0, Config));
            Assert.AreEqual(CarryLoad.Light, CarryRules.LoadFor(2f, 1, Config));
            Assert.AreEqual(CarryLoad.Heavy, CarryRules.LoadFor(20f, 1, Config));
            Assert.AreEqual(CarryLoad.HeavyShared, CarryRules.LoadFor(20f, 2, Config));
        }

        [Test]
        public void MaxCarriers_LightOneHeavyTwo()
        {
            Assert.AreEqual(1, CarryRules.MaxCarriersFor(2f, Config));
            Assert.AreEqual(2, CarryRules.MaxCarriersFor(15f, Config));
        }

        [Test]
        public void CanThrow_OnlyLightSingleCarrier()
        {
            Assert.IsTrue(CarryRules.CanThrow(2f, 1, Config));
            Assert.IsFalse(CarryRules.CanThrow(20f, 1, Config));
            Assert.IsFalse(CarryRules.CanThrow(20f, 2, Config));
        }

        [Test]
        public void HoldPoint_LightInFrontAtChestHeight()
        {
            AssertNear(new Vector3(0f, 1.3f, 1.2f), CarryRules.HoldPoint(Vector3.Zero, Vector3.UnitZ, 2f, Config));
        }

        [Test]
        public void HoldPoint_IgnoresPitch()
        {
            var expected = new Vector3(0f, 1.3f, 1.2f);
            AssertNear(expected, CarryRules.HoldPoint(Vector3.Zero, new Vector3(0f, -0.9f, 0.1f), 2f, Config));
            AssertNear(expected, CarryRules.HoldPoint(Vector3.Zero, new Vector3(0f, 0.9f, 0.1f), 2f, Config));
        }

        [Test]
        public void SharedHoldPoint_IsAverage()
        {
            AssertNear(new Vector3(1f, 1f, 1f), CarryRules.SharedHoldPoint(new[] { new Vector3(0f, 1f, 0f), new Vector3(2f, 1f, 2f) }));
            Assert.Throws<ArgumentException>(() => CarryRules.SharedHoldPoint(Array.Empty<Vector3>()));
        }

        [Test]
        public void FollowVelocity_PullsTowardHold()
        {
            AssertNear(new Vector3(0f, 0f, 6f), CarryRules.FollowVelocity(new Vector3(0f, 0f, 0.5f), Vector3.Zero, Config));
        }

        [Test]
        public void FollowVelocity_IsClampedToMaxSpeed()
        {
            AssertNear(new Vector3(0f, 0f, 10f), CarryRules.FollowVelocity(new Vector3(0f, 0f, 5f), Vector3.Zero, Config));
        }

        [Test]
        public void HoldErrorWatch_TriggersAfterGrace()
        {
            var watch = new HoldErrorWatch(Config);
            Assert.IsFalse(watch.Update(2.5f, 0.3f));
            Assert.IsTrue(watch.Update(2.5f, 0.3f));
        }

        [Test]
        public void HoldErrorWatch_ResetsWhenBackInRange()
        {
            var watch = new HoldErrorWatch(Config);
            watch.Update(2.5f, 0.4f);
            watch.Update(0.5f, 0.1f);
            Assert.IsFalse(watch.Update(2.5f, 0.4f));
        }
    }
}
