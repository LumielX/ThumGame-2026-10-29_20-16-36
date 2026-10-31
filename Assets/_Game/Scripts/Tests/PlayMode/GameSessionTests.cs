using System.Collections;
using NUnit.Framework;
using SpiritMovers.Player;
using SpiritMovers.Session;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class GameSessionTests : GameFlowFixture
    {
        [UnityTest]
        public IEnumerator Boot_LoadsMainMenu()
        {
            Assert.AreEqual(GameSession.MainMenuScene, ActiveScene);
            Assert.AreEqual(1, Object.FindObjectsByType<GameSession>(FindObjectsSortMode.None).Length);
            yield break;
        }

        [UnityTest]
        public IEnumerator StartSolo_LoadsLobbyAndSpawnsPlayer()
        {
            yield return SoloIntoLobby();
            Assert.IsTrue(Session.IsSolo);
            var spawn = GameObject.Find("PlayerSpawn0").transform.position;
            Assert.Less(Vector3.Distance(spawn, PlayerMotor.Local.Feet), 0.5f, "standing at the van's first spawn");
        }

        [UnityTest]
        public IEnumerator Leave_ReturnsToMainMenuWithCursorFree()
        {
            yield return SoloIntoLobby();
            yield return Await(Session.LeaveAsync());
            yield return WaitFor(() => ActiveScene == GameSession.MainMenuScene, 5f, "main menu");
            Assert.IsFalse(NetworkManager.Singleton.IsListening);
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
        }

        [UnityTest]
        public IEnumerator GameSession_SurvivesSceneChanges()
        {
            var first = Session;
            yield return SoloIntoLobby();
            Assert.AreSame(first, Session);
            yield return Await(Session.LeaveAsync());
            yield return WaitFor(() => ActiveScene == GameSession.MainMenuScene, 5f, "main menu");
            Assert.AreSame(first, Session);
        }
    }
}
