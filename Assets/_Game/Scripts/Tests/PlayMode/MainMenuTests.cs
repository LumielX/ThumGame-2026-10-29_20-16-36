using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using SpiritMovers.Core;
using SpiritMovers.Net;
using SpiritMovers.Session;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class MainMenuTests : GameFlowFixture
    {
        /// <summary>Stands in for Unity Gaming Services: join either fails, hangs, or is never reached.</summary>
        private sealed class FakeService : ISessionService
        {
            public Exception JoinFailure;
            public TaskCompletionSource<bool> JoinHang;
            public int JoinCalls;

            public Task InitializeAsync(string profile = null) => Task.CompletedTask;
            public Task<string> HostAsync(int maxPlayers, GameVersion version) => throw new InvalidOperationException("not used");
            public Task LeaveAsync() => Task.CompletedTask;

            public Task JoinAsync(string code, GameVersion version)
            {
                JoinCalls++;
                if (JoinHang != null)
                    return JoinHang.Task;
                return Task.FromException(JoinFailure ?? new InvalidOperationException("relay down"));
            }
        }

        private static MainMenuPanel Menu => UnityEngine.Object.FindAnyObjectByType<MainMenuPanel>();

        private FakeService UseFake()
        {
            var fake = new FakeService();
            Session.ServiceOverride = fake;
            return fake;
        }

        [UnityTest]
        public IEnumerator Join_EmptyCode_ShowsErrorWithoutConnecting()
        {
            var fake = UseFake();
            Menu.ClickJoin("   ");
            yield return null;
            Assert.AreEqual(SessionErrors.Code, Menu.StatusKey);
            Assert.AreEqual(0, fake.JoinCalls);
            Assert.IsTrue(Menu.ButtonsInteractable);
        }

        [UnityTest]
        public IEnumerator Join_ServiceFailure_ShowsErrorAndReenablesButtons()
        {
            UseFake();
            Menu.ClickJoin("abc123");
            yield return WaitFor(() => Menu.StatusKey == SessionErrors.Join, 3f, "join error");
            Assert.IsTrue(Menu.ButtonsInteractable);
        }

        [UnityTest]
        public IEnumerator Join_VersionMismatch_ShowsBothVersions()
        {
            var fake = UseFake();
            fake.JoinFailure = new VersionMismatchException(GameVersion.Parse("0.0.1"), "0.0.2");
            Menu.ClickJoin("ABC123");
            yield return WaitFor(() => Menu.StatusKey == SessionErrors.Version, 3f, "version error");
            StringAssert.Contains("0.0.1", Menu.StatusText);
            StringAssert.Contains("0.0.2", Menu.StatusText);
        }

        [UnityTest]
        public IEnumerator Connecting_DisablesButtons()
        {
            var fake = UseFake();
            fake.JoinHang = new TaskCompletionSource<bool>();
            Menu.ClickJoin("ABC123");
            yield return null;
            Assert.IsFalse(Menu.ButtonsInteractable);
            Assert.AreEqual("menu.connecting", Menu.StatusKey);
            fake.JoinHang.SetException(new InvalidOperationException("gave up"));
            yield return WaitFor(() => Menu.ButtonsInteractable, 3f, "buttons back");
        }

        [UnityTest]
        public IEnumerator Solo_GoesToLobby()
        {
            Menu.ClickSolo();
            yield return WaitFor(() => ActiveScene == GameSession.LobbyScene, 10f, "lobby");
        }

        [UnityTest]
        public IEnumerator PendingMessage_ShownOnceOnMenu()
        {
            GameSession.PendingMessageKey = "session.host_left_job";
            GameSession.PendingPay = 50;
            SceneManager.LoadScene(GameSession.MainMenuScene);
            yield return WaitFor(() => Menu != null && Menu.StatusKey == "session.host_left_job", 5f, "message");
            StringAssert.Contains("50", Menu.StatusText);
            SceneManager.LoadScene(GameSession.MainMenuScene);
            yield return null;
            yield return null;
            Assert.AreNotEqual("session.host_left_job", Menu.StatusKey);
        }
    }
}
