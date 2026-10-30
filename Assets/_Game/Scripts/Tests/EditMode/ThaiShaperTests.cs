using System.Linq;
using NUnit.Framework;
using SpiritMovers.Core.Text;

namespace SpiritMovers.Tests
{
    public class ThaiShaperTests
    {
        private const char MaiEk = '่';
        private const char SaraII = 'ี';
        private const char SaraUu = 'ู';

        private static char Variant(char baseChar, string suffix) =>
            ThaiShaper.Variants.Single(v => v.Base == baseChar && v.GlyphSuffix == suffix).Pua;

        [TestCase("")]
        [TestCase("Spirit Movers 123")]
        [TestCase("ไม่")]     // tone on a normal consonant stays where it is
        [TestCase("น้ำ")]
        [TestCase("ผู้ใหญ่")]   // tone after a lower vowel on a normal consonant
        public void Shape_LeavesTextThatNeedsNoAdjustment(string text)
        {
            Assert.AreEqual(text, ThaiShaper.Shape(text));
        }

        [Test]
        public void Shape_RaisesToneAboveUpperVowel()
        {
            Assert.AreEqual("ก" + SaraII + Variant(MaiEk, ".small"), ThaiShaper.Shape("กี่"));
        }

        [Test]
        public void Shape_ShiftsToneLeftOnTallConsonant()
        {
            // ปู่: tone after a lower vowel on ป moves left to clear the ascender.
            Assert.AreEqual("ป" + SaraUu + Variant(MaiEk, ".narrow"), ThaiShaper.Shape("ปู่"));
        }

        [Test]
        public void Shape_ShiftsUpperVowelLeftAndRaisesToneOnTallConsonant()
        {
            Assert.AreEqual("ป" + Variant(SaraII, ".narrow") + Variant(MaiEk, ".small"), ThaiShaper.Shape("ปี่"));
        }

        [Test]
        public void Shape_LowersLowerVowelUnderDescenderConsonant()
        {
            Assert.AreEqual("ฎ" + Variant('ุ', ".small"), ThaiShaper.Shape("ฎุ"));
        }

        [Test]
        public void Shape_ResetsStateAtEachConsonant()
        {
            // ญี่ปุ่น = ญ ี ่ | ป ุ ่ | น
            var expected = "ญ" + SaraII + Variant(MaiEk, ".small") + "ป" + 'ุ' + Variant(MaiEk, ".narrow") + "น";
            Assert.AreEqual(expected, ThaiShaper.Shape("ญี่ปุ่น"));
        }

        [Test]
        public void Variants_HaveUniquePrivateUseCodePoints()
        {
            var codes = ThaiShaper.Variants.Select(v => v.Pua).ToArray();
            Assert.AreEqual(codes.Length, codes.Distinct().Count());
            Assert.IsTrue(codes.All(c => c >= '' && c <= ''));
        }

        [TestCase("ปู่ฟื้นน้ำ ญาติผู้ใหญ่")]
        [TestCase("ฎีกา ปฏิทิน กุ้ง ชื่อ")]
        [TestCase("Spirit Movers 123")]
        public void Unshape_RestoresOriginalText(string text)
        {
            Assert.AreEqual(text, ThaiShaper.Unshape(ThaiShaper.Shape(text)));
        }

        [Test]
        public void ShapeOfUnshape_ShapesTextTypedAfterAlreadyShapedPart()
        {
            // An input field keeps shaped text; the next keystroke appends a plain mark to it.
            var typedSoFar = ThaiShaper.Shape("ฟื") + "้";
            Assert.AreEqual(ThaiShaper.Shape("ฟื้"), ThaiShaper.Shape(ThaiShaper.Unshape(typedSoFar)));
        }

        [Test]
        public void Variant_GlyphNameFollowsUniNaming()
        {
            Assert.AreEqual("uni0E48.small", ThaiShaper.Variants.First(v => v.Base == MaiEk && v.GlyphSuffix == ".small").GlyphName);
        }
    }
}
