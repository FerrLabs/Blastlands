using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class ArenaGeneratorTests
    {
        private static readonly GridPos[] Cardinals =
        {
            new GridPos(1, 0),
            new GridPos(-1, 0),
            new GridPos(0, 1),
            new GridPos(0, -1)
        };

        [Test]
        public void Generate_LeavesTheIslandClearOfTheBounds()
        {
            // The wall around the board is gone: the coast is the edge of the world now.
            // The island has to float clear of the bounds on every side, or it is a
            // rectangle with the corners taken off and the drop is invisible.
            Arena arena = ArenaGenerator.Generate(ArenaSettings.Default, 1u).Arena;

            for (int x = 0; x < arena.Width; x++)
            {
                Assert.That(arena[new GridPos(x, 0)], Is.EqualTo(TileKind.Void), $"top edge at {x}");
                Assert.That(arena[new GridPos(x, arena.Height - 1)], Is.EqualTo(TileKind.Void), $"bottom edge at {x}");
            }

            for (int y = 0; y < arena.Height; y++)
            {
                Assert.That(arena[new GridPos(0, y)], Is.EqualTo(TileKind.Void), $"left edge at {y}");
                Assert.That(arena[new GridPos(arena.Width - 1, y)], Is.EqualTo(TileKind.Void), $"right edge at {y}");
            }
        }

        [Test]
        public void NoSeedErodesTheIslandAwayToNothing()
        {
            // Erosion is cheap to overdo and the result still passes every other test in
            // this file: an island of nine tiles is connected, has spawns, and is
            // unplayable. This is the floor under how much the coastline may eat.
            //
            // Swept rather than checked on one seed, and the bar sits under the measured
            // worst case rather than at a round fraction. Over these forty seeds the
            // thinnest island is 259 tiles of the 525 in the bounds and the average is
            // 281, so 240 leaves room for the generator to breathe without leaving room
            // for it to eat the arena.
            for (uint seed = 1; seed <= 40; seed++)
            {
                Arena arena = ArenaGenerator.Generate(ArenaSettings.Default, seed).Arena;

                int ground = 0;
                for (int y = 0; y < arena.Height; y++)
                {
                    for (int x = 0; x < arena.Width; x++)
                    {
                        if (arena[new GridPos(x, y)] != TileKind.Void)
                        {
                            ground++;
                        }
                    }
                }

                Assert.That(ground, Is.GreaterThan(240), $"seed {seed}: the island is mostly sea");
            }
        }

        [Test]
        public void Generate_LaysNothingPermanentOnTheIsland()
        {
            // The lattice is gone. Everything a player meets on the board can be blown
            // up, which is what lets the arena open out as the round goes on. See #120.
            Arena arena = ArenaGenerator.Generate(ArenaSettings.Default, 7u).Arena;

            for (int y = 0; y < arena.Height; y++)
            {
                for (int x = 0; x < arena.Width; x++)
                {
                    Assert.That(
                        arena[new GridPos(x, y)],
                        Is.Not.EqualTo(TileKind.HardBlock),
                        $"something permanent at {x},{y}");
                }
            }
        }

        [Test]
        public void Generate_GrowsCoverInClumpsRatherThanSprinkling()
        {
            // The shape of the cover is the point, not how much of it there is. Rolling
            // per tile gave the same texture everywhere; clumps give ground open enough
            // to fight across and ground dense enough to hide in.
            Arena arena = ArenaGenerator.Generate(ArenaSettings.Default, 7u).Arena;

            int cover = 0;
            var seen = new HashSet<GridPos>();
            int pieces = 0;

            for (int y = 0; y < arena.Height; y++)
            {
                for (int x = 0; x < arena.Width; x++)
                {
                    var tile = new GridPos(x, y);
                    if (!Tiles.CanBeDestroyed(arena[tile]))
                    {
                        continue;
                    }

                    cover++;
                    if (seen.Contains(tile))
                    {
                        continue;
                    }

                    pieces++;
                    Flood(arena, tile, seen);
                }
            }

            Assert.That(cover, Is.GreaterThan(0), "no cover at all");
            Assert.That(
                cover / pieces,
                Is.GreaterThanOrEqualTo(3),
                $"{cover} cover tiles in {pieces} pieces reads as a sprinkle, not as clumps");
        }

        private static void Flood(Arena arena, GridPos from, HashSet<GridPos> seen)
        {
            var pending = new Queue<GridPos>();
            pending.Enqueue(from);
            seen.Add(from);

            while (pending.Count > 0)
            {
                GridPos tile = pending.Dequeue();
                foreach (GridPos step in new[]
                {
                    new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1)
                })
                {
                    GridPos next = tile.Offset(step.X, step.Y);
                    if (arena.Contains(next) && !seen.Contains(next) && Tiles.CanBeDestroyed(arena[next]))
                    {
                        seen.Add(next);
                        pending.Enqueue(next);
                    }
                }
            }
        }

        [Test]
        public void Generate_KeepsEverySpawnAndItsEscapeTilesWalkable()
        {
            GeneratedArena generated = ArenaGenerator.Generate(new ArenaSettings(15, 13, 100), 42u);
            Arena arena = generated.Arena;

            foreach (GridPos spawn in generated.Spawns)
            {
                Assert.That(arena[spawn], Is.EqualTo(TileKind.Floor), $"spawn {spawn} is not walkable");

                int escapes = 0;
                foreach (GridPos direction in Cardinals)
                {
                    GridPos tile = spawn.Offset(direction.X, direction.Y);
                    if (!arena.Contains(tile) || Tiles.BlocksMovement(arena[tile]))
                    {
                        continue;
                    }

                    Assert.That(arena[tile], Is.EqualTo(TileKind.Floor), $"escape tile {tile} is blocked");
                    escapes++;
                }

                Assert.That(escapes, Is.GreaterThan(0), $"spawn {spawn} has no escape route");
            }
        }

        [Test]
        public void SpawnPositions_AreDistinctAndOnTheIsland()
        {
            GeneratedArena generated = ArenaGenerator.Generate(ArenaSettings.Default, 3u);
            IReadOnlyList<GridPos> spawns = generated.Spawns;

            Assert.That(spawns.Count, Is.EqualTo(8));
            Assert.That(new HashSet<GridPos>(spawns).Count, Is.EqualTo(spawns.Count));

            foreach (GridPos spawn in spawns)
            {
                Assert.That(Tiles.CanBeStoodOn(generated.Arena[spawn]), Is.True, $"{spawn} is off the island");
            }
        }

        [Test]
        public void Generate_IsDeterministicForTheSameSeed()
        {
            ArenaSettings settings = ArenaSettings.Default;

            Assert.That(
                Snapshot(ArenaGenerator.Generate(settings, 1234u).Arena),
                Is.EqualTo(Snapshot(ArenaGenerator.Generate(settings, 1234u).Arena)));
        }

        [Test]
        public void Generate_ProducesDifferentLayoutsForDifferentSeeds()
        {
            ArenaSettings settings = ArenaSettings.Default;

            Assert.That(
                Snapshot(ArenaGenerator.Generate(settings, 1u).Arena),
                Is.Not.EqualTo(Snapshot(ArenaGenerator.Generate(settings, 2u).Arena)));
        }

        [Test]
        public void Generate_WithoutDensity_PlacesNoSoftBlocks()
        {
            Arena arena = ArenaGenerator.Generate(new ArenaSettings(15, 13, 0), 99u).Arena;

            for (int y = 0; y < arena.Height; y++)
            {
                for (int x = 0; x < arena.Width; x++)
                {
                    Assert.That(arena[new GridPos(x, y)], Is.Not.EqualTo(TileKind.SoftBlock));
                }
            }
        }

        [Test]
        public void ArenaSettings_AcceptEvenDimensions()
        {
            // Odd was only ever required so the pillar lattice landed inside the border.
            Assert.DoesNotThrow(() => new ArenaSettings(14, 12, 50));
            Assert.That(ArenaGenerator.Generate(new ArenaSettings(14, 12, 50), 3u).Spawns, Is.Not.Empty);
        }

        [Test]
        public void ArenaSettings_RejectDensityOutsidePercentRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArenaSettings(15, 13, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArenaSettings(15, 13, 101));
        }

        private static string Snapshot(Arena arena)
        {
            var builder = new StringBuilder(arena.Width * arena.Height);

            for (int y = 0; y < arena.Height; y++)
            {
                for (int x = 0; x < arena.Width; x++)
                {
                    builder.Append((int)arena[new GridPos(x, y)]);
                }
            }

            return builder.ToString();
        }
    }
}
