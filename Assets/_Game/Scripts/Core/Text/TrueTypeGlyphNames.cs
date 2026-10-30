using System;
using System.Collections.Generic;
using System.Text;

namespace SpiritMovers.Core.Text
{
    /// <summary>
    /// Reads glyph names from a TrueType/OpenType font's 'post' table (format 2.0), so alternate glyphs
    /// such as <c>uni0E48.small</c> can be found by name (D-035). Only the parts needed for that are parsed.
    /// </summary>
    public static class TrueTypeGlyphNames
    {
        /// <summary>Returns glyph name → glyph index. Empty when the font has no format 2.0 'post' table.</summary>
        public static Dictionary<string, int> Read(byte[] font)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            if (font == null || font.Length < 12)
                return result;

            var tableCount = ReadUInt16(font, 4);
            var postOffset = -1;
            var postLength = 0;
            for (var i = 0; i < tableCount; i++)
            {
                var record = 12 + i * 16;
                if (record + 16 > font.Length)
                    return result;
                if (Encoding.ASCII.GetString(font, record, 4) == "post")
                {
                    postOffset = (int)ReadUInt32(font, record + 8);
                    postLength = (int)ReadUInt32(font, record + 12);
                    break;
                }
            }

            if (postOffset < 0 || postOffset + 34 > font.Length || ReadUInt32(font, postOffset) != 0x00020000)
                return result;

            var glyphCount = ReadUInt16(font, postOffset + 32);
            var indexStart = postOffset + 34;
            var namesStart = indexStart + glyphCount * 2;
            var end = Math.Min(postOffset + postLength, font.Length);

            var customNames = new List<string>();
            for (var p = namesStart; p < end;)
            {
                var length = font[p];
                if (p + 1 + length > end)
                    break;
                customNames.Add(Encoding.ASCII.GetString(font, p + 1, length));
                p += 1 + length;
            }

            for (var glyph = 0; glyph < glyphCount; glyph++)
            {
                var nameIndex = ReadUInt16(font, indexStart + glyph * 2);
                // Indices below 258 refer to the standard Macintosh glyph set; Thai alternates are always custom names.
                if (nameIndex < 258)
                    continue;
                var custom = nameIndex - 258;
                if (custom < customNames.Count && !result.ContainsKey(customNames[custom]))
                    result.Add(customNames[custom], glyph);
            }

            return result;
        }

        private static int ReadUInt16(byte[] data, int offset) => (data[offset] << 8) | data[offset + 1];

        private static uint ReadUInt32(byte[] data, int offset) =>
            ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3];
    }
}
