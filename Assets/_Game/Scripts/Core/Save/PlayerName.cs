using System.Globalization;
using System.Text;

namespace SpiritMovers.Core.Save
{
    /// <summary>
    /// The name a player chooses — the only personal data the game keeps (TECHNICAL_DESIGN 9/19). Letters (Thai vowels
    /// and tone marks included), digits, single spaces and "-_." only, at most <see cref="MaxLength"/> characters.
    /// </summary>
    public static class PlayerName
    {
        public const int MaxLength = 20;

        /// <summary>The name made safe to show and send; "" when nothing usable is left.</summary>
        public static string Clean(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "";
            var text = new StringBuilder(raw.Length);
            foreach (var c in raw)
            {
                if (c == ' ')
                {
                    if (text.Length > 0 && text[text.Length - 1] != ' ')
                        text.Append(' ');
                }
                else if (IsMark(c))
                {
                    // A vowel or tone mark needs the letter before it (emoji and spaces are not letters).
                    if (text.Length > 0 && text[text.Length - 1] != ' ')
                        text.Append(c);
                }
                else if (IsKept(c))
                {
                    text.Append(c);
                }
            }
            var cut = text.Length;
            if (cut > MaxLength)
            {
                cut = MaxLength;
                // Never keep a letter without the marks that sit on it.
                while (cut > 0 && IsMark(text[cut]))
                    cut--;
            }
            // A leading vowel (เ แ โ ใ ไ) belongs to the consonant after it.
            while (cut > 0 && (text[cut - 1] == ' ' || IsLeadingVowel(text[cut - 1])))
                cut--;
            return text.ToString(0, cut);
        }

        /// <summary>The first-run name, "พนักงาน" and four digits from <paramref name="seed"/> (⚙ M4B plan).</summary>
        public static string Default(int seed) => $"พนักงาน {1000 + (int)(((long)seed % 9000 + 9000) % 9000)}";

        private static bool IsKept(char c)
        {
            if (char.IsSurrogate(c))
                return false;
            switch (char.GetUnicodeCategory(c))
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.DecimalDigitNumber:
                    return true;
                default:
                    return c == '-' || c == '_' || c == '.';
            }
        }

        private static bool IsMark(char c)
        {
            var category = char.GetUnicodeCategory(c);
            return category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.SpacingCombiningMark;
        }

        private static bool IsLeadingVowel(char c) => c >= 'เ' && c <= 'ไ';
    }
}
