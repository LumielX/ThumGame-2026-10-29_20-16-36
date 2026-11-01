using SpiritMovers.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpiritMovers
{
    /// <summary>
    /// First object in the Boot scene. Reports the build version so logs from
    /// playtests can be matched to a release (TECHNICAL_DESIGN 15).
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        public static GameVersion Version { get; private set; }

        private void Awake()
        {
            if (!GameVersion.TryParse(Application.version, out var version))
            {
                Debug.LogError($"[Boot] Invalid application version '{Application.version}'. Expected major.minor.patch.");
                return;
            }

            Version = version;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // Version-mismatch test runs pretend to be another build (Tools/m4a-flow-test.ps1 -VersionMismatch).
            var args = System.Environment.GetCommandLineArgs();
            var at = System.Array.IndexOf(args, "-dev-version");
            if (at >= 0 && at + 1 < args.Length && GameVersion.TryParse(args[at + 1], out var pretend))
                Version = pretend;
#endif
            Debug.Log($"[Boot] {Application.productName} v{Version} ({Application.platform})");
            Save.GameData.Load();
        }

        /// <summary>On to the main menu (the persistent GameNetwork object in this scene comes along).</summary>
        private void Start()
        {
            Settings.SettingsApplier.ApplyAll();
            if (SceneUtility.GetBuildIndexByScenePath("Assets/_Game/Scenes/MainMenu.unity") >= 0)
                SceneManager.LoadScene("MainMenu");
        }
    }
}
