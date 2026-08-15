using System;
using System.Collections.Generic;

namespace Blastlands.Core
{
    public static class MatchFactory
    {
        public static MatchState Create(ArenaSettings arenaSettings, MatchSettings matchSettings, int playerCount, uint seed)
        {
            IReadOnlyList<GridPos> spawns = ArenaGenerator.SpawnPositions(arenaSettings.Width, arenaSettings.Height);

            if (playerCount < 1 || playerCount > spawns.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(playerCount), playerCount, "Player count must be between 1 and " + spawns.Count + ".");
            }

            Arena arena = ArenaGenerator.Generate(arenaSettings, seed);
            var state = new MatchState(arena, matchSettings, seed);

            foreach (KeyValuePair<GridPos, PowerUpKind> hidden
                in PowerUpPlacer.Place(arena, matchSettings.PowerUpDropPercent, seed))
            {
                state.HidePowerUp(hidden.Key, hidden.Value);
            }

            for (int i = 0; i < playerCount; i++)
            {
                state.AddPlayer(spawns[i]);
            }

            // After the players, so seeding never drops a bomb onto a spawn.
            BombSpawner.Seed(state);

            return state;
        }
    }
}
