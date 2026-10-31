using SpiritMovers.Session;
using SpiritMovers.Text;
using TMPro;
using Unity.Multiplayer.Tools.NetworkSimulator.Runtime;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace SpiritMovers.Editor
{
    /// <summary>
    /// The production scenes of the session flow (M4A): Boot (with the persistent GameNetwork) → MainMenu → Lobby (the
    /// van) → Job_Greybox (the greybox house without a scene NetworkManager), and the build list in that order.
    /// Batch mode (no -nographics): -executeMethod SpiritMovers.Editor.GameScenesSetup.CreateGameScenes
    /// </summary>
    public static class GameScenesSetup
    {
        private const string SceneDir = "Assets/_Game/Scenes";
        public const string BootPath = SceneDir + "/Boot.unity";
        public const string MainMenuPath = SceneDir + "/MainMenu.unity";
        public const string LobbyPath = SceneDir + "/Lobby.unity";
        public const string JobPath = SceneDir + "/Job_Greybox.unity";
        private const string GameNetworkPath = "Assets/_Game/Prefabs/GameNetwork.prefab";

        [MenuItem("Spirit Movers/Game/Create Game Scenes")]
        public static void CreateGameScenes()
        {
            var prefabs = TestRoomSetup.CreatePrefabs();
            var network = CreateGameNetworkPrefab(prefabs);
            AddToBoot(network);
            CreateMainMenu();
            CreateLobby(prefabs);
            CreateJob(prefabs);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootPath, true),
                new EditorBuildSettingsScene(MainMenuPath, true),
                new EditorBuildSettingsScene(LobbyPath, true),
                new EditorBuildSettingsScene(JobPath, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[GameScenes] Created Boot/MainMenu/Lobby/Job_Greybox and the build list");
        }

        /// <summary>NetworkManager + transport + the session, kept alive across every scene of the game.</summary>
        private static GameObject CreateGameNetworkPrefab(TestRoomSetup.Prefabs prefabs)
        {
            var go = new GameObject("GameNetwork");
            var transport = go.AddComponent<UnityTransport>();
            var manager = go.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = prefabs.Player,
                EnableSceneManagement = true,
                ConnectionApproval = true,
            };
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabs.List);
            go.AddComponent<NetworkSimulator>(); // -dev-latency in test runs; no effect otherwise
            go.AddComponent<GameSession>();
            go.AddComponent<LobbyGate>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, GameNetworkPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void AddToBoot(GameObject network)
        {
            var scene = EditorSceneManager.OpenScene(BootPath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "GameNetwork")
                    Object.DestroyImmediate(root);
            PrefabUtility.InstantiatePrefab(network, scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void CreateMainMenu()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>();
            var menu = new SerializedObject(new GameObject("MainMenu").AddComponent<MainMenuPanel>());
            menu.FindProperty("m_Font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TestRoomSetup.FontPath);
            menu.FindProperty("m_GlyphMap").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ThaiGlyphMap>(TestRoomSetup.GlyphMapPath);
            menu.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, MainMenuPath);
        }

        /// <summary>The company van as a greybox room: floor, walls, six spawns and the job board.</summary>
        private static void CreateLobby(TestRoomSetup.Prefabs prefabs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var van = new GameObject("Van").transform;
            Block(van, "Floor", new Vector3(0f, -0.1f, 0f), new Vector3(4f, 0.2f, 8f));
            Block(van, "WallLeft", new Vector3(-2.1f, 1.2f, 0f), new Vector3(0.2f, 2.4f, 8f));
            Block(van, "WallRight", new Vector3(2.1f, 1.2f, 0f), new Vector3(0.2f, 2.4f, 8f));
            Block(van, "WallFront", new Vector3(0f, 1.2f, 4.1f), new Vector3(4.4f, 2.4f, 0.2f));
            Block(van, "WallBack", new Vector3(0f, 1.2f, -4.1f), new Vector3(4.4f, 2.4f, 0.2f));
            Block(van, "Roof", new Vector3(0f, 2.5f, 0f), new Vector3(4.4f, 0.2f, 8.4f));
            Block(van, "JobBoard", new Vector3(0f, 1.3f, 3.9f), new Vector3(1.6f, 1f, 0.1f));
            for (var i = 0; i < 6; i++)
                new GameObject($"PlayerSpawn{i}").transform.SetPositionAndRotation(
                    new Vector3(i % 2 == 0 ? -0.9f : 0.9f, 0f, -2.5f + i / 2 * 1.5f), Quaternion.identity);
            PrefabUtility.InstantiatePrefab(prefabs.LobbyState, scene);
            var panel = new SerializedObject(new GameObject("LobbyBoard").AddComponent<LobbyPanel>());
            panel.FindProperty("m_Font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TestRoomSetup.FontPath);
            panel.FindProperty("m_GlyphMap").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ThaiGlyphMap>(TestRoomSetup.GlyphMapPath);
            panel.ApplyModifiedPropertiesWithoutUndo();
            SaveWithSceneIds(scene, LobbyPath);
        }

        private static void CreateJob(TestRoomSetup.Prefabs prefabs)
        {
            var scene = TestRoomSetup.NewGreyboxScene(prefabs, withNetwork: false);
            TestRoomSetup.BuildHouse(prefabs);
            SaveWithSceneIds(scene, JobPath);
        }

        /// <summary>
        /// Save, reopen and save again: a new scene has no asset GUID until its first save, so in-scene NetworkObjects
        /// get duplicate GlobalObjectIdHash values (NGO refuses them); reopening recomputes them (OnValidate).
        /// </summary>
        private static void SaveWithSceneIds(UnityEngine.SceneManagement.Scene scene, string path)
        {
            EditorSceneManager.SaveScene(scene, path);
            var reopened = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            EditorSceneManager.MarkSceneDirty(reopened);
            EditorSceneManager.SaveScene(reopened);
        }

        private static void Block(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = center;
            block.transform.localScale = size;
            block.isStatic = true;
        }
    }
}
