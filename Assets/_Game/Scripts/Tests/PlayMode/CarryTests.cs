using System.Collections;
using NUnit.Framework;
using SpiritMovers.Interaction;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class CarryTests : TestRoomFixture
    {
        private PlayerCarrier Carrier => Motor.GetComponent<PlayerCarrier>();
        private PlayerInteractor Interactor => Motor.GetComponent<PlayerInteractor>();

        private static Carryable Item(string name) => GameObject.Find(name).GetComponent<Carryable>();

        private static IEnumerator Wait(float seconds)
        {
            var end = Time.time + seconds;
            while (Time.time < end)
                yield return null;
        }

        /// <summary>Stand <paramref name="distance"/> m south of the item, looking at it.</summary>
        private IEnumerator Approach(Carryable item, float distance = 1.3f)
        {
            var target = item.transform.position;
            Motor.Teleport(new Vector3(target.x, 0f, target.z - distance), 0f);
            yield return WaitForGrounded();
            Motor.LookAt(target);
            yield return Wait(0.1f);
        }

        private IEnumerator PickUp(Carryable item)
        {
            yield return Approach(item);
            Input.Next.InteractPressed = true;
            yield return Wait(0.5f);
        }

        private void LookLevel() => Motor.LookAt(Motor.Head.position + Motor.Forward * 10f);

        [UnityTest]
        public IEnumerator PickUpLightBox_FollowsHoldPoint()
        {
            var box = Item("LightBox0");
            yield return PickUp(box);
            CollectionAssert.AreEqual(new[] { NetworkManager.ServerClientId }, box.Carriers);
            Assert.Less(Vector3.Distance(box.transform.position, Carrier.HoldPoint), 0.4f);
        }

        [UnityTest]
        public IEnumerator Drop_ReleasesAndFalls()
        {
            var box = Item("LightBox0");
            yield return PickUp(box);
            var heldY = box.transform.position.y;
            Input.Next.InteractPressed = true;
            yield return Wait(1f);
            Assert.AreEqual(0, box.CarrierCount);
            Assert.GreaterOrEqual(heldY - box.transform.position.y, 0.5f);
        }

        [UnityTest]
        public IEnumerator RotateStep_TurnsItem15Degrees()
        {
            var box = Item("LightBox0");
            yield return PickUp(box);
            var before = box.transform.eulerAngles.y;
            Input.Next.RotateSteps = 1f;
            yield return Wait(0.5f);
            Assert.AreEqual(15f, Mathf.DeltaAngle(before, box.transform.eulerAngles.y), 3f);
        }

        [UnityTest]
        public IEnumerator ThrowLight_FliesForward()
        {
            var box = Item("LightBox0");
            yield return PickUp(box);
            LookLevel();
            yield return Wait(0.2f);
            Input.Next.ThrowPressed = true;
            yield return Wait(1f);
            Assert.AreEqual(0, box.CarrierCount);
            Assert.GreaterOrEqual(Planar(Motor.Feet, box.transform.position), 3f);
        }

        [UnityTest]
        public IEnumerator HeavyCrate_CannotThrowAndSlowsPlayer()
        {
            var crate = Item("HeavyCrate0");
            yield return PickUp(crate);
            LookLevel();
            Input.Next.ThrowPressed = true;
            yield return Wait(0.3f);
            Assert.AreEqual(1, crate.CarrierCount);

            Input.Next.Move = new Vector2(0f, 1f);
            yield return Wait(1f);
            Assert.AreEqual(1.8f, Motor.LastMove.Speed, 0.01f);
        }

        [UnityTest]
        public IEnumerator InteractOutOfReach_DoesNothing()
        {
            var box = Item("LightBox0");
            yield return Approach(box, 4f);
            Assert.IsNull(Interactor.Focus);
            Input.Next.InteractPressed = true;
            yield return Wait(0.3f);
            Assert.AreEqual(0, box.CarrierCount);
        }

        [UnityTest]
        public IEnumerator TryAddCarrier_LightItemSecondCarrier_IsRejected()
        {
            var box = Item("LightBox0");
            yield return Approach(box);
            Assert.IsTrue(box.TryAddCarrier(NetworkManager.ServerClientId));
            Assert.IsFalse(box.TryAddCarrier(99));
            Assert.AreEqual(1, box.CarrierCount);
        }

        [UnityTest]
        public IEnumerator InputDisabledWhileCarrying_KeepsItem()
        {
            var box = Item("LightBox0");
            yield return PickUp(box);
            Motor.InputEnabled = false;
            Input.Next.InteractPressed = true;
            yield return Wait(1f);
            Assert.AreEqual(1, box.CarrierCount);
            Assert.Less(Vector3.Distance(box.transform.position, Carrier.HoldPoint), 0.4f);
        }
    }
}
