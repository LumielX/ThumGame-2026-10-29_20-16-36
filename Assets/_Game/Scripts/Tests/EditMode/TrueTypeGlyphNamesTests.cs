using System.IO;
using NUnit.Framework;
using SpiritMovers.Core.Text;

namespace SpiritMovers.Tests
{
    public class TrueTypeGlyphNamesTests
    {
        private const string MaliRegular = "Assets/_Game/Fonts/Mali/Mali-Regular.ttf";

        [Test]
        public void Read_FindsThaiAlternateGlyphsInMali()
        {
            var names = TrueTypeGlyphNames.Read(File.ReadAllBytes(MaliRegular));

            // Indices verified against the font's post/glyf tables during M2 (docs/M2_REPORT.md).
            Assert.AreEqual(731, names["uni0E48"]);
            Assert.AreEqual(732, names["uni0E48.small"]);
            Assert.AreEqual(733, names["uni0E48.narrow"]);
            Assert.AreEqual(751, names["uni0E35.narrow"]);
        }

        [Test]
        public void Read_InvalidData_ReturnsEmpty()
        {
            Assert.IsEmpty(TrueTypeGlyphNames.Read(null));
            Assert.IsEmpty(TrueTypeGlyphNames.Read(new byte[] { 0, 1, 0, 0 }));
        }
    }
}
