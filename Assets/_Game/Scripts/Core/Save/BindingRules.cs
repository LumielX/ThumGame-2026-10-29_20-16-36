using System;
using System.Collections.Generic;

namespace SpiritMovers.Core.Save
{
    /// <summary>Key rebinding checks (GDD 18): two actions on the same control are flagged, not refused.</summary>
    public static class BindingRules
    {
        /// <summary>Pairs of actions bound to the same control path (case-insensitive), in input order.</summary>
        public static IReadOnlyList<(string A, string B)> Conflicts(IReadOnlyList<(string Action, string Path)> bindings)
        {
            var conflicts = new List<(string, string)>();
            for (var i = 0; i < bindings.Count; i++)
            {
                if (string.IsNullOrEmpty(bindings[i].Path))
                    continue;
                for (var j = i + 1; j < bindings.Count; j++)
                {
                    if (bindings[i].Action != bindings[j].Action &&
                        string.Equals(bindings[i].Path, bindings[j].Path, StringComparison.OrdinalIgnoreCase) &&
                        !SharedOnPurpose(bindings[i].Action, bindings[j].Action))
                        conflicts.Add((bindings[i].Action, bindings[j].Action));
                }
            }
            return conflicts;
        }

        /// <summary>Throw and Use share the right mouse button on purpose (which one fires depends on what is held).</summary>
        private static bool SharedOnPurpose(string a, string b) =>
            (a == "Throw" && b == "Use") || (a == "Use" && b == "Throw");
    }
}
