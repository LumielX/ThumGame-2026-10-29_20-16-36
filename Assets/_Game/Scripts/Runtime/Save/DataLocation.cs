using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SpiritMovers.Save
{
    /// <summary>
    /// Where save_v{n}.json and settings.json live: persistentDataPath (TECHNICAL_DESIGN 9), a per-profile folder for
    /// dev bots running side by side, or a test's own folder.
    /// </summary>
    public static class DataLocation
    {
        /// <summary>Tests point this at a temporary folder so they never touch the player's real save.</summary>
        public static string OverrideRoot { get; set; }

        /// <summary>Several game instances on one machine sign in and save as different players (Development builds only).</summary>
        public static string DevProfile
        {
            get
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                var args = Environment.GetCommandLineArgs();
                for (var i = 0; i < args.Length - 1; i++)
                    if (args[i] == "-dev-profile")
                        return args[i + 1];
#endif
                return null;
            }
        }

        public static string Root => RootFor(OverrideRoot ?? Application.persistentDataPath, DevProfile);

        internal static string RootFor(string baseDirectory, string profile)
        {
            var safe = profile == null ? "" : new string(profile.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
            return safe.Length == 0 ? baseDirectory : Path.Combine(baseDirectory, "profiles", safe);
        }
    }
}
