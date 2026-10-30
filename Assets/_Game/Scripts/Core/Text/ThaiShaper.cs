using System.Collections.Generic;
using System.Text;

namespace SpiritMovers.Core.Text
{
    /// <summary>
    /// Pre-shapes Thai text for renderers without contextual glyph substitution (TextMeshPro, D-035).
    /// Marks that must move are replaced with Private Use Area code points; the font setup binds each
    /// code point to the matching alternate glyph of the font (e.g. <c>uni0E48.small</c>).
    /// </summary>
    public static class ThaiShaper
    {
        /// <summary>A shaped mark: Private Use Area code point, original character and font glyph suffix.</summary>
        public readonly struct Variant
        {
            public readonly char Pua;
            public readonly char Base;
            public readonly string GlyphSuffix;

            public Variant(char pua, char baseChar, string glyphSuffix)
            {
                Pua = pua;
                Base = baseChar;
                GlyphSuffix = glyphSuffix;
            }

            /// <summary>Glyph name in fonts that follow the common "uniXXXX.suffix" naming.</summary>
            public string GlyphName => $"uni{(int)Base:X4}{GlyphSuffix}";
        }

        // Raised tone marks and thanthakhat, used above an upper vowel.
        private const char RaisedToneStart = '';
        // Tone marks and thanthakhat shifted left, used on tall consonants.
        private const char LeftToneStart = '';

        private static readonly char[] s_UpperVowels = { 'ั', 'ิ', 'ี', 'ึ', 'ื', '็', 'ํ' };
        private static readonly char[] s_LowerVowels = { 'ุ', 'ู', 'ฺ' };

        private static readonly Dictionary<char, char> s_LeftUpperVowel = new();
        private static readonly Dictionary<char, char> s_LoweredLowerVowel = new();
        private static readonly List<Variant> s_Variants = new();

        static ThaiShaper()
        {
            for (var i = 0; i < 5; i++)
            {
                var tone = (char)('่' + i);
                s_Variants.Add(new Variant((char)(RaisedToneStart + i), tone, ".small"));
                s_Variants.Add(new Variant((char)(LeftToneStart + i), tone, ".narrow"));
            }

            var pua = '';
            foreach (var vowel in s_UpperVowels)
            {
                s_LeftUpperVowel[vowel] = pua;
                s_Variants.Add(new Variant(pua++, vowel, ".narrow"));
            }

            foreach (var vowel in s_LowerVowels)
            {
                s_LoweredLowerVowel[vowel] = pua;
                s_Variants.Add(new Variant(pua++, vowel, ".small"));
            }
        }

        /// <summary>Every alternate form <see cref="Shape"/> can emit.</summary>
        public static IReadOnlyList<Variant> Variants => s_Variants;

        public static bool IsThaiConsonant(char c) => c >= 'ก' && c <= 'ฮ';

        /// <summary>Consonants whose ascender collides with marks placed at the default position.</summary>
        public static bool IsTallConsonant(char c) => c == 'ป' || c == 'ฝ' || c == 'ฟ' || c == 'ฬ';

        /// <summary>Consonants whose descender collides with lower vowels.</summary>
        public static bool IsDescenderConsonant(char c) => c == 'ฎ' || c == 'ฏ';

        public static bool IsToneMark(char c) => c >= '่' && c <= '์';

        public static bool IsUpperVowel(char c) => System.Array.IndexOf(s_UpperVowels, c) >= 0;

        public static bool IsLowerVowel(char c) => System.Array.IndexOf(s_LowerVowels, c) >= 0;

        /// <summary>Returns text with repositioned marks replaced by their Private Use Area forms.</summary>
        public static string Shape(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            StringBuilder result = null;
            var baseChar = '\0';
            var hasUpperMark = false;

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                var shaped = c;

                if (IsUpperVowel(c))
                {
                    if (IsTallConsonant(baseChar))
                        shaped = s_LeftUpperVowel[c];
                    hasUpperMark = true;
                }
                else if (IsToneMark(c))
                {
                    if (hasUpperMark)
                        shaped = (char)(RaisedToneStart + (c - '่'));
                    else if (IsTallConsonant(baseChar))
                        shaped = (char)(LeftToneStart + (c - '่'));
                    hasUpperMark = true;
                }
                else if (IsLowerVowel(c))
                {
                    if (IsDescenderConsonant(baseChar))
                        shaped = s_LoweredLowerVowel[c];
                }
                else
                {
                    baseChar = c;
                    hasUpperMark = false;
                }

                if (shaped != c)
                {
                    result ??= new StringBuilder(text, 0, i, text.Length);
                    result.Append(shaped);
                }
                else
                {
                    result?.Append(c);
                }
            }

            return result?.ToString() ?? text;
        }
    }
}
