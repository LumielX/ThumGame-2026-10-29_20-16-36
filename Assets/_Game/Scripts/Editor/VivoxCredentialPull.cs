using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SpiritMovers.Editor
{
    /// <summary>
    /// Pulls Vivox server/domain/issuer from the Unity Dashboard into ProjectSettings, the same way the
    /// Project Settings › Services › Vivox page does. Needs a signed-in Editor (not -batchmode, which has no
    /// access token). Test mode stays off so the secret key is never written to the public repo.
    /// Command line: Unity.exe -projectPath . -executeMethod SpiritMovers.Editor.VivoxCredentialPull.PullAndExit
    /// </summary>
    public static class VivoxCredentialPull
    {
        private const double TimeoutSeconds = 90;
        private static double s_Deadline;
        private static bool s_Started;

        [MenuItem("Spirit Movers/Services/Pull Vivox Credentials")]
        public static void Pull() => Start(exitWhenDone: false);

        public static void PullAndExit() => Start(exitWhenDone: true);

        private static void Start(bool exitWhenDone)
        {
            s_Deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
            s_Started = false;
            EditorApplication.update += Tick;

            void Tick()
            {
                // Sign-in finishes asynchronously after the Editor loads; wait for the token before asking.
                if (!s_Started && !string.IsNullOrEmpty(CloudProjectSettings.accessToken) && !string.IsNullOrEmpty(CloudProjectSettings.projectId))
                {
                    s_Started = true;
                    Request();
                }
                else if (s_Started && HasCredentials(out var report))
                {
                    Debug.Log($"[VivoxPull] {report}");
                    Finish(0);
                }
                else if (EditorApplication.timeSinceStartup > s_Deadline)
                {
                    Debug.LogError($"[VivoxPull] timed out (signedIn={!string.IsNullOrEmpty(CloudProjectSettings.accessToken)} " +
                                   $"linked={!string.IsNullOrEmpty(CloudProjectSettings.projectId)})");
                    Finish(1);
                }
            }

            void Finish(int code)
            {
                EditorApplication.update -= Tick;
                if (exitWhenDone)
                    EditorApplication.Exit(code);
            }
        }

        private static Assembly VivoxEditor => Assembly.Load("Unity.Services.Vivox.Editor");

        private static void Request()
        {
            var settingsType = VivoxEditor.GetType("Unity.Services.Vivox.Editor.VivoxSettings", true);
            settingsType.GetProperty("IsTestMode")!.SetValue(Settings(settingsType), false);

            var clientType = VivoxEditor.GetType("Unity.Services.Vivox.Editor.VivoxApiClient", true);
            var client = clientType.GetProperty("Instance", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null);
            clientType.GetField("IsEditorFirstPass", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client, false);
            // Callbacks are optional; the result is read back from VivoxSettings in Tick.
            clientType.GetMethod("GetAndSetVivoxCredentials", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(client, new object[] { null, null });
            Debug.Log("[VivoxPull] request sent");
        }

        private static bool HasCredentials(out string report)
        {
            var settingsType = VivoxEditor.GetType("Unity.Services.Vivox.Editor.VivoxSettings", true);
            var settings = Settings(settingsType);
            bool Has(string name) => !string.IsNullOrEmpty(settingsType.GetProperty(name)!.GetValue(settings) as string);
            // Values are not logged; only whether each one arrived and that no secret key was stored.
            report = $"server={Has("Server")} domain={Has("Domain")} issuer={Has("TokenIssuer")} secretKeyStored={Has("TokenKey")}";
            return Has("Server") && Has("Domain") && Has("TokenIssuer");
        }

        private static object Settings(Type settingsType) =>
            settingsType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null);
    }
}
