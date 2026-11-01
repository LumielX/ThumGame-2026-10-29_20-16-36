using System;
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
        public const int MaxVoiceIdLength = 64;

        /// <summary>"version\nname\nvoiceId" — the voice id is the UGS player id voice chat knows the player by (M4C).</summary>
        public static string Write(string version, string name, string voiceId = "") =>
            $"{version}\n{PlayerName.Clean(name)}\n{CleanVoiceId(voiceId)}";

        public static bool TryRead(string text, out string version, out string name) => TryRead(text, out version, out name, out _);

        /// <summary>False unless the text starts with a valid version; missing lines (older builds) read as "".</summary>
        public static bool TryRead(string text, out string version, out string name, out string voiceId)
        {
            version = "";
            name = "";
            voiceId = "";
            if (string.IsNullOrEmpty(text))
                return false;
            var lines = text.Split(new[] { '\n' }, 3);
            if (!GameVersion.TryParse(lines[0], out _))
                return false;
            version = lines[0];
            name = lines.Length > 1 ? PlayerName.Clean(lines[1]) : "";
            voiceId = lines.Length > 2 ? CleanVoiceId(lines[2]) : "";
            return true;
        }

        public static bool TryRead(byte[] payload, out string version, out string name) => TryRead(payload, out version, out name, out _);

        public static bool TryRead(byte[] payload, out string version, out string name, out string voiceId)
        {
            version = "";
            name = "";
            voiceId = "";
            // Invalid UTF-8 decodes to replacement characters, so a garbage payload fails the version check.
            return payload != null && payload.Length > 0 && TryRead(Encoding.UTF8.GetString(payload), out version, out name, out voiceId);
        }

        /// <summary>Letters, digits, '-' and '_' only, at most 64 — what a UGS player id is made of.</summary>
        public static string CleanVoiceId(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "";
            var id = new StringBuilder(Math.Min(raw.Length, MaxVoiceIdLength));
            foreach (var c in raw)
            {
                if (id.Length == MaxVoiceIdLength)
                    break;
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_')
                    id.Append(c);
            }
            return id.ToString();
        }
    }
}
