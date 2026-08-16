using System.Collections.Generic;

namespace Blastlands.Core
{
    public static class ArenaGenerator
    {
        // How far the spawn pocket is cleared along each axis. It has to exceed the
        // starting fire range, or a player sealed into their own corner cannot open it:
        // the only way out is to bomb, and their own blast covers the whole pocket.
        //
        // One tile was enough while blasts were crosses. A disc of radius two swallows a
        // three-tile L whole, and the result was bots standing armed and idle for a whole
        // match because every bomb they considered was correctly refused as suicide.
        private const int SpawnClearance = 3;

        private static readonly GridPos[] Neighbours =
        {
            new GridPos(1, 0),
            new GridPos(-1, 0),
            new GridPos(0, 1),
            new GridPos(0, -1)
        };

        public static GeneratedArena Generate(ArenaSettings settings, uint seed)
        {
            var random = new DeterministicRandom(seed);
            var arena = new Arena(settings.Width, settings.Height);

            bool[] land = IslandShape.Carve(settings.Width, settings.Height, settings.Island, random);
            FillStructure(arena, land);
            DropUnreachableGround(arena);

            IReadOnlyList<GridPos> spawns = ChooseSpawns(arena);
            HashSet<GridPos> reserved = ReserveSpawns(arena, spawns);
            ScatterSoftBlocks(arena, reserved, settings.SoftBlockPercent, settings.BushPercent, random);

            return new GeneratedArena(arena, spawns);
        }

        // The corners the rectangle used to hand out, pulled onto the island.
        //
        // A coastline decides where its own corners are, so a nominal position is only a
        // direction to look in: whichever standable tile lies nearest it is the spawn.
        // Nearest by walking distance rather than as the crow flies, since the closest
        // tile across a bay is not the closest tile to reach.
        public static IReadOnlyList<GridPos> ChooseSpawns(Arena arena)
        {
            var spawns = new List<GridPos>();
            var taken = new HashSet<GridPos>();

            foreach (GridPos nominal in NominalSpawns(arena.Width, arena.Height))
            {
                GridPos found = NearestStandable(arena, nominal, taken);
                if (found.X >= 0)
                {
                    spawns.Add(found);
                    taken.Add(found);
                }
            }

            return spawns;
        }

        private static IEnumerable<GridPos> NominalSpawns(int width, int height)
        {
            int right = width - 2;
            int bottom = height - 2;
            int midX = width / 2;
            int midY = height / 2;

            yield return new GridPos(1, 1);
            yield return new GridPos(right, 1);
            yield return new GridPos(1, bottom);
            yield return new GridPos(right, bottom);
            yield return new GridPos(midX, 1);
            yield return new GridPos(midX, bottom);
            yield return new GridPos(1, midY);
            yield return new GridPos(right, midY);
        }

