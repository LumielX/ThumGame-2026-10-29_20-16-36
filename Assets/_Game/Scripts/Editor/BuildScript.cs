using System.IO;
using System.Linq;
using SpiritMovers.Core;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SpiritMovers.Editor
{
    /// <summary>
    /// Repeatable Windows build, callable from the command line:
    /// Unity.exe -batchmode -quit -projectPath . -executeMethod SpiritMovers.Editor.BuildScript.BuildWindows
    /// Output: Builds/SpiritMovers-v{version}-Windows/SpiritMovers.exe (Builds/ is git-ignored).
    /// </summary>
    public static class BuildScript
    {
        private const string BuildRoot = "Builds";

        [MenuItem("Spirit Movers/Build/Windows 64-bit")]
        public static void BuildWindows()
        {
            var version = GameVersion.Parse(PlayerSettings.bundleVersion);
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Fail("No enabled scenes in Build Settings.");
                return;
            }

            var outputDir = Path.Combine(BuildRoot, $"SpiritMovers-v{version}-Windows");
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(outputDir, "SpiritMovers.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            var summary = BuildPipeline.BuildPlayer(options).summary;
            Debug.Log($"[Build] result={summary.result} output={summary.outputPath} " +
                      $"size={summary.totalSize} bytes time={summary.totalTime} " +
                      $"errors={summary.totalErrors} warnings={summary.totalWarnings}");

            if (summary.result != BuildResult.Succeeded)
                Fail($"Build {summary.result}.");
        }

        private static void Fail(string message)
        {
            Debug.LogError($"[Build] {message}");
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }
}
