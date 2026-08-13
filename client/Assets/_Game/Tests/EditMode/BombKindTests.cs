using System.Collections.Generic;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class BombKindTests
    {
        [Test]
        public void Pierce_DestroysEverySoftBlockInLineAndKeepsGoing()
        {
            var arena = new Arena(9, 9);
            arena[new GridPos(2, 4)] = TileKind.SoftBlock;
            arena[new GridPos(3, 4)] = TileKind.SoftBlock;
            arena[new GridPos(4, 4)] = TileKind.SoftBlock;
            var bombs = new[] { new Bomb(new GridPos(1, 4), 0, 4, BombKind.Pierce) };

            ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0 });

            Assert.That(result.DestroyedSoftBlocks, Is.EquivalentTo(new[]
            {
                new GridPos(2, 4), new GridPos(3, 4), new GridPos(4, 4)
            }));
            Assert.That(result.FlameTiles, Has.Member(new GridPos(5, 4)));
        }

        [Test]
        public void Standard_WithTheSameRangeStopsAtTheFirstSoftBlock()
        {
            var arena = new Arena(9, 9);
            arena[new GridPos(2, 4)] = TileKind.SoftBlock;
            arena[new GridPos(3, 4)] = TileKind.SoftBlock;
            var bombs = new[] { new Bomb(new GridPos(1, 4), 0, 4) };

            ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0 });

            Assert.That(result.DestroyedSoftBlocks, Is.EquivalentTo(new[] { new GridPos(2, 4) }));
            Assert.That(result.FlameTiles, Has.No.Member(new GridPos(3, 4)));
        }

        [Test]
        public void Pierce_IsStillStoppedByAHardBlock()
        {
            var arena = new Arena(9, 9);
            arena[new GridPos(2, 4)] = TileKind.SoftBlock;
            arena[new GridPos(3, 4)] = TileKind.HardBlock;
            var bombs = new[] { new Bomb(new GridPos(1, 4), 0, 5, BombKind.Pierce) };

            ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0 });

            Assert.That(result.DestroyedSoftBlocks, Is.EquivalentTo(new[] { new GridPos(2, 4) }));
            Assert.That(result.FlameTiles, Has.No.Member(new GridPos(3, 4)));
            Assert.That(result.FlameTiles, Has.No.Member(new GridPos(4, 4)));
        }

        [Test]
        public void Cluster_FlaresOneTileAroundEachArmTip()
        {
            var arena = new Arena(9, 9);
            var bombs = new[] { new Bomb(new GridPos(4, 4), 0, 2, BombKind.Cluster) };

            ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0 });

            Assert.That(result.FlameTiles, Has.Member(new GridPos(7, 4)), "reaches past its own range");
            Assert.That(result.FlameTiles, Has.Member(new GridPos(6, 5)), "flares perpendicular to the arm");
            Assert.That(result.FlameTiles, Has.Member(new GridPos(6, 3)));
            Assert.That(result.FlameTiles, Has.Member(new GridPos(4, 7)));
            Assert.That(result.FlameTiles, Has.Member(new GridPos(1, 4)));
        }

        [Test]
        public void Standard_WithTheSameRangeReachesNoneOfTheClusterFlareTiles()
        {
            var arena = new Arena(9, 9);
            var bombs = new[] { new Bomb(new GridPos(4, 4), 0, 2) };

            ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0 });

            Assert.That(result.FlameTiles, Has.No.Member(new GridPos(7, 4)));
            Assert.That(result.FlameTiles, Has.No.Member(new GridPos(6, 5)));
            Assert.That(result.FlameTiles, Has.No.Member(new GridPos(6, 3)));
        }

        [Test]
        public void Cluster_DoesNotFlareFromAnArmBlockedImmediately()
        {
            var arena = new Arena(9, 9);
            arena[new GridPos(2, 4)] = TileKind.HardBlock;
            var bombs = new[] { new Bomb(new GridPos(1, 4), 0, 3, BombKind.Cluster) };

            ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0 });

            Assert.That(result.FlameTiles, Has.No.Member(new GridPos(2, 4)));
            Assert.That(result.FlameTiles, Has.No.Member(new GridPos(3, 4)));
        }

        [Test]
        public void Cluster_FlareChainsABombItTouches()
        {
            var arena = new Arena(9, 9);
            var bombs = new[]
            {
                new Bomb(new GridPos(4, 4), 0, 2, BombKind.Cluster),
                new Bomb(new GridPos(7, 4), 1, 1)
            };

            ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0 });

            Assert.That(result.DetonatedBombs, Is.EquivalentTo(new[] { 0, 1 }));
        }

        [Test]
        public void Cluster_ChainsTerminateAndDetonateEachBombOnce()
        {
            var arena = new Arena(9, 9);
            var bombs = new[]
            {
                new Bomb(new GridPos(2, 4), 0, 2, BombKind.Cluster),
                new Bomb(new GridPos(4, 4), 1, 2, BombKind.Cluster),
                new Bomb(new GridPos(6, 4), 2, 2, BombKind.Cluster)
            };

            ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0 });

            Assert.That(result.DetonatedBombs, Is.EquivalentTo(new[] { 0, 1, 2 }));
            Assert.That(new HashSet<int>(result.DetonatedBombs).Count, Is.EqualTo(result.DetonatedBombs.Count));
            Assert.That(new HashSet<GridPos>(result.FlameTiles).Count, Is.EqualTo(result.FlameTiles.Count));
        }

        [Test]
        public void EveryKindStillBurnsItsOwnTile()
        {
            var arena = new Arena(9, 9);

            foreach (BombKind kind in new[] { BombKind.Standard, BombKind.Pierce, BombKind.Cluster })
            {
                var bombs = new[] { new Bomb(new GridPos(4, 4), 0, 2, kind) };
                ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0 });

                Assert.That(result.FlameTiles, Has.Member(new GridPos(4, 4)), $"{kind} spares its own tile");
            }
        }
    }
}
