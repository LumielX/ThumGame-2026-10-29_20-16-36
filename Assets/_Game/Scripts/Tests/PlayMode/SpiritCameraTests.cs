using System.Collections;
using System.Linq;
using NUnit.Framework;
using SpiritMovers.Core.Ghost;
using SpiritMovers.Core.Job;
using SpiritMovers.Ghost;
using SpiritMovers.Interaction;
using SpiritMovers.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class SpiritCameraTests : TestHouseFixture
    {
        private static SpiritCamera Camera => Object.FindAnyObjectByType<SpiritCamera>();
        private static MemoryOverlay Overlay => Object.FindAnyObjectByType<MemoryOverlay>();

        private IEnumerator StartJobHoldingCamera()
        {
            Director.BeginJob(3, JobDifficulty.Normal);
            yield return WaitForGrounded();
            Motor.Teleport(new Vector3(-5f, 0f, 0f), 0f);
            Camera.GetComponent<Carryable>().SetSingleCarrierForTest(NetworkManager.ServerClientId);
            yield return null;
            yield return new WaitForFixedUpdate();
        }

        private IEnumerator Peek()
        {
            Input.Next.UsePressed = true;
            yield return null;
            yield return null;
            yield return null;
        }

        /// <summary>Planned slots whose item is not in place and whose place is within the overlay range of the eyes.</summary>
        private int MisplacedInRange()
        {
            var eye = Motor.Head.position;
            return Director.Plan.Rooms.SelectMany(r => r.Slots).Count(task =>
            {
                var item = House.Slot(task.Slot.Id).Item.transform;
                var actual = new Placement(House.RoomAt(item.position), item.position.ToNumerics(), item.rotation.ToNumerics());
                return !PlacementMath.IsCorrect(task.Slot.Target, actual, task.Slot.Tolerance) &&
                       Vector3.Distance(eye, task.Slot.Target.Position.ToUnity()) <= new ToolConfig().OverlayRange;
            });
        }

        [UnityTest]
        public IEnumerator BeginJob_SpawnsCamerasForPlayerCount()
        {
            Director.BeginJob(3, JobDifficulty.Normal);
            yield return null;
            var cameras = Object.FindObjectsByType<SpiritCamera>(FindObjectsSortMode.None);
            Assert.AreEqual(1, cameras.Length);
            Assert.AreEqual(6, cameras[0].Film);
        }

        [UnityTest]
        public IEnumerator Peek_ShowsMisplacedSlotsInRange()
        {
            yield return StartJobHoldingCamera();
            var expected = MisplacedInRange();
            Assume.That(expected, Is.GreaterThan(0));
            var anger = Director.AngerValue;
            yield return Peek();
            Assert.AreEqual(expected, Overlay.ActiveCount);
            Assert.AreEqual(5, Camera.Film);
            Assert.AreEqual(1, Camera.PeeksSeen);
            Assert.AreEqual(anger + 2f * JobSetup.AngerMultiplier(JobDifficulty.Normal, 1), Director.AngerValue, 0.1f);
        }

        [UnityTest]
        public IEnumerator Overlay_ClearsAfterThreeSeconds()
        {
            yield return StartJobHoldingCamera();
            yield return Peek();
            Assume.That(Overlay.ActiveCount, Is.GreaterThan(0));
            yield return new WaitForSeconds(3.5f);
            Assert.AreEqual(0, Overlay.ActiveCount);
        }

        [UnityTest]
        public IEnumerator Overlay_ClearsAfterDropMidPeek()
        {
            yield return StartJobHoldingCamera();
            yield return Peek();
            Assume.That(Overlay.ActiveCount, Is.GreaterThan(0));
            Camera.GetComponent<Carryable>().ReleaseCarrier(NetworkManager.ServerClientId);
            yield return new WaitForSeconds(3.5f);
            Assert.AreEqual(0, Overlay.ActiveCount);
        }

        [UnityTest]
        public IEnumerator Peek_WithNoFilm_DoesNothing()
        {
            yield return StartJobHoldingCamera();
            Camera.SetFilmForTest(0);
            var anger = Director.AngerValue;
            yield return Peek();
            Assert.AreEqual(0, Overlay.ActiveCount);
            Assert.AreEqual(0, Camera.Film);
            Assert.AreEqual(anger, Director.AngerValue, 0.05f);
        }

        [UnityTest]
        public IEnumerator Peek_NotHolder_IsRejected()
        {
            Director.BeginJob(3, JobDifficulty.Normal);
            yield return null;
            Assert.IsFalse(Camera.TryPeek(NetworkManager.ServerClientId, Motor.Head.position, Motor.Look));
            yield return null;
            Assert.AreEqual(0, Overlay.ActiveCount);
            Assert.AreEqual(6, Camera.Film);
        }

        [UnityTest]
        public IEnumerator Peek_AtCabinet_UnlocksIt()
        {
            Director.BeginJob(3, JobDifficulty.Normal);
            yield return WaitForGrounded();
            var cabinet = Object.FindObjectsByType<MemoryCabinet>(FindObjectsSortMode.None).First(c => c.RoomId == "west");
            Assert.IsTrue(cabinet.Capture(Motor.GetComponent<PlayerCapture>()));
            var eye = cabinet.FrontPoint + cabinet.transform.forward * 2f + Vector3.up * 1.6f;
            var look = (cabinet.transform.position + Vector3.up - eye).normalized;
            Camera.UnlockCabinetsInView(eye, look);
            Assert.IsTrue(cabinet.IsUnlocked, "a friend aimed the camera at the cabinet");
        }

        [UnityTest]
        public IEnumerator Overlay_MaterialIsSpiritMemoryShader()
        {
            yield return StartJobHoldingCamera();
            yield return Peek();
            var renderers = Overlay.GetComponentsInChildren<MeshRenderer>();
            Assume.That(renderers.Length, Is.GreaterThan(0));
            Assert.IsTrue(renderers.All(r => r.sharedMaterial != null && r.sharedMaterial.shader.name == "SpiritMovers/SpiritMemory"));
        }
    }
}
