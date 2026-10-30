using System.IO;
using SpiritMovers.Core;
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

namespace SpiritMovers.Prototypes.NetSync.Editor
{
    /// <summary>
    /// Builds the throwaway network prototype scene/prefabs and a Development player for it.
    /// Batch mode: -executeMethod SpiritMovers.Prototypes.NetSync.Editor.NetSyncProtoSetup.CreateAndBuild
    /// </summary>
    public static class NetSyncProtoSetup
    {
        private const string Root = "Assets/_Game/Prototypes/NetSync";
        private const string ScenePath = Root + "/Proto_NetSync.unity";
        private const string PropPath = Root + "/ProtoProp.prefab";
        private const string PlayerPath = Root + "/ProtoPlayer.prefab";

        [MenuItem("Spirit Movers/Prototypes/NetSync: Create Scene")]
        public static void CreateScene()
        {
            var prop = CreatePropPrefab();
            var player = CreatePlayerPrefab();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var overview = Camera.main;
            if (overview != null)
                overview.transform.SetPositionAndRotation(new Vector3(0f, 12f, -12f), Quaternion.Euler(45f, 0f, 0f));

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);

            var networkObject = new GameObject("NetworkManager");
            var transport = networkObject.AddComponent<UnityTransport>();
            var manager = networkObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport };
            networkObject.AddComponent<NetworkSimulator>();

            var runner = new GameObject("NetSyncProtoRunner").AddComponent<NetSyncProtoRunner>();
            var serialized = new SerializedObject(runner);
            serialized.FindProperty("m_PlayerPrefab").objectReferenceValue = player;
            serialized.FindProperty("m_PropPrefab").objectReferenceValue = prop;
            serialized.FindProperty("m_ChatFont").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Game/Fonts/Mali/Mali-Regular SDF.asset");
            serialized.FindProperty("m_ChatGlyphMap").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<ThaiGlyphMap>("Assets/_Game/Fonts/Mali/Mali-Regular ThaiGlyphMap.asset");
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[ProtoSetup] Created {ScenePath}");
        }

        [MenuItem("Spirit Movers/Prototypes/NetSync: Create Scene and Build")]
        public static void CreateAndBuild()
        {
            CreateScene();
            var version = GameVersion.Parse(PlayerSettings.bundleVersion);
            var output = Path.Combine("Builds", $"Proto-NetSync-v{version}", "SpiritMovers.exe");
            var summary = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            }).summary;

            Debug.Log($"[ProtoSetup] build result={summary.result} output={summary.outputPath} errors={summary.totalErrors}");
            if (summary.result != BuildResult.Succeeded && Application.isBatchMode)
                EditorApplication.Exit(1);
        }

        private static GameObject CreatePropPrefab()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ProtoProp";
            go.transform.localScale = Vector3.one * 0.6f;
            var body = go.AddComponent<Rigidbody>();
            body.mass = 5f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            // Props must survive their carrier disconnecting (found in the first relay test: carried prop vanished).
            go.AddComponent<NetworkObject>().DontDestroyWithOwner = true;
            var networkTransform = go.AddComponent<NetworkTransform>();
            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            go.AddComponent<NetworkRigidbody>();
            go.AddComponent<ProtoProp>();
            return SaveAsPrefab(go, PropPath);
        }

        private static GameObject CreatePlayerPrefab()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "ProtoPlayer";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.AddComponent<NetworkObject>();
            var networkTransform = go.AddComponent<NetworkTransform>();
            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            go.AddComponent<ProtoPlayer>();
            return SaveAsPrefab(go, PlayerPath);
        }

        private static GameObject SaveAsPrefab(GameObject go, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
