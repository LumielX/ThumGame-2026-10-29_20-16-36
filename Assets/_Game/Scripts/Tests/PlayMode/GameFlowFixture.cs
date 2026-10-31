using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using SpiritMovers.Player;
using SpiritMovers.Session;
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
    /// Starts the real game from the Boot scene (the production flow: Boot → MainMenu → Lobby → Job_Greybox) and
    /// tears the persistent <see cref="GameSession"/> down afterwards so other fixtures get a clean NetworkManager.
    /// </summary>
    public abstract class GameFlowFixture
    {
        protected GameSession Session => GameSession.Instance;
        protected static string ActiveScene => SceneManager.GetActiveScene().name;

        [UnitySetUp]
        public IEnumerator Boot()
        {
            GameSession.PendingMessageKey = null;
            GameSession.PendingPay = 0;
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Game/Scenes/Boot.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return WaitFor(() => ActiveScene == GameSession.MainMenuScene && GameSession.Instance != null, 5f, "main menu");
        }

        [UnityTearDown]
        public IEnumerator ShutDown()
        {
            if (GameSession.Instance != null)
            {
                var root = GameSession.Instance.gameObject;
                root.GetComponent<NetworkManager>()?.Shutdown();
                UnityEngine.Object.Destroy(root);
            }
            GameSession.PendingMessageKey = null;
            GameSession.PendingPay = 0;
            yield return null;
            yield return null;
        }

        protected static IEnumerator WaitFor(Func<bool> condition, float timeout, string what)
        {
            var deadline = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, $"timed out waiting for {what}");
                yield return null;
            }
        }

        protected static IEnumerator Await(Task task, float timeout = 10f)
        {
            yield return WaitFor(() => task.IsCompleted, timeout, "task");
            if (task.IsFaulted)
                throw task.Exception!;
        }

        /// <summary>Solo into the van and hand the local player a scripted input.</summary>
        protected IEnumerator SoloIntoLobby()
        {
            Session.StartSolo();
            yield return WaitFor(() => ActiveScene == GameSession.LobbyScene && PlayerMotor.Local != null, 10f, "lobby");
            yield return null;
            yield return new WaitForFixedUpdate();
        }
    }
}
