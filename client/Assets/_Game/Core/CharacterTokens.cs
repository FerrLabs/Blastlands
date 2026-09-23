using System;
using System.Collections.Generic;

namespace Blastlands.Core
{
    public static class CharacterTokens
    {
        private static readonly Dictionary<string, CharacterKind> ByToken =
            new Dictionary<string, CharacterKind>(StringComparer.Ordinal)
            {
                { "demolisher", CharacterKind.Demolisher },
                { "runner", CharacterKind.Runner },
                { "grenadier", CharacterKind.Grenadier },
                { "sapper", CharacterKind.Sapper }
            };

        public static bool TryRead(string token, out CharacterKind character)
        {
            character = CharacterKind.None;
            return token != null && ByToken.TryGetValue(token, out character);
        }

        public static string Write(CharacterKind character)
        {
            foreach (KeyValuePair<string, CharacterKind> entry in ByToken)
            {
                if (entry.Value == character)
                {
                    return entry.Key;
                }
            }

            return null;
        }
    }
}
