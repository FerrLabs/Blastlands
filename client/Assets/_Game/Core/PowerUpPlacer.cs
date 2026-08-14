using System.Collections.Generic;

namespace Blastlands.Core
{
    // Decides what hides under which soft block, once, when the match is created.
    //
    // Rolling at destruction time instead would mean the seed no longer describes the
    // whole match: two players blowing up the same blocks in a different order would
    // find different pickups.
    public static class PowerUpPlacer
    {
        // Weights, not an even split. Bomb and fire are the bread and butter of a round;
        // the bomb kinds are meant to feel like a find.
        private static readonly PowerUpKind[] Table =
        {
            PowerUpKind.BombUp, PowerUpKind.BombUp, PowerUpKind.BombUp, PowerUpKind.BombUp,
            PowerUpKind.FireUp, PowerUpKind.FireUp, PowerUpKind.FireUp, PowerUpKind.FireUp,
            PowerUpKind.SpeedUp, PowerUpKind.SpeedUp,
            PowerUpKind.PierceBomb,
            PowerUpKind.ClusterBomb
        };

        public static Dictionary<GridPos, PowerUpKind> Place(Arena arena, int dropPercent, uint seed)
        {
            var hidden = new Dictionary<GridPos, PowerUpKind>();
            if (dropPercent <= 0)
            {
                return hidden;
            }

            // A stream of its own, so adding or removing a soft block from the arena
            // generator does not shuffle every pickup in the match.
            var random = new DeterministicRandom(seed ^ 0x5BF03635u);

            for (int y = 0; y < arena.Height; y++)
            {
                for (int x = 0; x < arena.Width; x++)
                {
                    var tile = new GridPos(x, y);
                    if (arena[tile] != TileKind.SoftBlock)
                    {
                        continue;
                    }

                    if (random.NextInt(100) < dropPercent)
                    {
                        hidden[tile] = Table[random.NextInt(Table.Length)];
                    }
                }
            }

            return hidden;
        }
    }
}