        // Breadth-first over every tile rather than over walkable ones: the nominal
        // position is usually out over the water, and a search that refuses to cross the
        // void never reaches the island at all.
        private static GridPos NearestStandable(Arena arena, GridPos from, HashSet<GridPos> taken)
        {
            var seen = new HashSet<GridPos> { from };
            var queue = new Queue<GridPos>();
            queue.Enqueue(from);

            while (queue.Count > 0)
            {
                GridPos current = queue.Dequeue();

                if (arena.Contains(current) && Tiles.CanBeStoodOn(arena[current]) && !taken.Contains(current))
                {
                    return current;
                }

                for (int i = 0; i < Neighbours.Length; i++)
                {
                    GridPos next = current.Offset(Neighbours[i].X, Neighbours[i].Y);
                    if (arena.Contains(next) && seen.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            return new GridPos(-1, -1);
        }

        private static void FillStructure(Arena arena, bool[] land)
        {
            for (int y = 0; y < arena.Height; y++)
            {
                for (int x = 0; x < arena.Width; x++)
                {
                    var tile = new GridPos(x, y);

                    if (!IslandShape.IsLand(land, arena.Width, arena.Height, tile))
                    {
                        arena[tile] = TileKind.Void;
                        continue;
                    }

                    // No border ring any more: the coast is the edge of the world, and a
                    // wall around an island would only hide the drop the island is for.
                    arena[tile] = x % 2 == 0 && y % 2 == 0 ? TileKind.HardBlock : TileKind.Floor;
                }
            }
        }

        // The island flood guaranteed the ground was one piece. The pillar lattice is laid
        // on top of it afterwards, and on an irregular coast a pillar and a bay between
        // them can fence off a tile or two that nothing can ever reach.
        //
        // On the rectangle this was impossible: the border was a uniform ring and the
        // lattice sat on even coordinates inside it, so every gap led somewhere. The
        // island removed that guarantee without removing the assumption, and the arena
        // quietly grew pockets holding a hidden power-up nobody could collect.
        //
        // Anything the walk cannot reach is not part of the island, so it is dropped
        // rather than patched: carving a corridor to it would move a pillar the lattice
        // is entitled to have.
        private static void DropUnreachableGround(Arena arena)
        {
            GridPos start = NearestStandable(arena, Centre(arena), new HashSet<GridPos>());
            if (start.X < 0)
            {
                return;
            }

            var reached = new HashSet<GridPos> { start };
            var queue = new Queue<GridPos>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                GridPos current = queue.Dequeue();

                for (int i = 0; i < Neighbours.Length; i++)
                {
                    GridPos next = current.Offset(Neighbours[i].X, Neighbours[i].Y);
                    if (!arena.Contains(next) || arena[next] == TileKind.HardBlock || arena[next] == TileKind.Void)
                    {
                        continue;
                    }

                    if (reached.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            for (int y = 0; y < arena.Height; y++)
            {
                for (int x = 0; x < arena.Width; x++)
                {
                    var tile = new GridPos(x, y);
                    if (arena[tile] != TileKind.Void && arena[tile] != TileKind.HardBlock && !reached.Contains(tile))
                    {
                        arena[tile] = TileKind.Void;
                    }
                }
            }

            DropStrandedPillars(arena);
        }

        // A pillar left standing with nothing but water around it is a rock in the sea,
        // not part of the board, and it would still be drawn as one.
        private static void DropStrandedPillars(Arena arena)
        {
            for (int y = 0; y < arena.Height; y++)
            {
                for (int x = 0; x < arena.Width; x++)
                {
                    var tile = new GridPos(x, y);
                    if (arena[tile] != TileKind.HardBlock)
                    {
                        continue;
                    }

                    bool touchesGround = false;
                    for (int i = 0; i < Neighbours.Length && !touchesGround; i++)
                    {
                        GridPos next = tile.Offset(Neighbours[i].X, Neighbours[i].Y);
                        touchesGround = arena.Contains(next) && arena[next] != TileKind.Void;
                    }

                    if (!touchesGround)
                    {
                        arena[tile] = TileKind.Void;
                    }
                }
            }
        }

        private static GridPos Centre(Arena arena)
        {
            return new GridPos(arena.Width / 2, arena.Height / 2);
        }

        private static HashSet<GridPos> ReserveSpawns(Arena arena, IReadOnlyList<GridPos> spawns)
        {
            var reserved = new HashSet<GridPos>();

            for (int i = 0; i < spawns.Count; i++)
            {
                GridPos spawn = spawns[i];
                reserved.Add(spawn);
                arena[spawn] = TileKind.Floor;

                foreach (GridPos direction in Neighbours)
                {
                    for (int step = 1; step <= SpawnClearance; step++)
                    {
                        GridPos tile = spawn.Offset(direction.X * step, direction.Y * step);
                        if (!arena.Contains(tile) || Tiles.BlocksMovement(arena[tile]))
                        {
                            break;
                        }

                        reserved.Add(tile);
                    }
                }
            }

            return reserved;
        }

        private static void ScatterSoftBlocks(
            Arena arena, HashSet<GridPos> reserved, int softBlockPercent, int bushPercent, DeterministicRandom random)
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
                        arena[tile] = random.NextInt(100) < bushPercent ? TileKind.Bush : TileKind.SoftBlock;
                    }
                }
            }
        }
    }
}
