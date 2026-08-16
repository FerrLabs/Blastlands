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
        public void Generate_PlacesThePillarLatticeOnEvenCoordinates()
        {
            Arena arena = ArenaGenerator.Generate(ArenaSettings.Default, 7u).Arena;

            // Only where there is ground to stand one on. Off the island the lattice
            // has nothing to sit in.
            for (int y = 2; y < arena.Height - 1; y += 2)
            {
                for (int x = 2; x < arena.Width - 1; x += 2)
                {
                    var tile = new GridPos(x, y);
                    if (arena[tile] == TileKind.Void)
                    {
                        continue;
                    }

                    Assert.That(arena[tile], Is.EqualTo(TileKind.HardBlock), $"pillar at {x},{y}");
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
        public void ArenaSettings_RejectEvenDimensions()
        {
            Assert.That(() => new ArenaSettings(14, 13, 50), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new ArenaSettings(15, 12, 50), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ArenaSettings_RejectDensityOutsidePercentRange()
        {
            Assert.That(() => new ArenaSettings(15, 13, -1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new ArenaSettings(15, 13, 101), Throws.TypeOf<ArgumentOutOfRangeException>());
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
