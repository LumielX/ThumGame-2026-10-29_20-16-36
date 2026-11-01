using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using SpiritMovers.Player;
using SpiritMovers.Save;
using UnityEngine.InputSystem;
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
        /// <summary>This test's own data folder: no test reads or writes the player's real save (M4B Review Focus 2).</summary>
        protected string DataDir { get; private set; }
        protected static string ActiveScene => SceneManager.GetActiveScene().name;

        [UnitySetUp]
        public IEnumerator Boot()
        {
            GameSession.PendingMessageKey = null;
            GameSession.PendingPay = 0;
            GameSession.PendingArgs = null;
            DataDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SpiritMoversTests", Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(DataDir);
            DataLocation.OverrideRoot = DataDir;
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
            GameSession.PendingArgs = null;
            GameData.Unload();
            // The input asset is shared by the whole run: no test leaves a changed key behind.
            SpiritMovers.Settings.SettingsApplier.Actions?.RemoveAllBindingOverrides();
            AudioListener.volume = 1f;
            DataLocation.OverrideRoot = null;
            if (DataDir != null && System.IO.Directory.Exists(DataDir))
                System.IO.Directory.Delete(DataDir, true);
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

        protected static ScriptedInput LocalInput => (ScriptedInput)PlayerMotor.Local.Input;

        protected static IEnumerator Press(System.Action<ScriptedInput> press)
        {
            press(LocalInput);
            yield return null;
            yield return null;
            yield return null;
        }

        /// <summary>Solo into the van, get ready, set off, and wait until the job is running.</summary>
        protected IEnumerator SoloIntoJob()
        {
            yield return SoloIntoLobby();
            yield return Press(i => i.Next.ReadyPressed = true);
            yield return Press(i => i.Next.ConfirmPressed = true);
            yield return WaitFor(() => ActiveScene == GameSession.JobScene, 8f, "job scene");
            yield return WaitFor(() => UnityEngine.Object.FindAnyObjectByType<SpiritMovers.Job.JobDirector>()?.Phase == SpiritMovers.Core.Job.JobPhase.Exploring,
                8f, "job started");
            yield return new WaitForFixedUpdate();
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
