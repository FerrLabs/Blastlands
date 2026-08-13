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
        public void Generate_SurroundsTheArenaWithHardBlocks()
        {
            Arena arena = ArenaGenerator.Generate(ArenaSettings.Default, 1u);

            for (int x = 0; x < arena.Width; x++)
            {
                Assert.That(arena[new GridPos(x, 0)], Is.EqualTo(TileKind.HardBlock));
                Assert.That(arena[new GridPos(x, arena.Height - 1)], Is.EqualTo(TileKind.HardBlock));
            }

            for (int y = 0; y < arena.Height; y++)
            {
                Assert.That(arena[new GridPos(0, y)], Is.EqualTo(TileKind.HardBlock));
                Assert.That(arena[new GridPos(arena.Width - 1, y)], Is.EqualTo(TileKind.HardBlock));
            }
        }

        [Test]
        public void Generate_PlacesThePillarLatticeOnEvenCoordinates()
        {
            Arena arena = ArenaGenerator.Generate(ArenaSettings.Default, 7u);

            for (int y = 2; y < arena.Height - 1; y += 2)
            {
                for (int x = 2; x < arena.Width - 1; x += 2)
                {
                    Assert.That(arena[new GridPos(x, y)], Is.EqualTo(TileKind.HardBlock), $"pillar at {x},{y}");
                }
            }
        }

        [Test]
        public void Generate_KeepsEverySpawnAndItsEscapeTilesWalkable()
        {
            Arena arena = ArenaGenerator.Generate(new ArenaSettings(15, 13, 100), 42u);

            foreach (GridPos spawn in ArenaGenerator.SpawnPositions(arena.Width, arena.Height))
            {
                Assert.That(arena[spawn], Is.EqualTo(TileKind.Floor), $"spawn {spawn} is not walkable");

                int escapes = 0;
                foreach (GridPos direction in Cardinals)
                {
                    GridPos tile = spawn.Offset(direction.X, direction.Y);
                    if (!arena.Contains(tile) || arena[tile] == TileKind.HardBlock)
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
        public void SpawnPositions_AreDistinct()
        {
            IReadOnlyList<GridPos> spawns = ArenaGenerator.SpawnPositions(15, 13);

            Assert.That(spawns.Count, Is.EqualTo(8));
            Assert.That(new HashSet<GridPos>(spawns).Count, Is.EqualTo(spawns.Count));
        }

        [Test]
        public void Generate_IsDeterministicForTheSameSeed()
        {
            ArenaSettings settings = ArenaSettings.Default;

            Assert.That(Snapshot(ArenaGenerator.Generate(settings, 1234u)),
                Is.EqualTo(Snapshot(ArenaGenerator.Generate(settings, 1234u))));
        }

        [Test]
        public void Generate_ProducesDifferentLayoutsForDifferentSeeds()
        {
            ArenaSettings settings = ArenaSettings.Default;

            Assert.That(Snapshot(ArenaGenerator.Generate(settings, 1u)),
                Is.Not.EqualTo(Snapshot(ArenaGenerator.Generate(settings, 2u))));
        }

        [Test]
        public void Generate_WithoutDensity_PlacesNoSoftBlocks()
        {
            Arena arena = ArenaGenerator.Generate(new ArenaSettings(15, 13, 0), 99u);

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
