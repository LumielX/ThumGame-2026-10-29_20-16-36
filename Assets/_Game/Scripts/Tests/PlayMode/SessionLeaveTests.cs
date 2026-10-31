using System.Collections;
using NUnit.Framework;
using SpiritMovers.Core.Session;
using SpiritMovers.Player;
using SpiritMovers.Session;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class SessionLeaveTests : GameFlowFixture
    {
        [UnityTest]
        public IEnumerator HostLeft_MessageMatchesPhase()
        {
            Assert.AreEqual("session.host_left_lobby", GameSession.MessageForHostLeft(SessionPhase.Lobby));
            Assert.AreEqual("session.host_left_lobby", GameSession.MessageForHostLeft(SessionPhase.Loading));
            Assert.AreEqual("session.host_left_job", GameSession.MessageForHostLeft(SessionPhase.Job));
            Assert.AreEqual("session.host_left_results", GameSession.MessageForHostLeft(SessionPhase.Results));
            yield break;
        }

        [UnityTest]
        public IEnumerator LeaveDuringJob_ReturnsToMenuCleanly()
        {
            yield return SoloIntoJob();
            yield return Press(i => i.Next.PausePressed = true);
            var pause = Object.FindAnyObjectByType<PauseMenu>();
            Assert.IsTrue(pause.IsOpen);
            Assert.IsFalse(PlayerMotor.Local.InputEnabled, "no walking while the menu is open");
            pause.ClickLeave();
            yield return WaitFor(() => ActiveScene == GameSession.MainMenuScene, 5f, "main menu");
            yield return null;
            Assert.IsFalse(NetworkManager.Singleton.IsListening);
            Assert.IsNull(PlayerMotor.Local);
            Assert.AreEqual(0, Object.FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None).Length);
            Assert.IsFalse(pause.IsOpen);
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
        }

        [UnityTest]
        public IEnumerator Pause_ResumeGivesControlBack()
        {
            yield return SoloIntoLobby();
            yield return Press(i => i.Next.PausePressed = true);
            var pause = Object.FindAnyObjectByType<PauseMenu>();
            pause.ClickResume();
            yield return null;
            Assert.IsFalse(pause.IsOpen);
            Assert.IsTrue(PlayerMotor.Local.InputEnabled);
        }

        [UnityTest]
        public IEnumerator PlayerLeavesLobby_CountdownCancels()
        {
            yield return SoloIntoLobby();
            yield return Press(i => i.Next.ReadyPressed = true);
            yield return Press(i => i.Next.ConfirmPressed = true);
            var lobby = Object.FindAnyObjectByType<LobbyState>();
            Assume.That(lobby.IsCountingDown);
            lobby.SimulateLeftForTest(NetworkManager.Singleton.LocalClientId);
            Assert.IsFalse(lobby.IsCountingDown);
        }
    }
}
