using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpiritMovers.Core.Job;
using SpiritMovers.Interaction;
using SpiritMovers.Job;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class BellTests : TestHouseFixture
    {
        private PlayerCarrier Carrier => Motor.GetComponent<PlayerCarrier>();
        private RoomTask FirstRoom => Director.Plan.Rooms[0];

        private GhostBell BellOf(string roomId) =>
            Object.FindObjectsByType<GhostBell>(FindObjectsSortMode.None).First(b => b.RoomId == roomId);

        private static IEnumerator Wait(float seconds)
        {
            var end = Time.time + seconds;
            while (Time.time < end)
                yield return null;
        }

        [UnityTest]
        public IEnumerator BellRing_ReportsCorrectCountOfThatRoom()
        {
            Director.BeginJob(3, JobDifficulty.Normal);
            yield return PutBack(FirstRoom.Slots[0].Slot.Id);
            yield return PutBack(FirstRoom.Slots[1].Slot.Id);
            var bell = BellOf(FirstRoom.RoomId);
            var nods = new List<int>();
            bell.Nodded += nods.Add;
            bell.Interact(Carrier);
            yield return Wait(0.3f);
            CollectionAssert.AreEqual(new[] { 2 }, nods);
            Assert.AreEqual(2, bell.LastNod);
        }

        [UnityTest]
        public IEnumerator BellRing_AddsBellAnger()
        {
            Director.BeginJob(3, JobDifficulty.Normal);
            var before = Director.AngerValue;
            BellOf(FirstRoom.RoomId).Interact(Carrier);
            yield return Wait(0.2f);
            // Bell = 3, solo anger multiplier 0.8 (Normal x1.0 x solo 0.8).
            Assert.AreEqual(before + 2.4f, Director.AngerValue, 0.2f);
        }

        [UnityTest]
        public IEnumerator BellWithinCooldown_IsIgnored()
        {
            Director.BeginJob(3, JobDifficulty.Normal);
            var bell = BellOf(FirstRoom.RoomId);
            var nods = 0;
            bell.Nodded += _ => nods++;
            var before = Director.AngerValue;
            for (var i = 0; i < 3; i++)
            {
                bell.Interact(Carrier);
                yield return Wait(0.3f);
            }
            Assert.AreEqual(1, nods);
            Assert.AreEqual(before + 2.4f, Director.AngerValue, 0.2f);
        }

        [UnityTest]
        public IEnumerator BellBeforeJobStarts_CannotInteract()
        {
            Assert.IsFalse(BellOf("west").CanInteract(Carrier));
            yield break;
        }
    }
}
