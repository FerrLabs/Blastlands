using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    // The question the window answers is not "will this tile be on fire when I arrive",
    // which is what a bot used to ask and what walks it into a pocket. It is "if I stand
    // here at tick N, can I still get out", and the answer for one tile depends on the
    // answer for its neighbours.
    public class EscapeWindowTests
    {
        private const int TicksPerTile = 4;

        private static readonly MatchSettings Settings =
            MatchSettings.Default.WithSuddenDeath(SuddenDeathSettings.Off);

        // A corridor one tile high, walls above and below, floor from x = 1 to width - 2.
        private static MatchState Corridor(int width)
        {
            var arena = new Arena(width, 3);
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    arena[new GridPos(x, y)] = TileKind.HardBlock;
                }
            }

            for (int x = 1; x < width - 1; x++)
            {
                arena[new GridPos(x, 1)] = TileKind.Floor;
            }

            var state = new MatchState(arena, Settings, 7u);
            state.AddPlayer(new GridPos(1, 1));
            return state;
        }

        private static EscapeWindow WindowOf(MatchState state)
        {
            BlastMap blast = BlastMap.From(state);
            return EscapeWindow.From(
                state, blast, TicksPerTile, tile => state.Arena.Contains(tile) && state.Arena[tile] == TileKind.Floor);
        }

        [Test]
        public void GroundNoBombReachesCanBeStoodOnWhenever()
        {
            MatchState state = Corridor(6);

            EscapeWindow window = WindowOf(state);

            Assert.That(window.TryLatestEntry(new GridPos(3, 1), out int latest), Is.True);
            Assert.That(latest, Is.EqualTo(int.MaxValue));
            Assert.That(window.Allows(new GridPos(3, 1), 10000), Is.True);
        }

        [Test]
        public void ATileTheFireWillReachHasToBeLeftBeforeItDoes()
        {
            MatchState state = Corridor(8);
            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(2, 1), 99, 1), 40));

            BlastMap blast = BlastMap.From(state);
            int fire = blast.TicksUntilFire(new GridPos(2, 1));
            EscapeWindow window = WindowOf(state);

            Assert.That(window.TryLatestEntry(new GridPos(2, 1), out int latest), Is.True);
            Assert.That(latest, Is.LessThan(fire), "standing there until it burns is not an escape");
            Assert.That(window.Allows(new GridPos(2, 1), latest), Is.True);
            Assert.That(window.Allows(new GridPos(2, 1), latest + 1), Is.False);
        }

        // The whole point. A tile can burn later than the one tile it escapes through,
        // which makes it read as the safer of the two on arrival and makes it the one to
        // leave first: once the way out is alight, nothing behind it gets through.
        [Test]
        public void ATileIsLeftBeforeTheOneItEscapesThrough()
        {
            MatchState state = Corridor(8);

            // (2,1) and (3,1) and (4,1) burn at 40; (1,1) and (2,1) at 90, so (1,1)
            // burns fifty ticks after the only tile it can leave through. (5,1) and
            // (6,1) are the open ground everything is running for.
            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(3, 1), 99, 1), 40));
            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(1, 1), 99, 1), 90));

            BlastMap blast = BlastMap.From(state);
            EscapeWindow window = WindowOf(state);

            Assert.That(
                blast.TicksUntilFire(new GridPos(1, 1)),
                Is.GreaterThan(blast.TicksUntilFire(new GridPos(2, 1))),
                "the dead end burns later than its way out, which is what makes it look safe");

            Assert.That(window.TryLatestEntry(new GridPos(2, 1), out int through), Is.True);
            Assert.That(window.TryLatestEntry(new GridPos(1, 1), out int behind), Is.True);
            Assert.That(behind, Is.LessThan(through), "the tile behind has to be left first, not last");
        }

        [Test]
        public void EachTileDeeperIsOneCrossingTighter()
        {
            MatchState state = Corridor(10);
            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(1, 1), 99, 1), 60));

            EscapeWindow window = WindowOf(state);

            Assert.That(window.TryLatestEntry(new GridPos(2, 1), out int near), Is.True);
            Assert.That(window.TryLatestEntry(new GridPos(1, 1), out int far), Is.True);
            Assert.That(far, Is.LessThanOrEqualTo(near - TicksPerTile));
        }

        [Test]
        public void GroundBehindAWallIsNoWayOut()
        {
            MatchState state = Corridor(6);
            state.Arena[new GridPos(3, 1)] = TileKind.HardBlock;
            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(1, 1), 99, 1), 60));

            EscapeWindow window = WindowOf(state);

            Assert.That(window.TryLatestEntry(new GridPos(4, 1), out int _), Is.True, "the far side is open ground");
            Assert.That(
                window.TryLatestEntry(new GridPos(2, 1), out int _),
                Is.False,
                "walled off from the open ground, with the bomb on its only neighbour");
        }
    }
}
