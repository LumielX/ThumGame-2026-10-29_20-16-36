using System.Text;
using SpiritMovers.Core.Save;

namespace SpiritMovers.Core.Session
{
    /// <summary>
    /// What a joining player sends with the connection request: the game version (TECHNICAL_DESIGN 7) and the name they
    /// chose, as "version\nname" in UTF-8. The host cleans the name again — it never trusts what a client sends.
    /// </summary>
    public static class ConnectionPayload
    {
        public static string Write(string version, string name) => $"{version}\n{PlayerName.Clean(name)}";

        /// <summary>False unless the text starts with a valid version; a payload with no name (M4A builds) reads as "".</summary>
        public static bool TryRead(string text, out string version, out string name)
        {
            version = "";
            name = "";
            if (string.IsNullOrEmpty(text))
                return false;
            var newline = text.IndexOf('\n');
            var head = newline < 0 ? text : text.Substring(0, newline);
            if (!GameVersion.TryParse(head, out _))
                return false;
            version = head;
            name = newline < 0 ? "" : PlayerName.Clean(text.Substring(newline + 1));
            return true;
        }

        public static bool TryRead(byte[] payload, out string version, out string name)
        {
            version = "";
            name = "";
            // Invalid UTF-8 decodes to replacement characters, so a garbage payload fails the version check.
            return payload != null && payload.Length > 0 && TryRead(Encoding.UTF8.GetString(payload), out version, out name);
        }
    }
}
