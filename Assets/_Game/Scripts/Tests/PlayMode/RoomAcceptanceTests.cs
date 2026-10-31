using System.Collections;
using System.Linq;
using NUnit.Framework;
using SpiritMovers.Core.Job;
using SpiritMovers.Job;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class RoomAcceptanceTests : TestHouseFixture
    {
        private RoomTask FirstRoom => Director.Plan.Rooms[0];

        private static IEnumerator Wait(float seconds)
        {
            var end = Time.time + seconds;
            while (Time.time < end)
                yield return null;
        }

        [UnityTest]
        public IEnumerator Room_NeedsToStaySettled()
        {
            // M3C carry-over 2: one still snapshot is not enough (an item may be tipping slowly).
            Director.BeginJob(3, JobDifficulty.Normal);
            foreach (var task in FirstRoom.Slots)
                Director.PlaceAtTarget(task.Slot.Id);
            Director.EvaluateNow(FirstRoom.RoomId);
            CollectionAssert.DoesNotContain(Director.AcceptedRooms, FirstRoom.RoomId, "accepted on the first still look");
            yield return WaitAccepted(FirstRoom.RoomId, 1.5f);
            CollectionAssert.Contains(Director.AcceptedRooms, FirstRoom.RoomId);
        }

        private IEnumerator WaitAccepted(string roomId, float timeout)
        {
            var end = Time.time + timeout;
            while (!Director.AcceptedRooms.Contains(roomId) && Time.time < end)
                yield return null;
        }

        [UnityTest]
        public IEnumerator PuttingRoomItemsBack_AcceptsRoom()
        {
            Director.BeginJob(3, JobDifficulty.Normal);
            foreach (var task in FirstRoom.Slots)
                yield return PutBack(task.Slot.Id);
            yield return WaitAccepted(FirstRoom.RoomId, 1.5f);
            CollectionAssert.Contains(Director.AcceptedRooms, FirstRoom.RoomId);
        }

        [UnityTest]
        public IEnumerator AcceptedRoom_LowersAngerAndRestoresColour()
        {
            Director.BeginJob(3, JobDifficulty.Normal);
            Director.ApplyAngerForTest(30f);
            var before = Director.AngerValue;
            foreach (var task in FirstRoom.Slots)
                yield return PutBack(task.Slot.Id);
            yield return WaitAccepted(FirstRoom.RoomId, 1.5f);
            Assert.LessOrEqual(Director.AngerValue, before - 19f);

            var restoration = House.Rooms.First(r => r.RoomId == FirstRoom.RoomId).GetComponent<RoomRestoration>();
            yield return Wait(3f);
            Assert.AreEqual(1f, restoration.Value, 1e-3f);
        }

        [UnityTest]
        public IEnumerator CarriedItemAtTarget_NotAcceptedUntilPutDown()
        {
            Director.BeginJob(3, JobDifficulty.Normal);
            var slots = FirstRoom.Slots;
            for (var i = 0; i < slots.Count - 1; i++)
                yield return PutBack(slots[i].Slot.Id);

            var last = House.Slot(slots[slots.Count - 1].Slot.Id).Item;
            last.SetSingleCarrierForTest(NetworkManager.ServerClientId);
            last.FollowEnabled = false;
            // Hold it exactly at its place (PlaceAtTarget would make the carrier let go first).
            var marker = House.Slot(slots[slots.Count - 1].Slot.Id).transform;
            var body = last.GetComponent<Rigidbody>();
            body.linearVelocity = Vector3.zero;
            body.position = marker.position;
            body.rotation = marker.rotation;
            yield return Wait(1.5f);
            Assume.That(last.IsCarried, "still in hand");
            CollectionAssert.DoesNotContain(Director.AcceptedRooms, FirstRoom.RoomId, "accepted while still in a player's hands");

            last.ReleaseCarrier(NetworkManager.ServerClientId);
            last.FollowEnabled = true;
            yield return PutBack(slots[slots.Count - 1].Slot.Id);
            yield return WaitAccepted(FirstRoom.RoomId, 1.5f);
            CollectionAssert.Contains(Director.AcceptedRooms, FirstRoom.RoomId);
        }

        [UnityTest]
        public IEnumerator AcceptedRoom_StaysAcceptedWhenItemMovedAway()
        {
            Director.BeginJob(3, JobDifficulty.Normal);
            foreach (var task in FirstRoom.Slots)
                yield return PutBack(task.Slot.Id);
            yield return WaitAccepted(FirstRoom.RoomId, 1.5f);

            var body = House.Slot(FirstRoom.Slots[0].Slot.Id).Item.GetComponent<Rigidbody>();
            body.position += Vector3.right * 3f;
            yield return Wait(1.5f);
            CollectionAssert.Contains(Director.AcceptedRooms, FirstRoom.RoomId);
            Assert.AreEqual(1, Director.AcceptedRooms.Count);
        }

        [UnityTest]
        public IEnumerator EvaluateUnknownRoom_DoesNothing()
        {
            Director.BeginJob(3, JobDifficulty.Normal);
            Assert.AreEqual(-1, Director.EvaluateNow("garage"));
            Assert.AreEqual(0, Director.AcceptedRooms.Count);
            yield break;
        }
    }
}
