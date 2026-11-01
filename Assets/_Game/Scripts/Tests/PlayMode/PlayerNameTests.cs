using System.Collections;
using System.Text;
using NUnit.Framework;
using SpiritMovers.Core.Save;
using SpiritMovers.Core.Session;
using SpiritMovers.Core.Text;
using SpiritMovers.Save;
using SpiritMovers.Session;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class PlayerNameTests : GameFlowFixture
    {
        private static MainMenuPanel Menu => Object.FindAnyObjectByType<MainMenuPanel>();

        private static void Type(string text)
        {
            var field = Menu.NameField;
            field.text = ThaiShaper.Shape(text);
            field.onEndEdit.Invoke(field.text);
        }

        [UnityTest]
        public IEnumerator Name_TypedInMenuIsSavedAndShownInVan()
        {
            Type("  ป้า  แจ่ม ");
            yield return null;
            Assert.AreEqual("ป้า แจ่ม", ThaiShaper.Unshape(Menu.NameField.text));
            Assert.AreEqual("ป้า แจ่ม", GameData.PlayerName);
            var saved = JsonUtility.FromJson<SaveData>(System.IO.File.ReadAllText(System.IO.Path.Combine(DataDir, "save_v1.json")));
            Assert.AreEqual("ป้า แจ่ม", saved.playerName);

            yield return SoloIntoLobby();
            var board = Object.FindAnyObjectByType<LobbyPanel>();
            yield return WaitFor(() => ThaiShaper.Unshape(board.LastText).Contains("ป้า แจ่ม"), 3f, "name on the van board");
        }

        [UnityTest]
        public IEnumerator Name_EmptyUsesDefault()
        {
            Type("   ");
            yield return null;
            var first = GameData.PlayerName;
            StringAssert.IsMatch(@"^พนักงาน \d{4}$", first);
            Assert.AreEqual(first, ThaiShaper.Unshape(Menu.NameField.text));
            Type("ชื่อจริง");
            Type("");
            Assert.AreEqual(first, GameData.PlayerName, "the same default every time");
        }

        [UnityTest]
        public IEnumerator Gate_NamesTheJoiningClient()
        {
            yield return SoloIntoLobby();
            var gate = Session.GetComponent<LobbyGate>();
            var response = new NetworkManager.ConnectionApprovalResponse();
            gate.Approve(new NetworkManager.ConnectionApprovalRequest
            {
                ClientNetworkId = 7,
                Payload = Encoding.UTF8.GetBytes(ConnectionPayload.Write(Bootstrap.Version.ToString(), "เพื่อน")),
            }, response);
            Assert.IsTrue(response.Approved);
            Assert.AreEqual("เพื่อน", LobbyGate.NameFor(7));
            gate.Approve(new NetworkManager.ConnectionApprovalRequest
            {
                ClientNetworkId = 8,
                Payload = Encoding.UTF8.GetBytes(Bootstrap.Version.ToString()),
            }, new NetworkManager.ConnectionApprovalResponse());
            Assert.AreEqual("", LobbyGate.NameFor(8), "no name: the board shows ผู้เล่น N");
            Assert.AreEqual(GameData.PlayerName, LobbyGate.NameFor(NetworkManager.ServerClientId), "the host's own name");
        }
    }
}
