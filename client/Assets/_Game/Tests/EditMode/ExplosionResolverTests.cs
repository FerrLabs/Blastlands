using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class ExplosionResolverTests
    {
        [Test]
        public void Resolve_BurnsADiscOfFireRangeAroundTheBomb()
        {
            // A disc, not a cross. The cross was a legibility device that only worked
            // while players stood on tile centres.
            var arena = new Arena(9, 9);
            var bombs = new[] { new Bomb(new GridPos(4, 4), 0, 2) };

            ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0 });

            Assert.That(result.FlameTiles, Has.Member(new GridPos(4, 4)), "the bomb tile");
            Assert.That(result.FlameTiles, Has.Member(new GridPos(6, 4)), "out to the range");
            Assert.That(result.FlameTiles, Has.Member(new GridPos(5, 5)), "and off the axes");
            Assert.That(result.FlameTiles, Has.No.Member(new GridPos(6, 6)), "but not past the radius");
            Assert.That(result.FlameTiles, Has.No.Member(new GridPos(7, 4)), "nor beyond the range");

            foreach (GridPos tile in result.FlameTiles)
            {
                int dx = tile.X - 4;
                int dy = tile.Y - 4;
                Assert.That((dx * dx) + (dy * dy), Is.LessThanOrEqualTo(4), $"{tile} is outside the radius");
            }
        }

        [Test]
        public void Resolve_ClampsFlameToTheArenaBounds()
        {
            var arena = new Arena(5, 5);
            var bombs = new[] { new Bomb(new GridPos(0, 0), 0, 3) };

            ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0 });

            foreach (GridPos tile in result.FlameTiles)
            {
                Assert.That(arena.Contains(tile), Is.True, $"{tile} is outside the arena");
            }

            Assert.That(result.FlameTiles, Has.Member(new GridPos(3, 0)));
            Assert.That(result.FlameTiles, Has.Member(new GridPos(0, 3)));
        }

        [Test]
        public void Resolve_DoesNotReachAroundACorner()
        {
            // The load-bearing rule of the whole change: cover works. Without it the
            // blast is a distance check, and stepping behind a wall means nothing.
            var arena = new Arena(9, 9);
            for (int y = 0; y <= 6; y++)
            {
                arena[new GridPos(5, y)] = TileKind.HardBlock;
            }

            var bombs = new[] { new Bomb(new GridPos(3, 3), 0, 4) };

            ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0 });

            Assert.That(result.FlameTiles, Has.No.Member(new GridPos(6, 3)), "the wall stopped it");
            Assert.That(result.FlameTiles, Has.No.Member(new GridPos(6, 2)), "and it did not bend around");
            Assert.That(result.FlameTiles, Has.Member(new GridPos(4, 3)), "the near side still burns");
        }

        [Test]
        public void Resolve_LeaksThroughADiagonalGap()
        {
            // Documented, not accidental. Bresenham squeezes between two blocks that
            // only touch at a corner, so standing diagonally behind a lone pillar is not
            // cover. Closing it means treating a corner touch as solid, which makes
            // cover markedly stronger everywhere — a balance decision, not a bug fix.
            var arena = new Arena(9, 9);
            arena[new GridPos(5, 4)] = TileKind.HardBlock;
            arena[new GridPos(4, 5)] = TileKind.HardBlock;

            var bombs = new[] { new Bomb(new GridPos(4, 4), 0, 3) };

            ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0 });

            Assert.That(result.FlameTiles, Has.Member(new GridPos(5, 5)), "it slips through the corner");
        }

        [Test]
        public void Resolve_TreatsAWallOfCratesAsCover()
        {
            var arena = new Arena(9, 9);
            for (int y = 0; y <= 8; y++)
            {
                arena[new GridPos(5, y)] = TileKind.SoftBlock;
            }

            var bombs = new[] { new Bomb(new GridPos(3, 4), 0, 4) };

            ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0 });

            Assert.That(result.FlameTiles, Has.Member(new GridPos(5, 4)), "the crate itself is hit");
            Assert.That(result.DestroyedSoftBlocks, Has.Member(new GridPos(5, 4)));
            Assert.That(result.FlameTiles, Has.No.Member(new GridPos(6, 4)), "whoever is behind it is safe");
        }

        [Test]
        public void Resolve_StopsAtHardBlockWithoutBurningIt()
        {
            var arena = new Arena(7, 7);
            arena[new GridPos(5, 3)] = TileKind.HardBlock;
            var bombs = new[] { new Bomb(new GridPos(3, 3), 0, 3) };

            ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0 });

            Assert.That(result.FlameTiles, Has.Member(new GridPos(4, 3)));
            Assert.That(result.FlameTiles, Has.No.Member(new GridPos(5, 3)));
            Assert.That(result.FlameTiles, Has.No.Member(new GridPos(6, 3)));
        }

        [Test]
        public void Resolve_DestroysTheFirstSoftBlockAndStopsThere()
        {
            var arena = new Arena(7, 7);
            arena[new GridPos(4, 3)] = TileKind.SoftBlock;
            arena[new GridPos(5, 3)] = TileKind.SoftBlock;
            var bombs = new[] { new Bomb(new GridPos(3, 3), 0, 3) };

            ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0 });

            Assert.That(result.DestroyedSoftBlocks, Is.EquivalentTo(new[] { new GridPos(4, 3) }));
            Assert.That(result.FlameTiles, Has.Member(new GridPos(4, 3)));
            Assert.That(result.FlameTiles, Has.No.Member(new GridPos(5, 3)));
        }

        [Test]
        public void Resolve_ChainsABombCaughtInTheBlast()
        {
            var arena = new Arena(7, 7);
            var bombs = new[]
            {
                new Bomb(new GridPos(3, 3), 0, 1),
                new Bomb(new GridPos(4, 3), 1, 2)
            };

            ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0 });

            Assert.That(result.DetonatedBombs, Is.EquivalentTo(new[] { 0, 1 }));
            Assert.That(result.FlameTiles, Has.Member(new GridPos(6, 3)));
        }

        [Test]
        public void Resolve_DoesNotChainABombShieldedByAHardBlock()
        {
            var arena = new Arena(7, 7);
            arena[new GridPos(2, 3)] = TileKind.HardBlock;
            var bombs = new[]
            {
                new Bomb(new GridPos(1, 3), 0, 5),
                new Bomb(new GridPos(3, 3), 1, 1)
            };

            ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0 });

            Assert.That(result.DetonatedBombs, Is.EquivalentTo(new[] { 0 }));
        }

        [Test]
        public void Resolve_DoesNotChainABombBehindADestroyedSoftBlock()
        {
            var arena = new Arena(7, 7);
            arena[new GridPos(2, 3)] = TileKind.SoftBlock;
            var bombs = new[]
            {
                new Bomb(new GridPos(1, 3), 0, 5),
                new Bomb(new GridPos(3, 3), 1, 1)
            };

            ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0 });

            Assert.That(result.DetonatedBombs, Is.EquivalentTo(new[] { 0 }));
            Assert.That(result.DestroyedSoftBlocks, Is.EquivalentTo(new[] { new GridPos(2, 3) }));
        }

        [Test]
        public void Resolve_DetonatesEachBombOnceWhenBombsTriggerEachOther()
        {
            var arena = new Arena(7, 7);
            var bombs = new[]
            {
                new Bomb(new GridPos(3, 3), 0, 2),
                new Bomb(new GridPos(4, 3), 1, 2),
                new Bomb(new GridPos(5, 3), 2, 2)
            };

            ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0 });

            Assert.That(result.DetonatedBombs, Is.EquivalentTo(new[] { 0, 1, 2 }));
            Assert.That(new HashSet<int>(result.DetonatedBombs).Count, Is.EqualTo(result.DetonatedBombs.Count));
        }

        [Test]
        public void Resolve_ReportsNoDuplicateFlameTilesWhenBlastsOverlap()
        {
            var arena = new Arena(7, 7);
            var bombs = new[]
            {
                new Bomb(new GridPos(3, 3), 0, 2),
                new Bomb(new GridPos(3, 4), 1, 2)
            };

            ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0, 1 });

            Assert.That(new HashSet<GridPos>(result.FlameTiles).Count, Is.EqualTo(result.FlameTiles.Count));
        }

        [Test]
        public void Resolve_RejectsABombIndexOutsideTheList()
        {
            var arena = new Arena(7, 7);
            var bombs = new[] { new Bomb(new GridPos(3, 3), 0, 1) };

            Assert.That(
                () => ExplosionResolver.Resolve(arena, bombs, new[] { 1 }),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
