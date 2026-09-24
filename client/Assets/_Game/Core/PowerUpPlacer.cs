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

        private static readonly PowerUpKind[] ClassicTable =
        {
            PowerUpKind.Kick, PowerUpKind.Kick,
            PowerUpKind.Remote,
            PowerUpKind.Skull
        };

        public const int ClassicItemPercent = 12;

        public static Dictionary<GridPos, PowerUpKind> Place(Arena arena, int dropPercent, uint seed)
        {
            return Place(arena, dropPercent, seed, false);
        }

        public static Dictionary<GridPos, PowerUpKind> Place(Arena arena, int dropPercent, uint seed, bool classicItems)
        {
            var hidden = new Dictionary<GridPos, PowerUpKind>();
            if (dropPercent <= 0)
            {
                return hidden;
            }

            // A stream of its own, so adding or removing a soft block from the arena
            // generator does not shuffle every pickup in the match.
            var random = new DeterministicRandom(seed ^ 0x5BF03635u);
            var classic = new DeterministicRandom(seed ^ 0x2C1B3C6Du);

            for (int y = 0; y < arena.Height; y++)
            {
                for (int x = 0; x < arena.Width; x++)
                {
                    var tile = new GridPos(x, y);
                    if (!Tiles.CanBeDestroyed(arena[tile]))
                    {
                        continue;
                    }

                    if (random.NextInt(100) < dropPercent)
                    {
                        hidden[tile] = Table[random.NextInt(Table.Length)];
                    }
                    else if (classicItems && classic.NextInt(100) < ClassicItemPercent)
                    {
                        hidden[tile] = ClassicTable[classic.NextInt(ClassicTable.Length)];
                    }
                }
            }

            return hidden;
        }
    }
}
