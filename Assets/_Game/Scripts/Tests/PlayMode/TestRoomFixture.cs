using System.Collections;
using NUnit.Framework;
using SpiritMovers.Dev;
using SpiritMovers.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace SpiritMovers.Tests
{
    /// <summary>
    /// Loads the M3 test room, starts a local host (no Unity Gaming Services) and hands the local player
    /// a <see cref="ScriptedInput"/> so tests can drive it.
    /// </summary>
    public abstract class TestRoomFixture
    {
        protected virtual string ScenePath => "Assets/_Game/Scenes/M3_TestRoom.unity";

        protected PlayerMotor Motor { get; private set; }
        protected ScriptedInput Input { get; private set; }

        [UnitySetUp]
        public IEnumerator LoadRoomAndHost()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return BeforeHost();
            Object.FindAnyObjectByType<DevSessionRunner>().StartLocalHost();

            var deadline = Time.realtimeSinceStartup + 10f;
            while (PlayerMotor.Local == null)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "local player did not spawn");
                yield return null;
            }

            Motor = PlayerMotor.Local;
            Input = new ScriptedInput();
            Motor.Input = Input;
            yield return WaitForGrounded();
        }

        [UnityTearDown]
        public IEnumerator ShutDown()
        {
            var manager = NetworkManager.Singleton;
            if (manager != null)
            {
                manager.Shutdown();
                Object.Destroy(manager.gameObject);
            }
            yield return null;
            yield return null;
        }

        /// <summary>Runs after the scene loads and before the host starts.</summary>
        protected virtual IEnumerator BeforeHost()
        {
            yield break;
        }

        protected IEnumerator WaitForGrounded(float timeout = 3f)
        {
            var deadline = Time.realtimeSinceStartup + timeout;
            while (!Motor.IsGrounded && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        protected static float Planar(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}
