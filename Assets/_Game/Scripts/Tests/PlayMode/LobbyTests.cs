using System.Collections;
using System.Linq;
using NUnit.Framework;
using SpiritMovers.Core.Session;
using SpiritMovers.Player;
using SpiritMovers.Session;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class LobbyTests : GameFlowFixture
    {
        private static LobbyState Lobby => Object.FindAnyObjectByType<LobbyState>();
        private static ScriptedInput Input => (ScriptedInput)PlayerMotor.Local.Input;
        private static bool LocalReady => Lobby.Members.First(m => m.ClientId == NetworkManager.Singleton.LocalClientId).Ready;

        private static IEnumerator Press(System.Action<ScriptedInput> press)
        {
            press(Input);
            yield return null;
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Ready_TogglesForLocalPlayer()
        {
            yield return SoloIntoLobby();
            Assert.IsFalse(LocalReady);
            yield return Press(i => i.Next.ReadyPressed = true);
            Assert.IsTrue(LocalReady);
            yield return Press(i => i.Next.ReadyPressed = true);
            Assert.IsFalse(LocalReady);
        }

        [UnityTest]
        public IEnumerator Start_RequiresEveryoneReady()
        {
            yield return SoloIntoLobby();
            yield return Press(i => i.Next.ConfirmPressed = true);
            Assert.IsFalse(Lobby.IsCountingDown);
        }

        [UnityTest]
        public IEnumerator Countdown_CancelsWhenSomeoneUnreadies()
        {
            yield return SoloIntoLobby();
            yield return Press(i => i.Next.ReadyPressed = true);
            yield return Press(i => i.Next.ConfirmPressed = true);
            Assert.IsTrue(Lobby.IsCountingDown);
            yield return Press(i => i.Next.ReadyPressed = true);
            Assert.IsFalse(Lobby.IsCountingDown);
        }

        [UnityTest]
        public IEnumerator Countdown_LoadsJobScene()
        {
            yield return SoloIntoLobby();
            yield return Press(i => i.Next.ReadyPressed = true);
            yield return Press(i => i.Next.ConfirmPressed = true);
            yield return WaitFor(() => ActiveScene == GameSession.JobScene, 6f, "job scene");
            Assert.AreEqual(SessionPhase.Job, Session.Phase);
        }

        [UnityTest]
        public IEnumerator Gate_RefusesJoinDuringJob()
        {
            Assert.AreEqual(JoinRefusal.JobInProgress, LobbyGate.Decide(SessionPhase.Job, 1, true));
            Assert.AreEqual(JoinRefusal.None, LobbyGate.Decide(SessionPhase.Lobby, 1, true));
            Assert.AreEqual(JoinRefusal.Full, LobbyGate.Decide(SessionPhase.Lobby, SessionRules.MaxPlayers, true));
            Assert.AreEqual(SessionErrors.InJob, SessionErrors.KeyFor(JoinRefusal.JobInProgress));
            yield break;
        }

        [UnityTest]
        public IEnumerator Gate_RefusesOtherVersionsBeforeTheySync()
        {
            // TECHNICAL_DESIGN 7: the version also travels in the connection-approval payload, so a mismatched player is
            // refused before the host's scene is synced to it (final-pass finding: it landed in a dead van).
            var host = SpiritMovers.Core.GameVersion.Parse("0.0.1");
            Assert.IsTrue(LobbyGate.VersionMatches(System.Text.Encoding.UTF8.GetBytes("0.0.1"), host));
            Assert.IsFalse(LobbyGate.VersionMatches(System.Text.Encoding.UTF8.GetBytes("0.0.0"), host));
            Assert.IsFalse(LobbyGate.VersionMatches(System.Text.Encoding.UTF8.GetBytes("nonsense"), host));
            Assert.IsFalse(LobbyGate.VersionMatches(null, host));
            Assert.AreEqual(JoinRefusal.VersionMismatch, LobbyGate.Decide(SessionPhase.Lobby, 1, versionMatches: false));
            yield break;
        }

        [UnityTest]
        public IEnumerator Panel_ShowsExperimentalLabelForFive()
        {
            yield return SoloIntoLobby();
            var panel = Object.FindAnyObjectByType<LobbyPanel>();
            var five = Enumerable.Range(1, 5).Select(n => new LobbyMember((ulong)n, n % 2 == 0, n)).ToList();
            panel.Render(five, joinCode: "ABC123", solo: false);
            StringAssert.Contains(SessionUi.Text("lobby.experimental"), panel.LastText);
            panel.Render(five.Take(4).ToList(), joinCode: "ABC123", solo: false);
            StringAssert.DoesNotContain(SessionUi.Text("lobby.experimental"), panel.LastText);
        }
    }
}
