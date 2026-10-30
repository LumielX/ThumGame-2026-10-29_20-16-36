using System.IO;
using System.Linq;
using SpiritMovers.Core.Text;
using SpiritMovers.Text;
using UnityEditor;
using UnityEngine;

namespace SpiritMovers.Editor
{
    /// <summary>
    /// Creates a <see cref="ThaiGlyphMap"/> next to a .ttf by reading its glyph names (D-035).
    /// Select the font in the Project window, or run <see cref="BuildForGameFonts"/> from the command line.
    /// </summary>
    public static class ThaiGlyphMapBuilder
    {
        private static readonly string[] s_GameFonts =
        {
            "Assets/_Game/Fonts/Mali/Mali-Regular.ttf",
            "Assets/_Game/Fonts/Mali/Mali-Bold.ttf",
        };

        [MenuItem("Spirit Movers/Text/Build Thai Glyph Map (selected font)")]
        public static void BuildForSelection()
        {
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!path.EndsWith(".ttf") && !path.EndsWith(".otf"))
            {
                Debug.LogError("[ThaiText] Select a .ttf or .otf font first.");
                return;
            }
            Build(path);
        }

        [MenuItem("Spirit Movers/Text/Build Thai Glyph Maps (game fonts)")]
        public static void BuildForGameFonts()
        {
            foreach (var font in s_GameFonts)
                Build(font);
        }

        public static ThaiGlyphMap Build(string fontPath)
        {
            var names = TrueTypeGlyphNames.Read(File.ReadAllBytes(fontPath));
            var entries = ThaiShaper.Variants.Select(v => new ThaiGlyphMap.Entry
            {
                Pua = v.Pua,
                Base = v.Base,
                GlyphIndex = names.TryGetValue(v.GlyphName, out var index) ? index : 0,
            }).ToList();

            var mapPath = Path.ChangeExtension(fontPath, null) + " ThaiGlyphMap.asset";
            var map = AssetDatabase.LoadAssetAtPath<ThaiGlyphMap>(mapPath);
            if (map == null)
            {
                map = ScriptableObject.CreateInstance<ThaiGlyphMap>();
                AssetDatabase.CreateAsset(map, mapPath);
            }

            map.SetEntries(entries);
            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssets();

            var missing = entries.Where(e => e.GlyphIndex == 0).Select(e => $"U+{e.Base:X4}").ToArray();
            Debug.Log($"[ThaiText] {mapPath}: {entries.Count - missing.Length}/{entries.Count} alternates found" +
                      (missing.Length > 0 ? $"; using base glyph for {string.Join(", ", missing)}" : ""));
            return map;
        }
    }
}
