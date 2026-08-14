using System.Collections.Generic;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    // A random arena is only worth anything if every one it can produce is playable.
    // These sweep many seeds rather than checking one, because a generator flaw shows
    // up in a small fraction of layouts and never in the one you happened to look at.
    public class ArenaConnectivityTests
    {
        private const int Seeds = 300;

        private static readonly GridPos[] Cardinals =
        {
            new GridPos(1, 0),
            new GridPos(-1, 0),
            new GridPos(0, 1),
            new GridPos(0, -1)
        };

        // Soft blocks are destructible, so they do not divide the arena: only the hard
        // lattice can. Flooding through them is what "reachable eventually" means.
        private static HashSet<GridPos> ReachableIgnoringSoftBlocks(Arena arena, GridPos from)
        {
            var seen = new HashSet<GridPos> { from };
            var pending = new Queue<GridPos>();
            pending.Enqueue(from);

            while (pending.Count > 0)
            {
                GridPos tile = pending.Dequeue();

                foreach (GridPos direction in Cardinals)
                {
                    GridPos next = tile.Offset(direction.X, direction.Y);
                    if (!arena.Contains(next) || arena[next] == TileKind.HardBlock || seen.Contains(next))
                    {
                        continue;
                    }

                    seen.Add(next);
                    pending.Enqueue(next);
                }
            }

            return seen;
        }

        private static int CountReachableTiles(Arena arena)
        {
            int count = 0;
            for (int y = 0; y < arena.Height; y++)
            {
                for (int x = 0; x < arena.Width; x++)
                {
                    if (arena[new GridPos(x, y)] != TileKind.HardBlock)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        [Test]
        public void EverySeedLetsEverySpawnReachEveryOther()
        {
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                Arena arena = ArenaGenerator.Generate(ArenaSettings.Default, seed);
                IReadOnlyList<GridPos> spawns = ArenaGenerator.SpawnPositions(arena.Width, arena.Height);
                HashSet<GridPos> reached = ReachableIgnoringSoftBlocks(arena, spawns[0]);

                for (int i = 1; i < spawns.Count; i++)
                {
                    Assert.That(reached, Has.Member(spawns[i]), $"seed {seed}: spawn {spawns[i]} is walled off");
                }
            }
        }

        [Test]
        public void EverySeedLeavesNoIsolatedPocket()
        {
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                Arena arena = ArenaGenerator.Generate(ArenaSettings.Default, seed);
                GridPos start = ArenaGenerator.SpawnPositions(arena.Width, arena.Height)[0];

                Assert.That(
                    ReachableIgnoringSoftBlocks(arena, start).Count,
                    Is.EqualTo(CountReachableTiles(arena)),
                    $"seed {seed}: part of the arena is sealed off");
            }
        }

        [Test]
        public void EverySeedKeepsEverySpawnAndItsEscapeRouteClear()
        {
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                Arena arena = ArenaGenerator.Generate(ArenaSettings.Default, seed);

                foreach (GridPos spawn in ArenaGenerator.SpawnPositions(arena.Width, arena.Height))
                {
                    Assert.That(arena[spawn], Is.EqualTo(TileKind.Floor), $"seed {seed}: spawn {spawn} is blocked");

                    int escapes = 0;
                    foreach (GridPos direction in Cardinals)
                    {
                        GridPos tile = spawn.Offset(direction.X, direction.Y);
                        if (arena.Contains(tile) && arena[tile] == TileKind.Floor)
                        {
                            escapes++;
                        }
                    }

                    Assert.That(escapes, Is.GreaterThan(0), $"seed {seed}: spawn {spawn} is bomb-locked at tick zero");
                }
            }
        }

        [Test]
        public void DifferentSeedsProduceDifferentLayouts()
        {
            var layouts = new HashSet<string>();

            for (uint seed = 1; seed <= 100; seed++)
            {
                Arena arena = ArenaGenerator.Generate(ArenaSettings.Default, seed);
                var builder = new System.Text.StringBuilder();

                for (int y = 0; y < arena.Height; y++)
                {
                    for (int x = 0; x < arena.Width; x++)
                    {
                        builder.Append((int)arena[new GridPos(x, y)]);
                    }
                }

                layouts.Add(builder.ToString());
            }

            // The guard this provides: a generator that silently ignores its seed still
            // passes every other test in this file.
            Assert.That(layouts.Count, Is.EqualTo(100), "seeds are producing duplicate arenas");
        }
    }
}
