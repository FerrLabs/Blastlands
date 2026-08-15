using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class DashTests
    {
        private static readonly MatchSettings Settings = MatchSettings.Default;

        private static MatchState OpenMatch(params GridPos[] spawns)
        {
            var state = new MatchState(new Arena(15, 15), Settings, 9u);
            foreach (GridPos spawn in spawns)
            {
                state.AddPlayer(spawn);
            }

            return state;
        }

        private static void Run(MatchState state, int ticks, params PlayerInput[] inputs)
        {
            for (int i = 0; i < ticks; i++)
            {
                MatchSim.Tick(state, inputs);
            }
        }

        [Test]
        public void ADashCoversMoreGroundThanWalking()
        {
            MatchState walking = OpenMatch(new GridPos(2, 7));
            Run(walking, Settings.DashTicks, PlayerInput.Moving(Direction.Right));

            MatchState dashing = OpenMatch(new GridPos(2, 7));
            MatchSim.Tick(dashing, new[] { PlayerInput.Dashing(Direction.Right) });
            Run(dashing, Settings.DashTicks - 1, PlayerInput.None);

            Assert.That(
                dashing.Players[0].Position.X,
                Is.GreaterThan(walking.Players[0].Position.X),
                "the same ticks should carry further");
        }

        [Test]
        public void ADashRunsItsLengthWithoutFurtherInput()
        {
            // It is committed on purpose: that is the risk that pays for the speed.
            MatchState state = OpenMatch(new GridPos(2, 7));
            int start = state.Players[0].Position.X;

            MatchSim.Tick(state, new[] { PlayerInput.Dashing(Direction.Right) });
            Run(state, Settings.DashTicks - 1, PlayerInput.None);

            Assert.That(state.Players[0].Position.X, Is.GreaterThan(start));
            Assert.That(state.Players[0].Dashing, Is.False, "and then it is over");
        }

        [Test]
        public void ADashCannotBeSteeredOnceItHasStarted()
        {
            MatchState state = OpenMatch(new GridPos(7, 7));
            int startY = state.Players[0].Position.Y;

            MatchSim.Tick(state, new[] { PlayerInput.Dashing(Direction.Right) });
            Run(state, Settings.DashTicks - 1, PlayerInput.Moving(Direction.Up));

            Assert.That(state.Players[0].Position.Y, Is.EqualTo(startY), "it went where it was aimed");
        }

        [Test]
        public void ASecondDashIsRefusedUntilItHasRecharged()
        {
            MatchState state = OpenMatch(new GridPos(2, 7));
            MatchSim.Tick(state, new[] { PlayerInput.Dashing(Direction.Right) });

            Run(state, Settings.DashTicks, PlayerInput.None);
            Assert.That(state.Players[0].CanDash, Is.False, "still recharging");

            // Refusing the dash does not refuse the movement: the player keeps walking
            // in the direction they asked for, they simply do not get the burst.
            int before = state.Players[0].Position.X;
            MatchSim.Tick(state, new[] { PlayerInput.Dashing(Direction.Right) });
            Assert.That(state.Players[0].Dashing, Is.False, "the dash is ignored");
            Assert.That(
                state.Players[0].Position.X - before,
                Is.EqualTo(Settings.SpeedFor(state.Players[0].SpeedSteps)),
                "they walked, they did not burst");

            Run(state, Settings.DashCooldownTicks, PlayerInput.None);
            Assert.That(state.Players[0].CanDash, Is.True, "recharged");
        }

        [Test]
        public void ADashStopsAtAWallInsteadOfPassingThrough()
        {
            MatchState state = OpenMatch(new GridPos(1, 7));
            state.Arena[new GridPos(2, 7)] = TileKind.HardBlock;

            MatchSim.Tick(state, new[] { PlayerInput.Dashing(Direction.Right) });
            Run(state, Settings.DashTicks, PlayerInput.None);

            Assert.That(state.Players[0].Tile, Is.EqualTo(new GridPos(1, 7)), "a dash is speed, not a teleport");
        }

        [Test]
        public void ADashDoesNotCarryThroughAPlacedBomb()
        {
            // Passing through would make laying a bomb risk-free: seal yourself in, then
            // dash out of your own trap.
            MatchState state = OpenMatch(new GridPos(1, 7), new GridPos(13, 13));
            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(2, 7), 99, 1), 400));

            MatchSim.Tick(state, new[] { PlayerInput.Dashing(Direction.Right), PlayerInput.None });
            Run(state, Settings.DashTicks, PlayerInput.None, PlayerInput.None);

            Assert.That(state.Players[0].Tile, Is.EqualTo(new GridPos(1, 7)));
        }

        [Test]
        public void ADashingPlayerBurnsLikeAnyOther()
        {
            // No invulnerability window. Bombs are what kills, and a dash does not
            // change what a bomb means.
            MatchState state = OpenMatch(new GridPos(1, 7), new GridPos(13, 13));
            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(4, 7), 99, 3), 1));

            MatchSim.Tick(state, new[] { PlayerInput.Dashing(Direction.Right), PlayerInput.None });
            Run(state, Settings.DashTicks, PlayerInput.None, PlayerInput.None);

            Assert.That(state.Players[0].Alive, Is.False, "it dashed straight into the blast");
        }

        [Test]
        public void DashingNowhereDoesNotSpendTheCooldown()
        {
            MatchState state = OpenMatch(new GridPos(7, 7));
            state.Players[0].Facing = Direction.None;

            MatchSim.Tick(state, new[] { new PlayerInput(Direction.None, false, true) });

            Assert.That(state.Players[0].Dashing, Is.False);
            Assert.That(state.Players[0].CanDash, Is.True, "the button was not wasted");
        }

        [Test]
        public void AStandingDashGoesTheWayThePlayerFaces()
        {
            MatchState state = OpenMatch(new GridPos(7, 7));
            Run(state, 10, PlayerInput.Moving(Direction.Left));
            Assert.That(state.Players[0].Facing, Is.EqualTo(Direction.Left));

            int before = state.Players[0].Position.X;
            MatchSim.Tick(state, new[] { new PlayerInput(Direction.None, false, true) });
            Run(state, Settings.DashTicks - 1, PlayerInput.None);

            Assert.That(state.Players[0].Position.X, Is.LessThan(before));
        }

        [Test]
        public void TwoPlayersDashingTheSameTickBothGo()
        {
            MatchState state = OpenMatch(new GridPos(2, 7), new GridPos(12, 7));
            int leftStart = state.Players[0].Position.X;
            int rightStart = state.Players[1].Position.X;

            MatchSim.Tick(state, new[] { PlayerInput.Dashing(Direction.Right), PlayerInput.Dashing(Direction.Left) });
            Run(state, Settings.DashTicks - 1, PlayerInput.None, PlayerInput.None);

            Assert.That(state.Players[0].Position.X, Is.GreaterThan(leftStart));
            Assert.That(state.Players[1].Position.X, Is.LessThan(rightStart));
        }
    }
}
