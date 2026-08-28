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

        // A clump is a few tiles across: big enough to break a sight line and to be worth
        // walking around, small enough that several of them read as an arena rather than
        // as a maze.
        private const int MinClump = 3;
        private const int MaxClump = 9;

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

            if (settings.Board == BoardKind.Lattice)
            {
                FillClassic(arena);
            }
            else
            {
                bool[] land = IslandShape.Carve(settings.Width, settings.Height, settings.Island, random);
                FillGround(arena, land);
            }

            IReadOnlyList<GridPos> spawns = ChooseSpawns(arena);
            HashSet<GridPos> reserved = ReserveSpawns(arena, spawns);
            if (settings.Board == BoardKind.Lattice)
            {
                SprinkleCover(arena, reserved, settings.SoftBlockPercent, random);
            }
            else
            {
                ScatterCover(arena, reserved, settings.SoftBlockPercent, settings.BushPercent, random);
            }

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

        // A border ring and a pillar on every even/even coordinate. The lattice is the
        // readability trick a checkerboard game is built on: it gives the board a grain,
        // and every corridor it leaves is exactly one tile wide.
        private static void FillClassic(Arena arena)
        {
            for (int y = 0; y < arena.Height; y++)
            {
                for (int x = 0; x < arena.Width; x++)
                {
                    var tile = new GridPos(x, y);
                    bool border = x == 0 || y == 0 || x == arena.Width - 1 || y == arena.Height - 1;
                    bool pillar = x % 2 == 0 && y % 2 == 0;
                    arena[tile] = border || pillar ? TileKind.HardBlock : TileKind.Floor;
                }
            }
        }

        // A roll per tile, which is the even sprinkle Arena grew out of. Here it is the
        // right answer rather than the lazy one: with a pillar every other tile the board
        // already has its structure, and the soft blocks are the part you dig through.
        private static void SprinkleCover(
            Arena arena, HashSet<GridPos> reserved, int coverPercent, DeterministicRandom random)
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

                    if (random.NextInt(100) < coverPercent)
                    {
                        arena[tile] = TileKind.SoftBlock;
                    }
                }
            }
        }

        private static void FillGround(Arena arena, bool[] land)
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

                    // No border ring, and no pillar lattice either. The coast is the edge
                    // of the world, and nothing on the island is permanent: everything a
                    // player meets can be blown up. See #120.
                    arena[tile] = TileKind.Floor;
                }
            }
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

        // Cover in clumps rather than a roll per tile. Uniform noise at three quarters
        // gave the same texture everywhere: nowhere open enough to fight across and
        // nowhere dense enough to hide in. Clumps make both, and which one you are
        // standing in becomes a thing you chose.
        private static void ScatterCover(
            Arena arena, HashSet<GridPos> reserved, int coverPercent, int bushPercent, DeterministicRandom random)
        {
            List<GridPos> open = OpenFloor(arena, reserved);
            if (coverPercent <= 0 || open.Count == 0)
            {
                return;
            }

            int target = open.Count * coverPercent / 100;
            int placed = 0;

            // A clump can land entirely on ground another clump already took, which
            // places nothing and would spin forever against the target.
            int attempts = 0;
            int limit = open.Count * 4;

            while (placed < target && attempts < limit)
            {
                attempts++;
                GridPos start = open[random.NextInt(open.Count)];
                int size = MinClump + random.NextInt(MaxClump - MinClump + 1);
                placed += GrowClump(arena, reserved, start, size, bushPercent, random);
            }
        }

        // Grown by taking a random tile off the frontier rather than the nearest one, so
        // the shape comes out ragged instead of as a diamond.
        private static int GrowClump(
            Arena arena,
            HashSet<GridPos> reserved,
            GridPos start,
            int size,
            int bushPercent,
            DeterministicRandom random)
        {
            var frontier = new List<GridPos> { start };
            int placed = 0;

            while (placed < size && frontier.Count > 0)
            {
                int index = random.NextInt(frontier.Count);
                GridPos tile = frontier[index];
                frontier.RemoveAt(index);

                if (!arena.Contains(tile) || arena[tile] != TileKind.Floor || reserved.Contains(tile))
                {
                    continue;
                }

                arena[tile] = random.NextInt(100) < bushPercent ? TileKind.Bush : TileKind.SoftBlock;
                placed++;

                for (int i = 0; i < Neighbours.Length; i++)
                {
                    frontier.Add(tile.Offset(Neighbours[i].X, Neighbours[i].Y));
                }
            }

            return placed;
        }

        private static List<GridPos> OpenFloor(Arena arena, HashSet<GridPos> reserved)
        {
            var open = new List<GridPos>();

            for (int y = 0; y < arena.Height; y++)
            {
                for (int x = 0; x < arena.Width; x++)
                {
                    var tile = new GridPos(x, y);
                    if (arena[tile] == TileKind.Floor && !reserved.Contains(tile))
                    {
                        open.Add(tile);
                    }
                }
            }

            return open;
        }
    }
}
