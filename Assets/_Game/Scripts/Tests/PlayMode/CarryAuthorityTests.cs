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
        public IEnumerator DespawnedCarrier_ReleasesItem()
        {
            // A carrier whose player object goes away (respawn, knocked out) must not leave the item floating.
            Carryable box = null;
            yield return PickUp("LightBox0", b => box = b);
            Motor.NetworkObject.Despawn();
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(0, box.CarrierCount);
            Assert.IsTrue(box.GetComponent<Rigidbody>().useGravity || !box.IsOwner);
        }

        [UnityTest]
        public IEnumerator TryAddCarrier_ThroughWall_IsRejected()
        {
            // Box east of the divider wall, player 2 m away on the west side, looking at it through the wall.
            var box = GameObject.Find("LightBox0").GetComponent<Carryable>();
            MoveItem(box, new Vector3(1.0f, 0.2f, -4f));
            Motor.Teleport(new Vector3(-1.0f, 0f, -4f), 90f);
            yield return Wait(0.3f);
            Motor.LookAt(box.transform.position);
            yield return null;
            Assert.IsFalse(box.TryAddCarrier(NetworkManager.ServerClientId));
        }

        [UnityTest]
        public IEnumerator TryAddCarrier_BehindPlayer_IsRejected()
        {
            var box = GameObject.Find("LightBox0").GetComponent<Carryable>();
            var target = box.transform.position;
            Motor.Teleport(new Vector3(target.x, 0f, target.z + 1.2f), 0f); // facing +z, box behind
            yield return Wait(0.3f);
            Assert.IsFalse(box.TryAddCarrier(NetworkManager.ServerClientId));
        }

        [UnityTest]
        public IEnumerator TryAddCarrier_ReportedHeadingTowardItem_IsAccepted()
        {
            // The host's copy of a remote player's heading lags; a player who just turned to the box reports it.
            var box = GameObject.Find("LightBox0").GetComponent<Carryable>();
            var target = box.transform.position;
            Motor.Teleport(new Vector3(target.x, 0f, target.z + 1.2f), 0f); // host view: facing +z, box behind
            yield return Wait(0.3f);
            Assert.IsTrue(box.TryAddCarrier(NetworkManager.ServerClientId, Vector3.back));
            Assert.AreEqual(0f, Mathf.DeltaAngle(180f, box.transform.eulerAngles.y - box.YawOffset), 1f,
                "carried yaw follows the reported heading");
        }

        [UnityTest]
        public IEnumerator TryAddCarrier_ReportedHeadingAway_IsRejected()
        {
            var box = GameObject.Find("LightBox0").GetComponent<Carryable>();
            var target = box.transform.position;
            Motor.Teleport(new Vector3(target.x, 0f, target.z + 1.2f), 180f); // facing the box
            yield return Wait(0.3f);
            Assert.IsFalse(box.TryAddCarrier(NetworkManager.ServerClientId, Vector3.forward));
        }

        [UnityTest]
        public IEnumerator RepickBeforeThrowApplied_DoesNotFlingLater()
        {
            Carryable box = null;
            yield return PickUp("LightBox0", b => box = b);
            box.Throw(NetworkManager.ServerClientId, Motor.Forward);
            Assert.IsTrue(box.TryAddCarrier(NetworkManager.ServerClientId), "re-pick in the same frame");
            yield return Wait(0.5f);
            box.ReleaseCarrier(NetworkManager.ServerClientId);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            var velocity = box.GetComponent<Rigidbody>().linearVelocity;
            Assert.Less(new Vector2(velocity.x, velocity.z).magnitude, 2f);
        }

        [UnityTest]
        public IEnumerator OwnershipToUnknownClient_FallsBackToServer()
        {
            var box = GameObject.Find("LightBox0").GetComponent<Carryable>();
            box.SetSingleCarrierForTest(99);
            // Before any frame: the hold-error check would release client 99 anyway (M3C carry-over 4).
            Assert.AreEqual(NetworkManager.ServerClientId, box.OwnerClientId);
            yield break;
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
