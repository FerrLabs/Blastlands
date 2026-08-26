using System.Globalization;

namespace Blastlands.Core.Lobby
{
    // The same rule the lobby applies, so a player learns their name is no good while
    // they are typing it rather than after a round trip.
    //
    // Duplicating a server rule on the client is normally how the two drift apart. It is
    // worth it here because the alternative is a text box that looks fine until you press
    // a button, and the rule is small and stable. The server stays the authority: this
    // only ever refuses early, and never accepts anything the lobby would refuse.
    //
    // Mirrors server/crates/lobby/src/names.rs. Trim, then between 2 and 16 characters,
    // and nothing but letters, digits, space, hyphen and underscore. Counted in
    // characters rather than bytes, so an accented name is measured the way a person
    // would count it.
    public static class DisplayName
    {
        public const int MinLength = 2;
        public const int MaxLength = 16;

        public static string Clean(string raw)
        {
            return raw == null ? string.Empty : raw.Trim();
        }

        public static bool IsAcceptable(string raw)
        {
            string trimmed = Clean(raw);
            int length = CountCharacters(trimmed);

            if (length < MinLength || length > MaxLength)
            {
                return false;
            }

            var walker = StringInfo.GetTextElementEnumerator(trimmed);
            while (walker.MoveNext())
            {
                string element = walker.GetTextElement();
                for (int i = 0; i < element.Length; i++)
                {
                    if (!IsAllowed(element[i]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsAllowed(char c)
        {
            return char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_';
        }

        // Surrogate pairs count as one, which is what "characters, not bytes" means to a
        // player holding an emoji-adjacent alphabet.
        private static int CountCharacters(string value)
        {
            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsLowSurrogate(value[i]))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
