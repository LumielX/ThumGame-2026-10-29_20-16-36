using System.Collections;
using NUnit.Framework;
using SpiritMovers.Interaction;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class CarryAuthorityTests : TestRoomFixture
    {
        private PlayerCarrier Carrier => Motor.GetComponent<PlayerCarrier>();

        private static IEnumerator Wait(float seconds)
        {
            var end = Time.time + seconds;
            while (Time.time < end)
                yield return null;
        }

        private IEnumerator PickUp(string name, System.Action<Carryable> got)
        {
            var item = GameObject.Find(name).GetComponent<Carryable>();
            var target = item.transform.position;
            Motor.Teleport(new Vector3(target.x, 0f, target.z - 1.3f), 0f);
            yield return WaitForGrounded();
            Motor.LookAt(target);
            yield return Wait(0.1f);
            Input.Next.InteractPressed = true;
            yield return Wait(0.5f);
            Assert.AreEqual(1, item.CarrierCount, "pick-up failed");
            got(item);
        }

        private static void MoveItem(Carryable item, Vector3 position)
        {
            var body = item.GetComponent<Rigidbody>();
            body.position = position;
            item.transform.position = position;
        }

        [UnityTest]
        public IEnumerator ReleaseCarrier_ClearsCarrierAndReturnsOwnership()
        {
            Carryable box = null;
            yield return PickUp("LightBox0", b => box = b);
            box.ReleaseCarrier(NetworkManager.ServerClientId);
            yield return null;
            Assert.AreEqual(0, box.CarrierCount);
            Assert.AreEqual(NetworkManager.ServerClientId, box.OwnerClientId);
            Assert.IsTrue(box.IsSpawned);
            Assert.IsNull(Carrier.Held);
        }

        [UnityTest]
        public IEnumerator HeldItemFarFromHand_IsForceDropped()
        {
            Carryable box = null;
            yield return PickUp("LightBox0", b => box = b);
            var dropped = false;
            void OnDropped(Carryable item, ulong _) => dropped |= item == box;
            Carryable.ForceDropped += OnDropped;
            try
            {
                box.FollowEnabled = false;
                MoveItem(box, Carrier.HoldPoint + Vector3.right * 5f);
                yield return Wait(1f);
                Assert.AreEqual(0, box.CarrierCount);
                Assert.IsTrue(dropped);
            }
            finally
            {
                Carryable.ForceDropped -= OnDropped;
            }
        }

        [UnityTest]
        public IEnumerator ShortGlitchFarFromHand_DoesNotDrop()
        {
            Carryable box = null;
            yield return PickUp("LightBox0", b => box = b);
            box.FollowEnabled = false;
            MoveItem(box, Carrier.HoldPoint + Vector3.right * 3f);
            yield return Wait(0.2f);
            MoveItem(box, Carrier.HoldPoint);
            box.FollowEnabled = true;
            yield return Wait(0.6f);
            Assert.AreEqual(1, box.CarrierCount);
        }

        [UnityTest]
        public IEnumerator DisconnectCallback_ReleasesThatClientOnly()
        {
            Carryable box = null;
            yield return PickUp("LightBox0", b => box = b);
            box.OnClientDisconnected(99);
            Assert.AreEqual(1, box.CarrierCount);
            box.OnClientDisconnected(NetworkManager.ServerClientId);
            Assert.AreEqual(0, box.CarrierCount);
        }
    }
}
