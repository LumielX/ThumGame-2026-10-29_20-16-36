using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using SpiritMovers.Core.Job;

namespace SpiritMovers.Tests
{
    public class JobSetupTests
    {
        private static HouseLayout House(float slotX = 0f, bool swapRooms = false)
        {
            RoomLayout Room(string id, float x) => new(id, new[]
            {
                new SlotDefinition($"{id}-s0", $"{id}-item", new Placement(id, new Vector3(x, 0f, 0f), Quaternion.Identity),
                    Tolerance.Default, new[] { ProblemType.Moved }),
            });
            var rooms = new List<RoomLayout> { Room("a", slotX), Room("b", 5f) };
            if (swapRooms)
                rooms.Reverse();
            return new HouseLayout("h", rooms, new[] { new Placement("a", new Vector3(9f, 0f, 9f), Quaternion.Identity) },
                new[] { new Placement("b", new Vector3(-9f, 0f, -9f), Quaternion.Identity) });
        }

        [Test]
        public void AngerMultiplier_CombinesDifficultyAndPlayers()
        {
            Assert.AreEqual(1.54f, JobSetup.AngerMultiplier(JobDifficulty.Hard, 4), 1e-4f);
            var anger = JobSetup.CreateAnger(AngerConfig.Default, JobDifficulty.Hard, 4);
            anger.Apply(AngerEvent.ItemBroken);
            Assert.AreEqual(8f * 1.54f, anger.Value, 1e-3f);
        }

        [Test]
        public void Validate_FindsBadRanges()
        {
            var bad = JobDifficulty.Normal;
            bad.MinRooms = 5;
            bad.MaxRooms = 3;
            Assert.IsNotEmpty(bad.Validate());
            Assert.IsEmpty(JobDifficulty.Normal.Validate());
            Assert.IsEmpty(JobDifficulty.Hard.Validate());
        }

        [Test]
        public void AngerValidate_FindsUnreachableEviction()
        {
            Assert.IsNotEmpty(new AngerConfig { Max = 90f }.Validate());
            Assert.IsNotEmpty(new AngerConfig { StageThresholds = new[] { 50f, 25f, 75f, 100f } }.Validate());
            Assert.IsEmpty(AngerConfig.Default.Validate());
        }

        [Test]
        public void ContentHash_IsStableAndOrderSensitive()
        {
            Assert.AreEqual(House().ContentHash(), House().ContentHash());
            Assert.AreNotEqual(House().ContentHash(), House(swapRooms: true).ContentHash());
        }

        [Test]
        public void ContentHash_ChangesWhenLayoutChanges()
        {
            Assert.AreNotEqual(House().ContentHash(), House(slotX: 0.01f).ContentHash());
        }

        [Test]
        public void Generate_SetsLayoutHash()
        {
            var house = House();
            Assert.AreEqual(house.ContentHash(), JobGenerator.Generate(1, house, JobDifficulty.Normal, 2).LayoutHash);
        }
    }
}
