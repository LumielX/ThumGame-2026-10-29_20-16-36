using System.Globalization;
using System.Text;

namespace SpiritMovers.Core.Voice
{
    /// <summary>The six quick messages for players without a microphone (GDD 10, D-013).</summary>
    public enum QuickMessage
    {
        NeedHands,
        GhostHere,
        Watch,
        ToVan,
        Okay,
        No,
    }

    /// <summary>
    /// Text chat (D-013 J3): what is sent and what is shown is cleaned the same way — no control characters or emoji
    /// (the font has none), one space between words, at most <see cref="MaxLength"/> characters.
    /// </summary>
    public static class ChatText
    {
        public const int MaxLength = 120;

        public static string Clean(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "";
            var text = new StringBuilder(raw.Length);
            foreach (var c in raw)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (text.Length > 0 && text[text.Length - 1] != ' ')
                        text.Append(' ');
                }
                else if (IsMark(c))
                {
                    // A vowel or tone mark needs the letter before it.
                    if (text.Length > 0 && text[text.Length - 1] != ' ')
                        text.Append(c);
                }
                else if (!char.IsControl(c) && !char.IsSurrogate(c) &&
                         char.GetUnicodeCategory(c) != UnicodeCategory.Format)
                {
                    text.Append(c);
                }
            }
            var cut = text.Length;
            if (cut > MaxLength)
            {
                cut = MaxLength;
                while (cut > 0 && IsMark(text[cut]))
                    cut--;
            }
            while (cut > 0 && text[cut - 1] == ' ')
                cut--;
            return text.ToString(0, cut);
        }

        public static string KeyFor(QuickMessage message) => message switch
        {
            QuickMessage.NeedHands => "quick.need_hands",
            QuickMessage.GhostHere => "quick.ghost_here",
            QuickMessage.Watch => "quick.watch",
            QuickMessage.ToVan => "quick.to_van",
            QuickMessage.Okay => "quick.okay",
            _ => "quick.no",
        };

        private static bool IsMark(char c)
        {
            var category = char.GetUnicodeCategory(c);
            return category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.SpacingCombiningMark;
        }
    }
}
