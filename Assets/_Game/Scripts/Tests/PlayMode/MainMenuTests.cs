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
            /// <summary>The host's refusal reason arriving while the join call is still running (slow connections).</summary>
            public string RefusalDuringJoin;
            public int JoinCalls;
            public bool? Joinable;

            public Task InitializeAsync(string profile = null) => Task.CompletedTask;
            public Task LeaveAsync() => Task.CompletedTask;

            /// <summary>Hosts on this machine (no Relay) so the online host path runs without the internet.</summary>
            public Task<string> HostAsync(int maxPlayers, GameVersion version)
            {
                var network = Unity.Netcode.NetworkManager.Singleton;
                network.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>().SetConnectionData("127.0.0.1", 7777, "127.0.0.1");
                network.StartHost();
                return Task.FromResult("FAKE01");
            }

            public Task SetJoinableAsync(bool joinable)
            {
                Joinable = joinable;
                return Task.CompletedTask;
            }

            public Task JoinAsync(string code, GameVersion version)
            {
                JoinCalls++;
                if (RefusalDuringJoin != null)
                    GameSession.Instance.NoteRefusal(RefusalDuringJoin);
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
        public IEnumerator Join_RefusedByHostDuringJoin_ShowsHostsReason()
        {
            // Final-pass finding (150 ms run): the SDK's join throws when the host refuses, and the menu said
            // "check the code or internet" instead of the host's reason.
            var fake = UseFake();
            fake.RefusalDuringJoin = SessionErrors.InJob;
            Menu.ClickJoin("ABC123");
            yield return WaitFor(() => Menu.StatusKey != "menu.connecting" && Menu.StatusKey != null, 3f, "refusal");
            Assert.AreEqual(SessionErrors.InJob, Menu.StatusKey);
        }

        [UnityTest]
        public IEnumerator Join_RefusedForVersion_ShowsBothVersions()
        {
            var fake = UseFake();
            fake.RefusalDuringJoin = "error.version:0.0.9";
            Menu.ClickJoin("ABC123");
            yield return WaitFor(() => Menu.StatusKey == SessionErrors.Version, 3f, "version refusal");
            StringAssert.Contains("0.0.9", Menu.StatusText);
            StringAssert.Contains(Bootstrap.Version.ToString(), Menu.StatusText);
        }

        [UnityTest]
        public IEnumerator FailureAwayFromMenu_GoesBackWithMessage()
        {
            // Final-pass finding: a join that failed after the host's scene had synced left the player in a dead van.
            Session.StartSolo();
            yield return WaitFor(() => ActiveScene == GameSession.LobbyScene, 10f, "van");
            Session.FailForTest(new VersionMismatchException(GameVersion.Parse("0.0.1"), "0.0.2"));
            yield return WaitFor(() => ActiveScene == GameSession.MainMenuScene && Menu != null && Menu.StatusKey != null, 5f, "menu");
            Assert.AreEqual(SessionErrors.Version, Menu.StatusKey);
            StringAssert.Contains("0.0.2", Menu.StatusText);
        }

        [UnityTest]
        public IEnumerator ServiceRefusals_MapToClearMessages()
        {
            // Final-pass finding (150 ms run): the host's NGO refusal can lose the race with the SDK, so the room is also
            // locked at the session level while a job runs and the SDK's refusals get their own messages.
            Assert.AreEqual(SessionErrors.InJob, SessionErrors.KeyFor(new Unity.Services.Multiplayer.SessionException(
                "Failed to join session: session is locked", Unity.Services.Multiplayer.SessionError.Unknown, null)));
            Assert.AreEqual(SessionErrors.Full, SessionErrors.KeyFor(new Unity.Services.Multiplayer.SessionException(
                "Failed to join session: session is full", Unity.Services.Multiplayer.SessionError.Unknown, null)));
            Assert.AreEqual(SessionErrors.Code, SessionErrors.KeyFor(new Unity.Services.Multiplayer.SessionException(
                "not found", Unity.Services.Multiplayer.SessionError.SessionNotFound, null)));
            Assert.AreEqual(SessionErrors.Join, SessionErrors.KeyFor(new InvalidOperationException("relay down")));
            yield break;
        }

        [UnityTest]
        public IEnumerator HostedRoom_LockedWhileOnAJob()
        {
            var fake = UseFake();
            Menu.ClickHost();
            yield return WaitFor(() => ActiveScene == GameSession.LobbyScene && SpiritMovers.Player.PlayerMotor.Local != null, 10f, "van");
            Assert.AreNotEqual(false, fake.Joinable, "open while in the van");
            yield return Press(i => i.Next.ReadyPressed = true);
            yield return Press(i => i.Next.ConfirmPressed = true);
            yield return WaitFor(() => ActiveScene == GameSession.JobScene, 8f, "job");
            yield return WaitFor(() => fake.Joinable == false, 3f, "locked during the job");
            UnityEngine.Object.FindAnyObjectByType<SpiritMovers.Job.JobDirector>().LeaveEarly();
            yield return Press(i => i.Next.ConfirmPressed = true);
            yield return WaitFor(() => ActiveScene == GameSession.LobbyScene, 5f, "van again");
            yield return WaitFor(() => fake.Joinable == true, 3f, "open again");
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
