using System.IO;
using SpiritMovers.Core;
using SpiritMovers.Dev;
using SpiritMovers.Interaction;
using SpiritMovers.Player;
using SpiritMovers.Text;
using TMPro;
using Unity.Multiplayer.Tools.NetworkSimulator.Runtime;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpiritMovers.Editor
{
    /// <summary>
    /// Builds the M3 greybox test room: two rooms joined by a doorway, stairs up to a mezzanine,
    /// player spawns, carryable boxes and crates, the networked prefabs and the scene's NetworkManager.
    /// Batch mode: -executeMethod SpiritMovers.Editor.TestRoomSetup.CreateScene
    /// </summary>
    public static class TestRoomSetup
    {
        public const string ScenePath = "Assets/_Game/Scenes/M3_TestRoom.unity";
        private const string PrefabDir = "Assets/_Game/Prefabs";
        private const string PlayerPath = PrefabDir + "/Player.prefab";
        private const string PrefabListPath = PrefabDir + "/NetworkPrefabs.asset";
        private const string ActionsPath = "Assets/_Game/Settings/Input/SpiritMovers.inputactions";
        private const string LightBoxPath = PrefabDir + "/LightBox.prefab";
        private const string HeavyCratePath = PrefabDir + "/HeavyCrate.prefab";
        private const string FontPath = "Assets/_Game/Fonts/Mali/Mali-Regular SDF.asset";
        private const string GlyphMapPath = "Assets/_Game/Fonts/Mali/Mali-Regular ThaiGlyphMap.asset";
        private const float WallHeight = 3f;
        private const string UrpLitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Lit.mat";

        [MenuItem("Spirit Movers/Test Room/Create Scene")]
        public static void CreateScene()
        {
            Directory.CreateDirectory(PrefabDir);
            var player = CreatePlayerPrefab();
            var lightBox = CreateCarryablePrefab(LightBoxPath, "LightBox", new Vector3(0.4f, 0.4f, 0.4f), 2f, new Color(0.85f, 0.7f, 0.45f));
            var heavyCrate = CreateCarryablePrefab(HeavyCratePath, "HeavyCrate", new Vector3(0.8f, 0.6f, 0.6f), 25f, new Color(0.45f, 0.3f, 0.2f));
            var prefabs = CreatePrefabList(player, lightBox, heavyCrate);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var overview = Camera.main;
            if (overview != null)
                overview.transform.SetPositionAndRotation(new Vector3(0f, 18f, -16f), Quaternion.Euler(50f, 0f, 0f));

            BuildRoom();
            for (var i = 0; i < 4; i++)
                Marker($"PlayerSpawn{i}", new Vector3(-7.5f + i * 1.5f, 0f, -8f), 0f);
            Marker("StairsBottom", new Vector3(3f, 0f, -6f), 90f);

            // In-scene items, north of the spawns so the movement tests have a clear run.
            for (var i = 0; i < 8; i++)
                Place(lightBox, $"LightBox{i}", new Vector3(-8f + (i % 4) * 1.5f, 0.2f, i < 4 ? 3f : 6f));
            Place(heavyCrate, "HeavyCrate0", new Vector3(-5f, 0.3f, 0.5f));
            Place(heavyCrate, "HeavyCrate1", new Vector3(-2.5f, 0.3f, 0.5f));

            var networkObject = new GameObject("NetworkManager");
            var transport = networkObject.AddComponent<UnityTransport>();
            var manager = networkObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, PlayerPrefab = player };
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabs);
            networkObject.AddComponent<NetworkSimulator>(); // -dev-latency in test runs; no effect otherwise

            new GameObject("DevSessionRunner").AddComponent<DevSessionRunner>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[TestRoom] Created {ScenePath}");
        }

        /// <summary>Development player of the test room for the two-instance tests (Tools/m3b-carry-test.ps1).</summary>
        [MenuItem("Spirit Movers/Test Room/Create Scene and Build")]
        public static void BuildTestRoom()
        {
            CreateScene();
            var version = GameVersion.Parse(PlayerSettings.bundleVersion);
            var summary = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine("Builds", $"M3-TestRoom-v{version}", "SpiritMovers.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            }).summary;
            Debug.Log($"[TestRoom] build result={summary.result} output={summary.outputPath} errors={summary.totalErrors}");
            if (summary.result != BuildResult.Succeeded && Application.isBatchMode)
                EditorApplication.Exit(1);
        }

        private static void BuildRoom()
        {
            var root = new GameObject("Greybox").transform;
            Block(root, "Floor", new Vector3(0f, -0.1f, 0f), new Vector3(20f, 0.2f, 20f));
            Block(root, "WallNorth", new Vector3(0f, WallHeight / 2f, 10f), new Vector3(20f, WallHeight, 0.2f));
            Block(root, "WallSouth", new Vector3(0f, WallHeight / 2f, -10f), new Vector3(20f, WallHeight, 0.2f));
            Block(root, "WallEast", new Vector3(10f, WallHeight / 2f, 0f), new Vector3(0.2f, WallHeight, 20f));
            Block(root, "WallWest", new Vector3(-10f, WallHeight / 2f, 0f), new Vector3(0.2f, WallHeight, 20f));
            // Divider between the two rooms with a 1.6 m doorway at z = 0.
            Block(root, "DividerSouth", new Vector3(0f, WallHeight / 2f, -5.4f), new Vector3(0.2f, WallHeight, 9.2f));
            Block(root, "DividerNorth", new Vector3(0f, WallHeight / 2f, 5.4f), new Vector3(0.2f, WallHeight, 9.2f));

            // Six 0.2 m steps up to a 1.2 m mezzanine in the east room.
            for (var i = 0; i < 6; i++)
            {
                var top = 0.2f * (i + 1);
                Block(root, $"Step{i}", new Vector3(4.175f + 0.35f * i, top / 2f, -6f), new Vector3(0.35f, top, 1.6f));
            }
            Block(root, "Mezzanine", new Vector3(8f, 0.6f, -6f), new Vector3(3.8f, 1.2f, 3f));
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

        private static void Place(GameObject prefab, string name, Vector3 position)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.position = position;
        }

        private static GameObject CreateCarryablePrefab(string path, string name, Vector3 size, float mass, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.localScale = size;
            // A copy of URP's own Lit material: one built from Shader.Find in batch mode rendered magenta in the player.
            var material = new Material(AssetDatabase.LoadAssetAtPath<Material>(UrpLitMaterialPath));
            material.SetColor("_BaseColor", color);
            var materialPath = path.Replace(".prefab", ".mat");
            AssetDatabase.DeleteAsset(materialPath);
            AssetDatabase.CreateAsset(material, materialPath);
            go.GetComponent<Renderer>().sharedMaterial = material;
            var body = go.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            // Items must survive their carrier disconnecting (M2 finding, D-034).
            go.AddComponent<NetworkObject>().DontDestroyWithOwner = true;
            go.AddComponent<NetworkTransform>().AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            go.AddComponent<NetworkRigidbody>();
            go.AddComponent<Carryable>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void Marker(string name, Vector3 position, float yaw) =>
            new GameObject(name).transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

        private static GameObject CreatePlayerPrefab()
        {
            var root = new GameObject("Player");
            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.radius = 0.35f;
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 50f;
            controller.skinWidth = 0.02f;
            controller.minMoveDistance = 0f;
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>().AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            var motor = root.AddComponent<PlayerMotor>();
            var serialized = new SerializedObject(motor);
            serialized.FindProperty("m_Actions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<PlayerCarrier>();
            var interactor = new SerializedObject(root.AddComponent<PlayerInteractor>());
            interactor.FindProperty("m_PromptFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            interactor.FindProperty("m_PromptGlyphMap").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ThaiGlyphMap>(GlyphMapPath);
            interactor.ApplyModifiedPropertiesWithoutUndo();

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static NetworkPrefabsList CreatePrefabList(params GameObject[] prefabs)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(PrefabListPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, PrefabListPath);
            }
            foreach (var prefab in prefabs)
                if (!list.Contains(prefab))
                    list.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
            return list;
        }
    }
}
