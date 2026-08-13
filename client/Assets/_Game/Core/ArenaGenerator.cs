using System.Collections.Generic;

namespace Blastlands.Core
{
    public static class ArenaGenerator
    {
        private static readonly GridPos[] Neighbours =
        {
            new GridPos(1, 0),
            new GridPos(-1, 0),
            new GridPos(0, 1),
            new GridPos(0, -1)
        };

        public static Arena Generate(ArenaSettings settings, uint seed)
        {
            var arena = new Arena(settings.Width, settings.Height);
            FillStructure(arena);
            HashSet<GridPos> reserved = ReserveSpawns(arena);
            ScatterSoftBlocks(arena, reserved, settings.SoftBlockPercent, new DeterministicRandom(seed));
            return arena;
        }

        public static IReadOnlyList<GridPos> SpawnPositions(int width, int height)
        {
            int right = width - 2;
            int bottom = height - 2;
            int midX = width / 2;
            int midY = height / 2;

            return new[]
            {
                new GridPos(1, 1),
                new GridPos(right, 1),
                new GridPos(1, bottom),
                new GridPos(right, bottom),
                new GridPos(midX, 1),
                new GridPos(midX, bottom),
                new GridPos(1, midY),
                new GridPos(right, midY)
            };
        }

        private static void FillStructure(Arena arena)
        {
            for (int y = 0; y < arena.Height; y++)
            {
                for (int x = 0; x < arena.Width; x++)
                {
                    bool border = x == 0 || y == 0 || x == arena.Width - 1 || y == arena.Height - 1;
                    bool pillar = x % 2 == 0 && y % 2 == 0;
                    arena[new GridPos(x, y)] = border || pillar ? TileKind.HardBlock : TileKind.Floor;
                }
            }
        }

        private static HashSet<GridPos> ReserveSpawns(Arena arena)
        {
            var reserved = new HashSet<GridPos>();

            foreach (GridPos spawn in SpawnPositions(arena.Width, arena.Height))
            {
                reserved.Add(spawn);

                foreach (GridPos direction in Neighbours)
                {
                    GridPos tile = spawn.Offset(direction.X, direction.Y);
                    if (arena.Contains(tile) && arena[tile] != TileKind.HardBlock)
                    {
                        reserved.Add(tile);
                    }
                }
            }

            return reserved;
        }

        private static void ScatterSoftBlocks(
            Arena arena, HashSet<GridPos> reserved, int softBlockPercent, DeterministicRandom random)
        {
            for (int y = 0; y < arena.Height; y++)
            {
                for (int x = 0; x < arena.Width; x++)
                {
                    var tile = new GridPos(x, y);
                    if (arena[tile] != TileKind.Floor || reserved.Contains(tile))
                    {
                        continue;
                    }

                    if (random.NextInt(100) < softBlockPercent)
                    {
                        arena[tile] = TileKind.SoftBlock;
                    }
                }
            }
        }
    }
}
