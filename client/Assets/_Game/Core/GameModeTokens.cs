using System;
using System.Collections.Generic;

namespace Blastlands.Core
{
    public static class GameModeTokens
    {
        private static readonly Dictionary<string, GameMode> ByToken =
            new Dictionary<string, GameMode>(StringComparer.Ordinal)
            {
                { "arena", GameMode.Arena },
                { "classic", GameMode.Classic },
                { "classic_blinded", GameMode.ClassicBlinded }
            };

        public static IReadOnlyList<GameMode> All { get; } =
            new[] { GameMode.Arena, GameMode.Classic, GameMode.ClassicBlinded };

        public static bool TryRead(string token, out GameMode mode)
        {
            mode = GameMode.Arena;
            return token != null && ByToken.TryGetValue(token, out mode);
        }

        public static string Write(GameMode mode)
        {
            foreach (KeyValuePair<string, GameMode> entry in ByToken)
            {
                if (entry.Value == mode)
                {
                    return entry.Key;
                }
            }

            return null;
        }

        public static GameMode Step(GameMode from, int delta)
        {
            int at = 0;
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i] == from)
                {
                    at = i;
                }
            }

            int next = (at + delta) % All.Count;
            return All[next < 0 ? next + All.Count : next];
        }
    }
}
