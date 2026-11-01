using System.Collections;
using System.IO;
using NUnit.Framework;
using SpiritMovers.Core.Save;
using SpiritMovers.Core.Session;
using SpiritMovers.Job;
using SpiritMovers.Save;
using SpiritMovers.Session;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class GameDataTests : GameFlowFixture
    {
        private static JobDirector Director => Object.FindAnyObjectByType<JobDirector>();
        private string SaveFile => Path.Combine(DataDir, "save_v1.json");

        private IEnumerator AcceptOneRoomAndLeave()
        {
            Director.AcceptRoomForTest(0);
            yield return null;
            Director.LeaveEarly();
            yield return WaitFor(() => Director.Result.IsSet, 3f, "results");
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator DataLocation_TestsUseTheirOwnFolder()
        {
            StringAssert.StartsWith(DataDir, DataLocation.Root);
            Assert.AreNotEqual(Application.persistentDataPath, DataLocation.Root);
            Assert.IsTrue(GameData.IsLoaded, "Boot loads the data");
            yield break;
        }

        [UnityTest]
        public IEnumerator DataLocation_DevProfileGetsSubfolder()
        {
            Assert.AreEqual("base", DataLocation.RootFor("base", null));
            Assert.AreEqual(Path.Combine("base", "profiles", "m4ahost"), DataLocation.RootFor("base", "m4ahost"));
            Assert.AreEqual(Path.Combine("base", "profiles", "x"), DataLocation.RootFor("base", "../x"), "no climbing out");
            yield break;
        }

        [UnityTest]
        public IEnumerator Pay_ResultsCreditOnce()
        {
            yield return SoloIntoJob();
            var before = GameData.Money;
            yield return AcceptOneRoomAndLeave();
            var pay = Director.Result.PerPlayer;
            Assert.Greater(pay, 0);
            Assert.AreEqual(before + pay, GameData.Money);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(before + pay, GameData.Money, "only once");
            Assert.AreEqual(before + pay, JsonUtility.FromJson<SaveData>(File.ReadAllText(SaveFile)).money, "saved to disk");
        }

        [UnityTest]
        public IEnumerator Pay_HostLeftDuringJobCreditsShare()
        {
            yield return SoloIntoJob();
            var before = GameData.Money;
            Director.AcceptRoomForTest(0);
            yield return null;
            var share = GameSession.PayForHostLeft(SessionPhase.Job);
            Assert.Greater(share, 0);
            NetworkManager.Singleton.Shutdown();
            yield return WaitFor(() => ActiveScene == GameSession.MainMenuScene, 5f, "main menu");
            Assert.AreEqual(before + share, GameData.Money);
        }

        [UnityTest]
        public IEnumerator Pay_LostDuringResultsNotTwice()
        {
            yield return SoloIntoJob();
            var before = GameData.Money;
            yield return AcceptOneRoomAndLeave();
            var pay = Director.Result.PerPlayer;
            NetworkManager.Singleton.Shutdown();
            yield return WaitFor(() => ActiveScene == GameSession.MainMenuScene, 5f, "main menu");
            Assert.AreEqual(before + pay, GameData.Money);
        }

        [UnityTest]
        public IEnumerator Pay_SecondJobCreditsAgain()
        {
            yield return SoloIntoJob();
            var before = GameData.Money;
            yield return AcceptOneRoomAndLeave();
            var first = Director.Result.PerPlayer;
            yield return Press(i => i.Next.ConfirmPressed = true);
            yield return WaitFor(() => ActiveScene == GameSession.LobbyScene, 3f, "van");
            yield return WaitFor(() => Object.FindAnyObjectByType<LobbyState>()?.Members.Count == 1, 3f, "lobby members");
            yield return Press(i => i.Next.ReadyPressed = true);
            yield return Press(i => i.Next.ConfirmPressed = true);
            yield return WaitFor(() => ActiveScene == GameSession.JobScene && Director?.Phase == Core.Job.JobPhase.Exploring, 10f, "second job");
            yield return AcceptOneRoomAndLeave();
            Assert.AreEqual(before + first + Director.Result.PerPlayer, GameData.Money);
        }

        [UnityTest]
        public IEnumerator Menu_ShowsMoneyAndSaveMessage()
        {
            File.WriteAllText(SaveFile, "{\"schema\":1,\"money\":");
            File.WriteAllText(SaveFile + ".bak", JsonUtility.ToJson(new SaveData { money = 321, playerName = "บีม" }));
            GameData.Load();
            SceneManager.LoadScene(GameSession.MainMenuScene);
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuPanel>();
            yield return WaitFor(() => menu != null && menu.StatusKey != null, 3f, "menu message");
            Assert.AreEqual("save.restored", menu.StatusKey);
            StringAssert.Contains("321", menu.MoneyText);
        }

        [UnityTest]
        public IEnumerator Restart_KeepsMoney()
        {
            GameData.CreditPay(40, new object());
            GameData.Load();
            Assert.AreEqual(40, GameData.Money);
            yield break;
        }
    }
}
