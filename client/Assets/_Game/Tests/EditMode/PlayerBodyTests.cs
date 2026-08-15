using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class PlayerBodyTests
    {
        private static readonly MatchSettings Settings = MatchSettings.Default;
        private const int Full = StickReader.Range;

        private static MatchState OpenMatch(GridPos spawn)
        {
            var state = new MatchState(new Arena(11, 11), Settings.WithLooseBombTarget(0), 3u);
            state.AddPlayer(spawn);
            return state;
        }

        private static void Run(MatchState state, int ticks, PlayerInput input)
        {
            for (int i = 0; i < ticks; i++)
            {
                MatchSim.Tick(state, new[] { input });
            }
        }

        [Test]
        public void APlayerPressedIntoAWallDiagonallyStillSlidesAlongIt()
        {
            // The whole reason the body exists. On the grid this could not happen:
            // movement was locked to one axis, so there was nothing to slide.
            MatchState state = OpenMatch(new GridPos(5, 5));
            for (int y = 0; y < 11; y++)
            {
                state.Arena[new GridPos(6, y)] = TileKind.HardBlock;
            }

            PlayerState player = state.Players[0];
            int startY = player.Position.Y;

            Run(state, 20, new PlayerInput(Full, Full, false, false));

            Assert.That(player.Position.Y, Is.GreaterThan(startY), "it kept moving down the wall");
            Assert.That(player.Tile.X, Is.EqualTo(5), "and never got through it");
        }

        [Test]
        public void GoingTwoWaysAtOnceIsNotFasterThanGoingOne()
        {
            MatchState straight = OpenMatch(new GridPos(5, 5));
            Run(straight, 10, new PlayerInput(Full, 0, false, false));
            int alongOneAxis = straight.Players[0].Position.X - SubPos.CentreOf(5);

            MatchState diagonal = OpenMatch(new GridPos(5, 5));
            Run(diagonal, 10, new PlayerInput(Full, Full, false, false));

            int dx = diagonal.Players[0].Position.X - SubPos.CentreOf(5);
            int dy = diagonal.Players[0].Position.Y - SubPos.CentreOf(5);

            Assert.That(dx, Is.LessThan(alongOneAxis), "a diagonal is not a free speed boost");
            Assert.That(dx, Is.EqualTo(dy), "and it is even on both axes");
        }

        [Test]
        public void APlayerNeverLeavesTheArena()
        {
            MatchState state = OpenMatch(new GridPos(1, 1));

            Run(state, 200, new PlayerInput(-Full, -Full, false, false));

            Assert.That(state.Arena.Contains(state.Players[0].Tile), Is.True);
            Assert.That(state.Players[0].Position.X, Is.GreaterThan(0));
            Assert.That(state.Players[0].Position.Y, Is.GreaterThan(0));
        }

        [Test]
        public void APlayerFitsThroughAOneTileCorridor()
        {
            // The body has to be narrower than a tile or the arena is impassable.
            MatchState state = OpenMatch(new GridPos(1, 5));
            for (int x = 0; x < 11; x++)
            {
                state.Arena[new GridPos(x, 4)] = TileKind.HardBlock;
                state.Arena[new GridPos(x, 6)] = TileKind.HardBlock;
            }

            Run(state, 120, new PlayerInput(Full, 0, false, false));

            Assert.That(state.Players[0].Tile.X, Is.GreaterThan(6), "it made it down the corridor");
        }

        [Test]
        public void AWallGrowingBackUnderAPlayerDoesNotTrapThem()
        {
            // Tiles the body already overlaps cannot block it, or a player caught by a
            // regrowing wall that had nowhere to shove them would be stuck for good.
            MatchState state = OpenMatch(new GridPos(5, 5));
            state.Arena[new GridPos(5, 5)] = TileKind.SoftBlock;

            Run(state, 40, new PlayerInput(Full, 0, false, false));

            Assert.That(state.Players[0].Tile.X, Is.GreaterThan(5), "it walked out");
        }

        [Test]
        public void MovementIsIdenticalForIdenticalInput()
        {
            MatchState first = OpenMatch(new GridPos(2, 2));
            MatchState second = OpenMatch(new GridPos(2, 2));

            var wander = new[]
            {
                new PlayerInput(Full, 300, false, false),
                new PlayerInput(-200, Full, false, false),
                new PlayerInput(700, -700, false, false)
            };

            for (int i = 0; i < 60; i++)
            {
                PlayerInput input = wander[i % wander.Length];
                MatchSim.Tick(first, new[] { input });
                MatchSim.Tick(second, new[] { input });
            }

            Assert.That(first.Players[0].Position, Is.EqualTo(second.Players[0].Position));
        }
    }
}
