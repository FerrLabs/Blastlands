using System;
using System.Collections.Generic;

namespace Blastlands.Core
{
    public static class MatchFactory
    {
        public static MatchState Create(ArenaSettings arenaSettings, MatchSettings matchSettings, int playerCount, uint seed)
        {
            return Create(arenaSettings, matchSettings, playerCount, seed, _ => CharacterKind.None);
        }

        // Who sits in each seat. Ignored in a mode whose rules have no room for kits, so
        // a caller cannot hand Classic a character by passing one in.
        public static MatchState Create(
            ArenaSettings arenaSettings,
            MatchSettings matchSettings,
            int playerCount,
            uint seed,
            Func<int, CharacterKind> characterOf)
        {
            // Generated first, because the island decides where its own spawns are.
            GeneratedArena generated = ArenaGenerator.Generate(arenaSettings, seed, playerCount);
            Arena arena = generated.Arena;
            IReadOnlyList<GridPos> spawns = generated.Spawns;

            if (playerCount < 1 || playerCount > spawns.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(playerCount), playerCount, "Player count must be between 1 and " + spawns.Count + ".");
            }

            var state = new MatchState(arena, matchSettings, seed);

            foreach (KeyValuePair<GridPos, PowerUpKind> hidden
                in PowerUpPlacer.Place(arena, matchSettings.PowerUpDropPercent, seed, matchSettings.Rules.ClassicItems))
            {
                state.HidePowerUp(hidden.Key, hidden.Value);
            }

            for (int i = 0; i < playerCount; i++)
            {
                CharacterKind character = matchSettings.Rules.AllowsCharacters ? characterOf(i) : CharacterKind.None;
                state.AddPlayer(spawns[i], character);
            }

            // After the players, so seeding never drops a bomb onto a spawn.
            BombSpawner.Seed(state);

            return state;
        }
    }
}
