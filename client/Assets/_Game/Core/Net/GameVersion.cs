using System;

namespace Blastlands.Core.Net
{
    // A released version of the game, as the lobby publishes it.
    //
    // Three numeric components, because that is what FerrFlow's ShortCalVer produces
    // (26.9.1) and what the lobby's own ClientVersion parses.
    //
    // This one is deliberately the stricter of the two, and the difference is worth
    // knowing rather than assuming away. The lobby parses each component with
    // u32::from_str, which accepts a leading plus and ten digits; this rejects both. So
    // "+26.9.1" is refused here and accepted there.
    //
    // Every gap runs the same harmless way: anything this refuses falls to Unknown, the
    // client plays, and the server gate still decides who gets into a match. Strictness
    // here costs a message the player would not have acted on. The direction that would
    // hurt is the opposite one, accepting something the lobby refuses, which is a client
    // that believes it may play and is turned away at the door.
    public readonly struct GameVersion : IEquatable<GameVersion>, IComparable<GameVersion>
    {
        // Thrown rather than clamped. TryParse cannot produce a negative, so this only
        // ever fires on a programming error, and clamping one to zero hides it in the
        // expensive direction: 0.9.0 compares as older than everything, so a mistyped
        // component would refuse a player rather than raise anything.
        public GameVersion(int major, int minor, int patch)
        {
            if (major < 0 || minor < 0 || patch < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(major), "a version component cannot be negative");
            }

            Major = major;
            Minor = minor;
            Patch = patch;
        }

        public int Major { get; }

        public int Minor { get; }

        public int Patch { get; }

        // Refused rather than guessed at. A version that does not parse is a client that
        // cannot say what it is, and the safe reading of that is not "probably current".
        public static bool TryParse(string text, out GameVersion version)
        {
            version = default;

            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            string[] parts = text.Trim().Split('.');
            if (parts.Length != 3)
            {
                return false;
            }

            if (!TryComponent(parts[0], out int major)
                || !TryComponent(parts[1], out int minor)
                || !TryComponent(parts[2], out int patch))
            {
                return false;
            }

            version = new GameVersion(major, minor, patch);
            return true;
        }

        // Parsed by hand rather than with int.Parse, which accepts a leading sign, and
        // with the invariant culture implied because only digits are allowed through.
        private static bool TryComponent(string text, out int value)
        {
            value = 0;

            if (string.IsNullOrEmpty(text) || text.Length > 9)
            {
                return false;
            }

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c < '0' || c > '9')
                {
                    return false;
                }

                value = (value * 10) + (c - '0');
            }

            return true;
        }

        public int CompareTo(GameVersion other)
        {
            if (Major != other.Major)
            {
                return Major.CompareTo(other.Major);
            }

            return Minor != other.Minor ? Minor.CompareTo(other.Minor) : Patch.CompareTo(other.Patch);
        }

        public bool Equals(GameVersion other)
        {
            return Major == other.Major && Minor == other.Minor && Patch == other.Patch;
        }

        public override bool Equals(object obj)
        {
            return obj is GameVersion other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Major * 73856093) ^ (Minor * 19349663) ^ (Patch * 83492791);
        }

        public override string ToString()
        {
            return Major + "." + Minor + "." + Patch;
        }

        public static bool operator <(GameVersion left, GameVersion right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator >(GameVersion left, GameVersion right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator <=(GameVersion left, GameVersion right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >=(GameVersion left, GameVersion right)
        {
            return left.CompareTo(right) >= 0;
        }

        public static bool operator ==(GameVersion left, GameVersion right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GameVersion left, GameVersion right)
        {
            return !left.Equals(right);
        }
    }
}
