using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using SpiritMovers.Core.Ghost;

namespace SpiritMovers.Tests
{
    public class GhostRulesTests
    {
        private static readonly Func<Vector3, Vector3, bool> Clear = (_, _) => true;

        private static Viewer LookingNorth(ulong id, float x = 0f) => new(id, new Vector3(x, 1.6f, 0f), Vector3.UnitZ);

        [Test]
        public void ModeFor_MapsStagesAndClamps()
        {
            CollectionAssert.AreEqual(
                new[] { GhostMode.Idle, GhostMode.Prank, GhostMode.Disturb, GhostMode.Hunt, GhostMode.Evict },
                new[] { 0, 1, 2, 3, 4 }.Select(GhostRules.ModeFor));
            Assert.AreEqual(GhostMode.Idle, GhostRules.ModeFor(-1));
            Assert.AreEqual(GhostMode.Evict, GhostRules.ModeFor(9));
        }

        [Test]
        public void IsWatched_InConeAndClear_IsWatched()
        {
            Assert.IsTrue(GhostRules.IsWatched(new Vector3(0f, 1.2f, 5f), new[] { LookingNorth(1) }, new GhostConfig(), Clear));
        }

        [Test]
        public void IsWatched_OutsideCone_IsNotWatched()
        {
            // 60° off the view direction, 5 m away.
            var ghost = new Vector3(5f * MathF.Sin(MathF.PI / 3f), 1.6f, 5f * MathF.Cos(MathF.PI / 3f));
            Assert.IsFalse(GhostRules.IsWatched(ghost, new[] { LookingNorth(1) }, new GhostConfig(), Clear));
        }

        [Test]
        public void IsWatched_TooFar_IsNotWatched()
        {
            Assert.IsFalse(GhostRules.IsWatched(new Vector3(0f, 1.6f, 30f), new[] { LookingNorth(1) }, new GhostConfig(), Clear));
        }

        [Test]
        public void IsWatched_BlockedLineOfSight_IsNotWatched()
        {
            Assert.IsFalse(GhostRules.IsWatched(new Vector3(0f, 1.2f, 5f), new[] { LookingNorth(1) }, new GhostConfig(), (_, _) => false));
        }

        [Test]
        public void IsWatched_AnyOneViewerIsEnough()
        {
            var ghost = new Vector3(0f, 1.2f, 5f);
            var away = new Viewer(2, new Vector3(0f, 1.6f, 0f), -Vector3.UnitZ);
            Assert.IsTrue(GhostRules.IsWatched(ghost, new[] { away, LookingNorth(1), away }, new GhostConfig(), Clear));
            Assert.IsFalse(GhostRules.IsWatched(ghost, new[] { away, away }, new GhostConfig(), Clear));
            Assert.IsFalse(GhostRules.IsWatched(ghost, Array.Empty<Viewer>(), new GhostConfig(), Clear));
        }

        [Test]
        public void PickTarget_SkipsCapturedAndImmune()
        {
            var ghost = Vector3.Zero;
            var players = new List<Target>
            {
                new(1, new Vector3(1f, 0f, 0f), captured: true, immuneUntil: 0f),
                new(2, new Vector3(2f, 0f, 0f), captured: false, immuneUntil: 10f),
                new(3, new Vector3(6f, 0f, 0f), captured: false, immuneUntil: 0f),
                new(4, new Vector3(9f, 0f, 0f), captured: false, immuneUntil: 0f),
            };
            Assert.AreEqual(3UL, GhostRules.PickTarget(ghost, players, now: 5f)?.ClientId);
            Assert.AreEqual(2UL, GhostRules.PickTarget(ghost, players, now: 10.5f)?.ClientId);
            Assert.IsNull(GhostRules.PickTarget(ghost, new[] { players[0] }, now: 5f));
        }

        [Test]
        public void PickPrank_SkipsCarriedAndAccepted()
        {
            var onlyBlocked = new List<PrankCandidate>
            {
                new("a", "west", carried: true, roomAccepted: false),
                new("b", "west", carried: false, roomAccepted: true),
                new("c", "west", carried: false, roomAccepted: false),
                new("d", "east", carried: false, roomAccepted: false),
            };
            Assert.IsNull(GhostRules.PickPrank(onlyBlocked, "west", new Random(1)));
            Assert.IsNull(GhostRules.PickPush(onlyBlocked.GetRange(0, 2), "west", new Random(1)));

            var free = new List<PrankCandidate>(onlyBlocked) { new("e", "west", carried: false, roomAccepted: false) };
            for (var seed = 0; seed < 20; seed++)
            {
                var pair = GhostRules.PickPrank(free, "west", new Random(seed));
                Assert.IsNotNull(pair);
                CollectionAssert.AreEquivalent(new[] { "c", "e" }, new[] { pair.Value.A, pair.Value.B });
                Assert.That(GhostRules.PickPush(free, "west", new Random(seed)), Is.EqualTo("c").Or.EqualTo("e"));
            }
        }

        [Test]
        public void PickPrank_SameSeed_SamePair()
        {
            var items = new List<PrankCandidate>();
            for (var i = 0; i < 6; i++)
                items.Add(new PrankCandidate($"s{i}", "west", carried: false, roomAccepted: false));
            Assert.AreEqual(GhostRules.PickPrank(items, "west", new Random(5)), GhostRules.PickPrank(items, "west", new Random(5)));
        }

        [Test]
        public void Validate_FindsBadGhostConfig()
        {
            CollectionAssert.IsEmpty(new GhostConfig().Validate());
            CollectionAssert.IsNotEmpty(new GhostConfig { WatchHalfAngle = 0f }.Validate());
            CollectionAssert.IsNotEmpty(new GhostConfig { WatchHalfAngle = 90f }.Validate());
            CollectionAssert.IsNotEmpty(new GhostConfig { HuntSpeed = -1f }.Validate());
        }
    }
}
