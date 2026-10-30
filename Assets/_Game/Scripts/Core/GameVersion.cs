using System;

namespace SpiritMovers.Core
{
    /// <summary>
    /// Semantic game version (major.minor.patch). Players can only share a session
    /// when their builds report the exact same version (GDD 9, D-008).
    /// </summary>
    public readonly struct GameVersion : IEquatable<GameVersion>
    {
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }

        public GameVersion(int major, int minor, int patch)
        {
            if (major < 0 || minor < 0 || patch < 0)
                throw new ArgumentOutOfRangeException(nameof(major), "Version parts must be non-negative.");
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        public static bool TryParse(string text, out GameVersion version)
        {
            version = default;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var parts = text.Trim().Split('.');
            if (parts.Length != 3)
                return false;

            if (!int.TryParse(parts[0], out var major) || major < 0 ||
                !int.TryParse(parts[1], out var minor) || minor < 0 ||
                !int.TryParse(parts[2], out var patch) || patch < 0)
                return false;

            version = new GameVersion(major, minor, patch);
            return true;
        }

        public static GameVersion Parse(string text)
        {
            if (!TryParse(text, out var version))
                throw new FormatException($"'{text}' is not a valid major.minor.patch version.");
            return version;
        }

        /// <summary>True when two builds may play in the same session.</summary>
        public bool CanPlayWith(GameVersion other) => Equals(other);

        public bool Equals(GameVersion other) =>
            Major == other.Major && Minor == other.Minor && Patch == other.Patch;

        public override bool Equals(object obj) => obj is GameVersion other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch);

        public override string ToString() => $"{Major}.{Minor}.{Patch}";

        public static bool operator ==(GameVersion left, GameVersion right) => left.Equals(right);

        public static bool operator !=(GameVersion left, GameVersion right) => !left.Equals(right);
    }
}
