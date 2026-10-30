using System.Reflection;
using SpiritMovers.Core.Text;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace SpiritMovers.Text
{
    /// <summary>
    /// Makes a TextMeshPro font asset render <see cref="ThaiShaper"/> output (D-035): each Private Use Area
    /// code point is registered as a character that uses the font's alternate glyph.
    /// </summary>
    public static class ThaiFontSupport
    {
        // TMP only exposes adding glyphs by Unicode; adding by glyph index is internal (checked on ugui 2.0).
        private static readonly MethodInfo s_TryAddGlyph = typeof(TMP_FontAsset).GetMethod(
            "TryAddGlyphInternal", BindingFlags.Instance | BindingFlags.NonPublic, null,
            new[] { typeof(uint), typeof(Glyph).MakeByRefType() }, null);

        /// <summary>Registers the alternates; returns how many were bound to dedicated glyphs.</summary>
        public static int Register(TMP_FontAsset font, ThaiGlyphMap map)
        {
            if (font == null || map == null)
            {
                Debug.LogError("[ThaiText] Register needs a font asset and a glyph map.");
                return 0;
            }

            if (s_TryAddGlyph == null)
            {
                Debug.LogError("[ThaiText] TMP_FontAsset.TryAddGlyphInternal not found — TextMeshPro changed; Thai tone marks will overlap.");
                return 0;
            }

            // A freshly loaded font asset has no glyph lookup until TMP first reads it (e.g. Register in Awake).
            font.ReadFontAssetDefinition();

            var bound = 0;
            foreach (var entry in map.Entries)
            {
                var glyph = entry.GlyphIndex > 0 ? AddGlyph(font, (uint)entry.GlyphIndex) : null;
                if (glyph != null)
                    bound++;
                else if (font.TryAddCharacters(((char)entry.Base).ToString()) &&
                         font.characterLookupTable.TryGetValue((uint)entry.Base, out var baseCharacter))
                    glyph = baseCharacter.glyph;

                if (glyph == null)
                    continue;

                var character = new TMP_Character((uint)entry.Pua, font, glyph);
                font.characterLookupTable[(uint)entry.Pua] = character;
                if (!font.characterTable.Exists(c => c.unicode == (uint)entry.Pua))
                    font.characterTable.Add(character);
            }

            return bound;
        }

        /// <summary>Shapes Thai text and assigns it to a TextMeshPro component.</summary>
        public static void SetThaiText(this TMP_Text target, string text) => target.text = ThaiShaper.Shape(text);

        /// <summary>
        /// Shapes Thai while the player types so tone marks stay visible. The field then holds shaped text:
        /// read what was typed with <see cref="ThaiShaper.Unshape"/>(field.text).
        /// </summary>
        public static void EnableThaiShaping(this TMP_InputField field) =>
            field.onValueChanged.AddListener(value =>
            {
                // Shaping is one character for one, so the caret stays where it was.
                var shaped = ThaiShaper.Shape(ThaiShaper.Unshape(value));
                if (shaped != value)
                    field.SetTextWithoutNotify(shaped);
            });

        private static Glyph AddGlyph(TMP_FontAsset font, uint glyphIndex)
        {
            var args = new object[] { glyphIndex, null };
            return (bool)s_TryAddGlyph.Invoke(font, args) ? (Glyph)args[1] : null;
        }
    }
}
