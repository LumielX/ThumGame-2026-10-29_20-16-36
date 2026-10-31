using System.IO;
using SpiritMovers.Core;
using SpiritMovers.Dev;
using SpiritMovers.Ghost;
using SpiritMovers.Interaction;
using SpiritMovers.Job;
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

        public const string HouseScenePath = "Assets/_Game/Scenes/M3_TestHouse.unity";

        private struct Prefabs
        {
            public GameObject Player, LightBox, HeavyCrate, JobDirector, GhostBell, Van, Ghost, Cabinet, SpiritCamera;
            public NetworkPrefabsList List;
        }

        private static Prefabs CreatePrefabs()
        {
            Directory.CreateDirectory(PrefabDir);
            var result = new Prefabs
            {
                Player = CreatePlayerPrefab(),
                LightBox = CreateCarryablePrefab(LightBoxPath, "LightBox", new Vector3(0.4f, 0.4f, 0.4f), 2f, new Color(0.85f, 0.7f, 0.45f)),
                HeavyCrate = CreateCarryablePrefab(HeavyCratePath, "HeavyCrate", new Vector3(0.8f, 0.6f, 0.6f), 25f, new Color(0.45f, 0.3f, 0.2f)),
            };
            result.JobDirector = CreateJobDirectorPrefab();
            result.GhostBell = CreateBellPrefab();
            result.Van = CreateVanPrefab();
            result.Ghost = CreateGhostPrefab();
            result.Cabinet = CreateCabinetPrefab();
            result.SpiritCamera = CreateCarryablePrefab(PrefabDir + "/SpiritCamera.prefab", "SpiritCamera",
                new Vector3(0.25f, 0.18f, 0.15f), 1f, new Color(0.15f, 0.15f, 0.17f), typeof(SpiritCamera));
            result.List = CreatePrefabList(result.Player, result.LightBox, result.HeavyCrate, result.JobDirector, result.GhostBell,
                result.Van, result.Ghost, result.Cabinet, result.SpiritCamera);
            return result;
        }

        /// <summary>New scene with the greybox shell, spawns, NetworkManager and dev runner.</summary>
        private static UnityEngine.SceneManagement.Scene NewGreyboxScene(Prefabs prefabs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var overview = Camera.main;
            if (overview != null)
                overview.transform.SetPositionAndRotation(new Vector3(0f, 18f, -16f), Quaternion.Euler(50f, 0f, 0f));

            BuildRoom();
            for (var i = 0; i < 4; i++)
                Marker($"PlayerSpawn{i}", new Vector3(-7.5f + i * 1.5f, 0f, -8f), 0f);
            Marker("StairsBottom", new Vector3(3f, 0f, -6f), 90f);

            var networkObject = new GameObject("NetworkManager");
            var transport = networkObject.AddComponent<UnityTransport>();
            var manager = networkObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, PlayerPrefab = prefabs.Player };
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabs.List);
            networkObject.AddComponent<NetworkSimulator>(); // -dev-latency in test runs; no effect otherwise

            new GameObject("DevSessionRunner").AddComponent<DevSessionRunner>();
            return scene;
        }

        [MenuItem("Spirit Movers/Test Room/Create Scene")]
        public static void CreateScene()
        {
            var prefabs = CreatePrefabs();
            var lightBox = prefabs.LightBox;
            var heavyCrate = prefabs.HeavyCrate;
            var scene = NewGreyboxScene(prefabs);

            // In-scene items, north of the spawns so the movement tests have a clear run.
            for (var i = 0; i < 8; i++)
                Place(lightBox, $"LightBox{i}", new Vector3(-8f + (i % 4) * 1.5f, 0.2f, i < 4 ? 3f : 6f));
            Place(heavyCrate, "HeavyCrate0", new Vector3(-5f, 0.3f, 0.5f));
            Place(heavyCrate, "HeavyCrate1", new Vector3(-2.5f, 0.3f, 0.5f));

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[TestRoom] Created {ScenePath}");
        }

        /// <summary>Development player of the test room for the two-instance tests (Tools/m3b-carry-test.ps1).</summary>
        [MenuItem("Spirit Movers/Test Room/Create Scene and Build")]
        public static void BuildTestRoom()
        {
            CreateScene();
            BuildScene(ScenePath, "M3-TestRoom");
        }

        /// <summary>
        /// M3C greybox house: the test room shell as three rooms (west living room, east kitchen, loft bedroom),
        /// 12 memory slots with their items at home, scatter and hiding spots, and the job director.
        /// Batch mode (no -nographics): -executeMethod SpiritMovers.Editor.TestRoomSetup.CreateHouseScene
        /// </summary>
        [MenuItem("Spirit Movers/Test Room/Create House Scene")]
        public static void CreateHouseScene()
        {
            var prefabs = CreatePrefabs();
            var scene = NewGreyboxScene(prefabs);

            var house = new GameObject("House").AddComponent<HouseAuthoring>();
            var houseSo = new SerializedObject(house);
            houseSo.FindProperty("m_HouseId").stringValue = "greybox";
            houseSo.ApplyModifiedPropertiesWithoutUndo();

            var west = Room(house.transform, "west", new Vector3(-5f, 1.5f, 0f), new Vector3(9.8f, 3f, 19.8f));
            var east = Room(house.transform, "east", new Vector3(5f, 0.575f, 0f), new Vector3(9.8f, 1.15f, 19.8f));
            var loft = Room(house.transform, "loft", new Vector3(8f, 2.1f, -6f), new Vector3(3.8f, 1.8f, 3f));

            Slot(west, "w0", prefabs.LightBox, new Vector3(-8f, 0.2f, 3f));
            Slot(west, "w1", prefabs.LightBox, new Vector3(-6.5f, 0.2f, 3f));
            Slot(west, "w2", prefabs.LightBox, new Vector3(-5f, 0.2f, 3f));
            Slot(west, "w3", prefabs.LightBox, new Vector3(-3.5f, 0.2f, 3f));
            Slot(west, "w4", prefabs.HeavyCrate, new Vector3(-5f, 0.3f, 6.5f));
            Slot(east, "e0", prefabs.LightBox, new Vector3(3f, 0.2f, 3f));
            Slot(east, "e1", prefabs.LightBox, new Vector3(5f, 0.2f, 3f));
            Slot(east, "e2", prefabs.LightBox, new Vector3(7f, 0.2f, 3f));
            Slot(east, "e3", prefabs.HeavyCrate, new Vector3(5f, 0.3f, 7f));
            Slot(loft, "l0", prefabs.LightBox, new Vector3(7f, 1.4f, -6f));
            Slot(loft, "l1", prefabs.LightBox, new Vector3(8f, 1.4f, -5.2f));
            Slot(loft, "l2", prefabs.LightBox, new Vector3(9f, 1.4f, -6.8f));

            // Moved items land on these (turned 35 degrees so they also need straightening).
            Spot(west, "Scatter", new Vector3(-8f, 0.25f, -3f));
            Spot(west, "Scatter", new Vector3(-3f, 0.25f, -3f));
            Spot(west, "Scatter", new Vector3(-8.5f, 0.25f, 8.5f));
            Spot(west, "Scatter", new Vector3(-2f, 0.25f, 8.5f));
            Spot(east, "Scatter", new Vector3(2f, 0.25f, -2f));
            Spot(east, "Scatter", new Vector3(8f, 0.25f, 1f));
            Spot(east, "Scatter", new Vector3(2f, 0.25f, 8.5f));
            Spot(east, "Scatter", new Vector3(8.5f, 0.25f, 8.5f));
            Spot(loft, "Scatter", new Vector3(6.6f, 1.45f, -4.9f));
            Spot(loft, "Scatter", new Vector3(9.4f, 1.45f, -4.9f));
            Spot(west, "Hide", new Vector3(-9.3f, 0.25f, -9.3f));
            Spot(east, "Hide", new Vector3(9.3f, 0.25f, 9.3f));
            Spot(east, "Hide", new Vector3(9.3f, 0.25f, -0.5f));
            Spot(loft, "Hide", new Vector3(6.5f, 1.45f, -7.2f));

            // A prefab instance: a NetworkObject created by script in an unsaved scene gets no GlobalObjectIdHash
            // and NGO refuses to spawn it.
            Bell(prefabs.GhostBell, "west", new Vector3(-9.75f, 1.2f, -2f));
            Bell(prefabs.GhostBell, "east", new Vector3(9.75f, 1.2f, 4f));
            Bell(prefabs.GhostBell, "loft", new Vector3(9.75f, 1.9f, -6f));

            // The company van stands in the living room by the spawns until the M4 lobby exists.
            var van = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.Van);
            van.name = "Van";
            van.transform.position = new Vector3(-9.2f, 0.6f, -5.5f);

            var ui = new GameObject("JobUI");
            foreach (var panel in new MonoBehaviour[] { ui.AddComponent<JobSheetPanel>(), ui.AddComponent<ResultsPanel>() })
            {
                var panelSo = new SerializedObject(panel);
                panelSo.FindProperty("m_Font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                panelSo.FindProperty("m_GlyphMap").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ThaiGlyphMap>(GlyphMapPath);
                panelSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // The shy ghost starts at the north end of the living room; the main light flickers when it is angry.
            var ghost = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.Ghost);
            ghost.name = "Ghost";
            ghost.transform.position = new Vector3(-5f, 0f, 8f);
            // Memory cabinets (GDD 6.3) against the outer walls, doors facing into the room; the loft is too small for one.
            Cabinet(prefabs.Cabinet, "west", new Vector3(-9.3f, 0f, 6f), 90f);
            Cabinet(prefabs.Cabinet, "east", new Vector3(9.3f, 0f, 6f), -90f);
            var sun = Object.FindAnyObjectByType<Light>();
            if (sun != null)
                sun.gameObject.AddComponent<GhostLights>();

            // The team toolbox by the van: BeginJob puts the spirit cameras here.
            var toolSpot = new GameObject("ToolSpot").transform;
            toolSpot.position = new Vector3(-7.5f, 0.3f, -6.3f);

            var director = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.JobDirector);
            var so = new SerializedObject(director.GetComponent<JobDirector>());
            so.FindProperty("m_House").objectReferenceValue = house;
            so.FindProperty("m_CameraPrefab").objectReferenceValue = prefabs.SpiritCamera;
            so.FindProperty("m_ToolSpot").objectReferenceValue = toolSpot;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, HouseScenePath);
            Debug.Log($"[TestRoom] Created {HouseScenePath}");
        }

        /// <summary>Development player of the house for the job tests (Tools/m3c-job-test.ps1).</summary>
        [MenuItem("Spirit Movers/Test Room/Create House Scene and Build")]
        public static void BuildTestHouse()
        {
            CreateHouseScene();
            BuildScene(HouseScenePath, "M3-TestHouse");
        }

        private static void BuildScene(string scenePath, string folder)
        {
            var version = GameVersion.Parse(PlayerSettings.bundleVersion);
            // Nothing may be left dirty in memory when the build starts (an unsaved material corrupted level0, M3C).
            AssetDatabase.SaveAssets();
            // Always a clean folder: incremental builds over an earlier one also left a corrupted level0 (M3C).
            var outputDir = Path.Combine("Builds", $"{folder}-v{version}");
            FileUtil.DeleteFileOrDirectory(outputDir);
            var summary = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = Path.Combine(outputDir, "SpiritMovers.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            }).summary;
            Debug.Log($"[TestRoom] build result={summary.result} output={summary.outputPath} errors={summary.totalErrors}");
            if (summary.result != BuildResult.Succeeded && Application.isBatchMode)
                EditorApplication.Exit(1);
        }

        /// <summary>A URP Lit material asset of this colour, created once and only recoloured afterwards.</summary>
        private static Material LitMaterial(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Material>(UrpLitMaterialPath));
                AssetDatabase.CreateAsset(material, path);
            }
            if (material.GetColor("_BaseColor") != color)
            {
                material.SetColor("_BaseColor", color);
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        private static Material FloorTintMaterial()
        {
            const string path = PrefabDir + "/FloorTint.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>(UrpLitMaterialPath));
            material.SetColor("_BaseColor", new Color(0.72f, 0.48f, 0.3f));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject CreateBellPrefab()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "GhostBell";
            go.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            go.GetComponent<Renderer>().sharedMaterial = BellMaterial();
            go.AddComponent<NetworkObject>();
            var light = new GameObject("Flash").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.7f, 0.28f); // memory amber #ffb347 (ART_BIBLE 3.3)
            light.range = 6f;
            light.intensity = 3f;
            go.AddComponent<GhostBell>().Configure("", light);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/GhostBell.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateVanPrefab()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Van";
            go.transform.localScale = new Vector3(0.6f, 1.2f, 1.6f);
            go.AddComponent<NetworkObject>();
            go.AddComponent<VanExit>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/Van.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static Material BellMaterial()
        {
            const string path = PrefabDir + "/GhostBell.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>(UrpLitMaterialPath));
            material.SetColor("_BaseColor", new Color(1f, 0.7f, 0.28f));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void Bell(GameObject prefab, string roomId, Vector3 position)
        {
            var bell = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            bell.name = $"Bell_{roomId}";
            bell.transform.position = position;
            var so = new SerializedObject(bell.GetComponent<GhostBell>());
            so.FindProperty("m_RoomId").stringValue = roomId;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Placeholder ghost: a floating capsule without a collider (it drifts through furniture), host-driven.</summary>
        private static GameObject CreateGhostPrefab()
        {
            var go = new GameObject("Ghost");
            go.AddComponent<NetworkObject>();
            go.AddComponent<NetworkTransform>();
            var ghost = go.AddComponent<ShyGhost>();
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = new Vector3(0.7f, 0.6f, 0.7f);
            var renderer = body.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = LitMaterial(PrefabDir + "/Ghost.mat", Color.white);
            var so = new SerializedObject(ghost);
            so.FindProperty("m_Body").objectReferenceValue = renderer;
            so.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/Ghost.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>
        /// Placeholder memory cabinet: a box seen from outside (its faces are culled from inside, so the captive sees out),
        /// an invisible door collider to aim at, and a lamp that blinks when the captive knocks.
        /// </summary>
        private static GameObject CreateCabinetPrefab()
        {
            var go = new GameObject("MemoryCabinet");
            go.AddComponent<NetworkObject>();
            var cabinet = go.AddComponent<MemoryCabinet>();
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            body.transform.localScale = new Vector3(1f, 2f, 1f);
            body.GetComponent<MeshRenderer>().sharedMaterial = LitMaterial(PrefabDir + "/Cabinet.mat", new Color(0.35f, 0.25f, 0.4f));
            var door = new GameObject("Door").AddComponent<BoxCollider>();
            door.transform.SetParent(go.transform, false);
            door.center = new Vector3(0f, 1f, 0.55f);
            door.size = new Vector3(1f, 2f, 0.1f);
            var light = new GameObject("KnockLight").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.transform.localPosition = new Vector3(0f, 2.3f, 0.7f);
            light.color = new Color(1f, 0.7f, 0.28f);
            light.range = 4f;
            cabinet.Configure("", light);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/MemoryCabinet.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void Cabinet(GameObject prefab, string roomId, Vector3 position, float yaw)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = $"Cabinet_{roomId}";
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var cabinet = instance.GetComponent<MemoryCabinet>();
            cabinet.Configure(roomId, instance.GetComponentInChildren<Light>());
            EditorUtility.SetDirty(cabinet);
        }

        /// <summary>The spirit camera overlay material, created once (never re-created: see the magenta-build finding).</summary>
        private static Material SpiritMemoryMaterial()
        {
            const string path = PrefabDir + "/SpiritMemory.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Game/Shaders/SpiritMemory.shader"));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject CreateJobDirectorPrefab()
        {
            var go = new GameObject("JobDirector");
            go.AddComponent<NetworkObject>();
            go.AddComponent<JobDirector>();
            go.AddComponent<NoiseHub>();
            var overlay = new SerializedObject(go.AddComponent<MemoryOverlay>());
            overlay.FindProperty("m_Material").objectReferenceValue = SpiritMemoryMaterial();
            overlay.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/JobDirector.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static Transform Room(Transform house, string id, Vector3 center, Vector3 size)
        {
            var room = new GameObject($"Room_{id}");
            room.transform.SetParent(house, false);
            room.transform.position = center;
            room.AddComponent<BoxCollider>().size = size;
            var so = new SerializedObject(room.AddComponent<RoomVolume>());
            so.FindProperty("m_RoomId").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            room.AddComponent<RoomRestoration>();

            // A floor tile per room so the returning colour is visible beyond the items (warm wood for now).
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = "FloorTint";
            Object.DestroyImmediate(tile.GetComponent<Collider>());
            tile.transform.SetParent(room.transform, false);
            tile.transform.position = new Vector3(center.x, center.y - size.y / 2f + 0.005f, center.z);
            tile.transform.localScale = new Vector3(size.x - 0.2f, 0.01f, size.z - 0.2f);
            tile.GetComponent<Renderer>().sharedMaterial = FloorTintMaterial();
            return room.transform;
        }

        private static void Slot(Transform room, string slotId, GameObject prefab, Vector3 home)
        {
            var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            item.name = $"Item_{slotId}";
            item.transform.position = home;
            var marker = new GameObject($"Slot_{slotId}");
            marker.transform.SetParent(room, false);
            marker.transform.SetPositionAndRotation(home, Quaternion.identity);
            marker.AddComponent<MemorySlotMarker>().Configure(slotId, item.GetComponent<Carryable>());
            EditorUtility.SetDirty(marker);
        }

        private static void Spot(Transform room, string kind, Vector3 position)
        {
            var spot = new GameObject($"{kind}{room.childCount}");
            spot.transform.SetParent(room, false);
            spot.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 35f, 0f));
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

        private static GameObject CreateCarryablePrefab(string path, string name, Vector3 size, float mass, Color color,
            System.Type extra = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.localScale = size;
            // Reuse the material asset across runs: a material deleted and re-created in the session that then
            // builds the player renders magenta in that build (M3C finding; see TESTING.md).
            var material = LitMaterial(path.Replace(".prefab", ".mat"), color);
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
            if (extra != null)
                go.AddComponent(extra);
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
            root.AddComponent<PlayerEmote>();
            root.AddComponent<PlayerCapture>();
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
