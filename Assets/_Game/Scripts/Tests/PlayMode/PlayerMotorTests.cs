using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class PlayerMotorTests : TestRoomFixture
    {
        private IEnumerator Hold(float seconds)
        {
            var end = Time.time + seconds;
            while (Time.time < end)
                yield return null;
        }

        private IEnumerator MoveFor(float seconds, bool sprint = false, bool crouch = false)
        {
            Input.Next.Move = new Vector2(0f, 1f);
            Input.Next.Sprint = sprint;
            Input.Next.Crouch = crouch;
            yield return Hold(seconds);
            Input.Next = default;
        }

        [UnityTest]
        public IEnumerator WalkForwardOneSecond_MovesAboutWalkSpeed()
        {
            var start = Motor.Feet;
            yield return MoveFor(1f);
            Assert.That(Planar(start, Motor.Feet), Is.InRange(2.4f, 3.3f));
        }

        [UnityTest]
        public IEnumerator Sprint_IsFasterThanWalk()
        {
            var start = Motor.Feet;
            yield return MoveFor(1f, sprint: true);
            Assert.GreaterOrEqual(Planar(start, Motor.Feet), 4.4f);
        }

        [UnityTest]
        public IEnumerator Crouch_IsSlowerThanWalk()
        {
            var start = Motor.Feet;
            yield return MoveFor(1f, crouch: true);
            Assert.That(Planar(start, Motor.Feet), Is.InRange(1.1f, 1.9f));
        }

        [UnityTest]
        public IEnumerator SprintStopsWhenStaminaRunsOut()
        {
            Motor.Teleport(new Vector3(-8f, 0f, -8f), 45f); // longest straight run in the room
            yield return WaitForGrounded();
            Input.Next.Move = new Vector2(0f, 1f);
            Input.Next.Sprint = true;
            yield return Hold(6f);
            Assert.IsFalse(Motor.LastMove.Running);
            Assert.AreEqual(3.0f, Motor.LastMove.Speed, 0.01f);
        }

        [UnityTest]
        public IEnumerator Jump_LeavesGroundAndLands()
        {
            var floor = Motor.Feet.y;
            Input.Next.JumpPressed = true;
            var peak = floor;
            var end = Time.time + 0.3f;
            while (Time.time < end)
            {
                peak = Mathf.Max(peak, Motor.Feet.y);
                yield return null;
            }
            Assert.GreaterOrEqual(peak - floor, 0.3f);
            yield return Hold(1.2f);
            Assert.IsTrue(Motor.IsGrounded);
        }

        [UnityTest]
        public IEnumerator InputDisabled_StopsMovement()
        {
            Motor.InputEnabled = false;
            var start = Motor.Feet;
            yield return MoveFor(1f);
            Assert.Less(Planar(start, Motor.Feet), 0.05f);
        }

        [UnityTest]
        public IEnumerator CanClimbStairs()
        {
            var stairs = GameObject.Find("StairsBottom").transform;
            Motor.Teleport(stairs.position, stairs.eulerAngles.y);
            yield return WaitForGrounded();
            yield return MoveFor(3f);
            Assert.GreaterOrEqual(Motor.Feet.y, 1.1f);
        }
    }
}
