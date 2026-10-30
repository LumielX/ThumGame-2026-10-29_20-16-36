using NUnit.Framework;
using SpiritMovers.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace SpiritMovers.Tests
{
    public class ThaiFontSupportTests
    {
        private const string MaliSdf = "Assets/_Game/Fonts/Mali/Mali-Regular SDF.asset";
        private const string MaliMap = "Assets/_Game/Fonts/Mali/Mali-Regular ThaiGlyphMap.asset";

        [Test]
        public void Register_OnFontAssetNotYetUsedByAnyText_BindsAllAlternates()
        {
            // Regression: in a player, Register ran in Awake before any TMP text had touched the font and
            // TMP's glyph lookup was still null (NullReferenceException inside TryAddGlyphInternal).
            // A copy has only the serialized data, like a font asset freshly loaded in a player.
            var font = Object.Instantiate(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MaliSdf));
            var map = AssetDatabase.LoadAssetAtPath<ThaiGlyphMap>(MaliMap);
            // Not destroyed: TMP would also destroy the atlas textures this copy shares with the real asset.
            Assert.AreEqual(20, ThaiFontSupport.Register(font, map));
            Assert.IsTrue(font.characterLookupTable.ContainsKey(0xF700), "raised mai ek (PUA U+F700) not registered");
        }
    }
}
