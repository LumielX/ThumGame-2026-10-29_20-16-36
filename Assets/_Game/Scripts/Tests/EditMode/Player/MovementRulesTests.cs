using NUnit.Framework;
using SpiritMovers.Core.Player;

namespace SpiritMovers.Tests
{
    public class MovementRulesTests
    {
        private const float Delta = 1e-4f;

        private static MoveResult Move(float forward = 0f, bool sprint = false, bool crouch = false,
            bool staminaAllowsRun = true, CarryLoad load = CarryLoad.None) =>
            MovementRules.Evaluate(new MoveIntent(forward, 0f, sprint, crouch, false), staminaAllowsRun, load, MovementConfig.Default);

        [Test]
        public void Idle_HasNoSpeedAndNoNoise()
        {
            var result = Move();
            Assert.AreEqual(0f, result.Speed);
            Assert.AreEqual(0f, result.Noise);
        }

        [Test]
        public void Walk_UsesWalkSpeedAndWalkNoise()
        {
            var result = Move(1f);
            Assert.AreEqual(3.0f, result.Speed, Delta);
            Assert.AreEqual(0.3f, result.Noise, Delta);
        }

        [Test]
        public void Sprint_WithStamina_Runs()
        {
            var result = Move(1f, sprint: true);
            Assert.AreEqual(5.5f, result.Speed, Delta);
            Assert.IsTrue(result.Running);
            Assert.AreEqual(1.0f, result.Noise, Delta);
        }

        [Test]
        public void Sprint_WithoutStamina_Walks()
        {
            var result = Move(1f, sprint: true, staminaAllowsRun: false);
            Assert.AreEqual(3.0f, result.Speed, Delta);
            Assert.IsFalse(result.Running);
        }

        [Test]
        public void Crouch_BeatsSprint()
        {
            var result = Move(1f, sprint: true, crouch: true);
            Assert.AreEqual(1.6f, result.Speed, Delta);
            Assert.IsTrue(result.Crouching);
            Assert.IsFalse(result.Running);
            Assert.AreEqual(0.05f, result.Noise, Delta);
        }

        [Test]
        public void Heavy_CannotRunOrJumpAndIsSlower()
        {
            var result = Move(1f, sprint: true, load: CarryLoad.Heavy);
            Assert.AreEqual(1.8f, result.Speed, Delta);
            Assert.IsFalse(result.Running);
            Assert.IsFalse(result.CanJump);
        }

        [Test]
        public void SharedHeavy_IsFasterThanHeavyAlone()
        {
            Assert.AreEqual(2.55f, Move(1f, load: CarryLoad.HeavyShared).Speed, Delta);
        }

        [Test]
        public void Stamina_DrainsWhileRunningAndStopsRun()
        {
            var stamina = new Stamina();
            stamina.Tick(5.1f, true);
            Assert.AreEqual(0f, stamina.Current, Delta);
            Assert.IsFalse(stamina.AllowsRun(true));
        }

        [Test]
        public void Stamina_RegeneratesAfterDelay()
        {
            var stamina = new Stamina();
            stamina.Tick(6f, true);
            stamina.Tick(1f, false);
            Assert.AreEqual(0f, stamina.Current, Delta);
            stamina.Tick(1f, false);
            Assert.AreEqual(1f, stamina.Current, Delta);
        }

        [Test]
        public void Stamina_NeedsMinimumToStartRunning()
        {
            var stamina = new Stamina();
            stamina.Tick(4.7f, true); // 0.3 left
            Assert.AreEqual(0.3f, stamina.Current, 1e-3f);
            Assert.IsFalse(stamina.AllowsRun(false));
            Assert.IsTrue(stamina.AllowsRun(true));
        }
    }
}
