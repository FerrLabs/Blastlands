using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class ExplosionResolverTests
    {
        [Test]
        public void Resolve_BurnsTheBombTileAndReachesFireRangeInEveryDirection()
        {
            var arena = new Arena(7, 7);
            var bombs = new[] { new Bomb(new GridPos(3, 3), 0, 2) };

            ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0 });

            Assert.That(result.FlameTiles, Is.EquivalentTo(new[]
            {
                new GridPos(3, 3),
                new GridPos(4, 3), new GridPos(5, 3),
                new GridPos(2, 3), new GridPos(1, 3),
                new GridPos(3, 4), new GridPos(3, 5),
                new GridPos(3, 2), new GridPos(3, 1)
            }));
        }

        [Test]
        public void Resolve_ClampsFlameToTheArenaBounds()
        {
            var arena = new Arena(5, 5);
            var bombs = new[] { new Bomb(new GridPos(0, 0), 0, 3) };

            ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0 });

            Assert.That(result.FlameTiles, Is.EquivalentTo(new[]
            {
                new GridPos(0, 0),
                new GridPos(1, 0), new GridPos(2, 0), new GridPos(3, 0),
                new GridPos(0, 1), new GridPos(0, 2), new GridPos(0, 3)
            }));
        }

        [Test]
        public void Resolve_StopsAtHardBlockWithoutBurningIt()
        {
            var arena = new Arena(7, 7);
            arena[new GridPos(5, 3)] = TileKind.HardBlock;
            var bombs = new[] { new Bomb(new GridPos(3, 3), 0, 3) };

            ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0 });

            Assert.That(result.FlameTiles, Does.Contain(new GridPos(4, 3)));
            Assert.That(result.FlameTiles, Does.Not.Contain(new GridPos(5, 3)));
            Assert.That(result.FlameTiles, Does.Not.Contain(new GridPos(6, 3)));
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
            Assert.That(result.FlameTiles, Does.Contain(new GridPos(4, 3)));
            Assert.That(result.FlameTiles, Does.Not.Contain(new GridPos(5, 3)));
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
            Assert.That(result.FlameTiles, Does.Contain(new GridPos(6, 3)));
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
