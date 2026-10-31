using System.Collections.Generic;
using NUnit.Framework;
using SpiritMovers.Core.Job;

namespace SpiritMovers.Tests
{
    public class AngerModelTests
    {
        private const float Delta = 1e-3f;

        /// <summary>Config with whole-number steps so thresholds can be hit exactly.</summary>
        private static AngerConfig Stepped(float itemBroken = 8f) => new()
        {
            PerSecond = 1f,
            EventAmounts = new Dictionary<AngerEvent, float> { [AngerEvent.ItemBroken] = itemBroken },
        };

        [Test]
        public void NewModel_IsCalmStageZero()
        {
            var model = new AngerModel(AngerConfig.Default, 1f);
            Assert.AreEqual(0f, model.Value);
            Assert.AreEqual(0, model.Stage);
        }

        [Test]
        public void Apply_ItemBroken_AddsEightTimesMultiplier()
        {
            var model = new AngerModel(AngerConfig.Default, 1.4f);
            model.Apply(AngerEvent.ItemBroken);
            Assert.AreEqual(11.2f, model.Value, Delta);
        }

        [Test]
        public void Stage_CrossesThresholds()
        {
            var model = new AngerModel(Stepped(), 1f);
            model.Tick(24f, false);
            Assert.AreEqual(0, model.Stage);
            model.Tick(1f, false);
            Assert.AreEqual(1, model.Stage);
            model.Tick(25f, false);
            Assert.AreEqual(2, model.Stage);
            model.Tick(25f, false);
            Assert.AreEqual(3, model.Stage);
        }

        [Test]
        public void ReachingMax_IsEvicted()
        {
            var model = new AngerModel(AngerConfig.Default, 1f);
            for (var i = 0; i < 13; i++)
                model.Apply(AngerEvent.ItemBroken);
            Assert.AreEqual(100f, model.Value, Delta);
            Assert.AreEqual(4, model.Stage);
            Assert.IsTrue(model.IsEvicted);
        }

        [Test]
        public void Apply_AfterEviction_IsIgnored()
        {
            var model = new AngerModel(AngerConfig.Default, 1f);
            for (var i = 0; i < 13; i++)
                model.Apply(AngerEvent.ItemBroken);
            model.OnRoomAccepted();
            Assert.IsFalse(model.Wai("a"));
            model.Tick(60f, true);
            Assert.AreEqual(100f, model.Value, Delta);
            Assert.AreEqual(4, model.Stage);
        }

        [Test]
        public void Wai_SameRoomTwice_ReducesOnce()
        {
            var model = new AngerModel(Stepped(), 1f);
            model.Tick(30f, false);
            Assert.IsTrue(model.Wai("a"));
            Assert.AreEqual(20f, model.Value, Delta);
            Assert.IsFalse(model.Wai("a"));
            Assert.AreEqual(20f, model.Value, Delta);
            Assert.IsTrue(model.Wai("b"));
            Assert.AreEqual(10f, model.Value, Delta);
        }

        [Test]
        public void Tick_NotSilent_AddsPerSecond()
        {
            var model = new AngerModel(AngerConfig.Default, 1f);
            model.Tick(10f, false);
            Assert.AreEqual(0.5f, model.Value, Delta);
        }

        [Test]
        public void Tick_SilentLongerThanGrace_Reduces()
        {
            var model = new AngerModel(AngerConfig.Default, 1f);
            model.Tick(600f, false);
            Assert.AreEqual(30f, model.Value, Delta);

            model.Tick(5f, true);
            Assert.AreEqual(30f, model.Value, Delta, "grace period: no rise, no drop");
            model.Tick(4f, true);
            Assert.AreEqual(28f, model.Value, Delta);
            model.Tick(1f, false);
            Assert.AreEqual(28.05f, model.Value, Delta);
            model.Tick(5f, true);
            Assert.AreEqual(28.05f, model.Value, Delta, "noise restarts the grace period");
        }

        [Test]
        public void Value_NeverBelowZero()
        {
            var model = new AngerModel(AngerConfig.Default, 1f);
            model.OnRoomAccepted();
            Assert.AreEqual(0f, model.Value);
        }

        [Test]
        public void StageChanged_FiresOncePerChange()
        {
            var model = new AngerModel(Stepped(itemBroken: 60f), 1f);
            var stages = new List<int>();
            model.StageChanged += stages.Add;
            model.Apply(AngerEvent.ItemBroken);
            CollectionAssert.AreEqual(new[] { 2 }, stages);
        }

        [Test]
        public void HighestStage_RemembersPeak()
        {
            var model = new AngerModel(Stepped(itemBroken: 80f), 1f);
            model.Apply(AngerEvent.ItemBroken);
            model.OnRoomAccepted();
            Assert.AreEqual(2, model.Stage);
            Assert.AreEqual(3, model.HighestStage);
        }
    }
}
