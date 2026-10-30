using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SpiritMovers.Editor
{
    /// <summary>
    /// One-time project setup that must be done through Unity APIs rather than by hand-editing assets.
    /// Safe to re-run: it overwrites the Boot scene and resets the build scene list to it.
    /// </summary>
    public static class ProjectSetup
    {
        private const string BootScenePath = "Assets/_Game/Scenes/Boot.unity";

        [MenuItem("Spirit Movers/Setup/Create Boot Scene")]
        public static void CreateBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("Bootstrap").AddComponent<Bootstrap>();

            if (!EditorSceneManager.SaveScene(scene, BootScenePath))
            {
                Debug.LogError($"[Setup] Could not save {BootScenePath}");
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                return;
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScenePath, true) };

            PlayerSettings.companyName = "SpiritMovers";
            PlayerSettings.productName = "Spirit Movers";
            PlayerSettings.bundleVersion = "0.0.1";
            AssetDatabase.SaveAssets();

            Debug.Log($"[Setup] Boot scene created at {BootScenePath}; product '{PlayerSettings.productName}' v{PlayerSettings.bundleVersion}");
        }
    }
}
