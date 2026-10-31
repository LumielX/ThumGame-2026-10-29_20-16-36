using System.Collections;
using NUnit.Framework;
using SpiritMovers.Core.Job;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class PlayerSyncTests : TestHouseFixture
    {
        private static IEnumerator Frames(int count)
        {
            for (var i = 0; i < count; i++)
                yield return null;
        }

        [UnityTest]
        public IEnumerator Crouch_UpdatesSyncedState()
        {
            Input.Next.Crouch = true;
            yield return Frames(3);
            Assert.IsTrue(Motor.Crouching);
            Input.Next.Crouch = false;
            yield return Frames(3);
            Assert.IsFalse(Motor.Crouching);
        }

        [UnityTest]
        public IEnumerator Look_FollowsPitch()
        {
            Motor.LookAt(Motor.Feet + Motor.Forward * 2f);
            yield return null;
            Assert.Less(Motor.Look.y, -0.3f, "looking down at the floor");
            Motor.LookAt(Motor.Head.position + Motor.Forward * 5f);
            yield return null;
            Assert.AreEqual(0f, Motor.Look.y, 0.05f);
        }

        [UnityTest]
        public IEnumerator Wai_CalmsOncePerRoom()
        {
            Director.BeginJob(3, JobDifficulty.Normal);
            Director.ApplyAngerForTest(30f);
            var before = Director.AngerValue;
            Input.Next.WaiPressed = true;
            yield return Frames(3);
            var after = Director.AngerValue;
            Assert.LessOrEqual(after, before - 9.9f, "the wai calms the ghost by 10");

            Input.Next.WaiPressed = true;
            yield return Frames(3);
            Assert.GreaterOrEqual(Director.AngerValue, after, "only once per room");
        }

        [UnityTest]
        public IEnumerator Wai_OutsideExploring_DoesNothing()
        {
            Assert.IsFalse(Director.Wai("west"));
            Director.BeginJob(3, JobDifficulty.Normal);
            Assert.IsFalse(Director.Wai(""), "outside every room");
            yield break;
        }
    }
}
