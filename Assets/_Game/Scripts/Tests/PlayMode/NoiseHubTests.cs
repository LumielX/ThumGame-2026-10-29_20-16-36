using System.Collections;
using System.Linq;
using NUnit.Framework;
using SpiritMovers.Core.Ghost;
using SpiritMovers.Core.Job;
using SpiritMovers.Ghost;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class NoiseHubTests : TestHouseFixture
    {
        private NoiseHub Hub => Object.FindAnyObjectByType<NoiseHub>();

        [UnityTest]
        public IEnumerator Running_ReportsLoudFootsteps()
        {
            yield return WaitForGrounded();
            Input.Next.Move = Vector2.up;
            Input.Next.Sprint = true;
            yield return new WaitForSeconds(1.5f);
            Input.Next = default;
            Assert.GreaterOrEqual(Hub.Recent.Count(n => n.Source == NoiseSource.Footstep && n.Loudness >= 0.99f), 2);
        }

        [UnityTest]
        public IEnumerator Crouching_IsQuiet()
        {
            yield return WaitForGrounded();
            var lastNoise = Hub.LastNoiseTime;
            Input.Next.Move = Vector2.up;
            Input.Next.Crouch = true;
            yield return new WaitForSeconds(1.5f);
            Input.Next = default;
            Assert.IsTrue(Hub.Recent.Any(n => n.Source == NoiseSource.Footstep), "crouching steps are still reported");
            Assert.IsTrue(Hub.Recent.All(n => n.Loudness < 0.1f));
            Assert.AreEqual(lastNoise, Hub.LastNoiseTime, "crouch-walking counts as silence");
        }

        [UnityTest]
        public IEnumerator DroppedItemImpact_IsHeard()
        {
            var body = House.Slot("w0").Item.GetComponent<Rigidbody>();
            body.position += Vector3.up * 2f;
            body.linearVelocity = Vector3.down * 2f;
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(Hub.Recent.Any(n => n.Source == NoiseSource.ItemImpact && n.Loudness > 0.5f));
        }

        [UnityTest]
        public IEnumerator LoudNoise_RaisesAngerOncePerCooldown()
        {
            Director.BeginJob(3, JobDifficulty.Normal);
            var before = Director.AngerValue;
            for (var i = 0; i < 3; i++)
                Hub.Report(new NoiseEvent(System.Numerics.Vector3.Zero, 1f, NoiseSource.ItemImpact));
            yield return null;
            var step = 3f * JobSetup.AngerMultiplier(JobDifficulty.Normal, 1);
            Assert.AreEqual(before + step, Director.AngerValue, 0.05f);
        }

        [UnityTest]
        public IEnumerator Silence_CalmsAnger()
        {
            Director.BeginJob(3, JobDifficulty.Normal);
            Director.ApplyAngerForTest(30f);
            var before = Director.AngerValue;
            yield return new WaitForSeconds(8f);
            Assert.Less(Director.AngerValue, before - 1f, "5 s grace, then 0.5 per second");
        }
    }
}
