using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class VisionTests
    {
        private static readonly MatchSettings Settings = MatchSettings.Default.WithTilesPerLooseBomb(0);
        private const int Full = StickReader.Range;

        private static MatchState OpenMatch(params GridPos[] spawns)
        {
            var state = new MatchState(new Arena(15, 15), Settings, 3u);
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
        public void AcrossOpenGroundEveryoneSeesEveryone()
        {
            MatchState state = OpenMatch(new GridPos(2, 7), new GridPos(9, 7));

            Assert.That(Vision.CanSee(state, state.Players[0], state.Players[1]), Is.True);
            Assert.That(Vision.CanSee(state, state.Players[1], state.Players[0]), Is.True);
        }

        [Test]
        public void AWallBetweenThemStopsBoth()
        {
            MatchState state = OpenMatch(new GridPos(2, 7), new GridPos(9, 7));
            for (int y = 0; y < 15; y++)
            {
                state.Arena[new GridPos(5, y)] = TileKind.SoftBlock;
            }

            Assert.That(Vision.CanSee(state, state.Players[0], state.Players[1]), Is.False);
            Assert.That(Vision.CanSee(state, state.Players[1], state.Players[0]), Is.False);
        }

        [Test]
        public void ABushHidesWhoeverStandsInItButDoesNotBlindThem()
        {
            // The asymmetry is the mechanic. A bush that blinded its occupant would be a
            // tile to avoid rather than one to use.
            MatchState state = OpenMatch(new GridPos(2, 7), new GridPos(9, 7));
            state.Arena[new GridPos(9, 7)] = TileKind.Bush;

            Assert.That(Vision.CanSee(state, state.Players[0], state.Players[1]), Is.False, "the hider is seen");
            Assert.That(Vision.CanSee(state, state.Players[1], state.Players[0]), Is.True, "the hider is blind");
        }

        [Test]
        public void DashingGivesTheHiderAway()
        {
            MatchState state = OpenMatch(new GridPos(2, 7), new GridPos(9, 7));
            state.Arena[new GridPos(9, 7)] = TileKind.Bush;
            Assert.That(Vision.CanSee(state, state.Players[0], state.Players[1]), Is.False);

            Run(state, 1, PlayerInput.None, PlayerInput.Dashing(Direction.Up));

            Assert.That(state.Players[1].RevealTicksRemaining, Is.GreaterThan(0));
            state.Players[1].Position = SubPos.AtTileCentre(new GridPos(9, 7));
            Assert.That(Vision.CanSee(state, state.Players[0], state.Players[1]), Is.True);
        }

        [Test]
        public void BeingGivenAwayWearsOff()
        {
            MatchState state = OpenMatch(new GridPos(2, 7), new GridPos(9, 7));
            state.Arena[new GridPos(9, 7)] = TileKind.Bush;
            state.Players[1].RevealTicksRemaining = 3;

            Run(state, 4, PlayerInput.None, PlayerInput.None);

            Assert.That(Vision.CanSee(state, state.Players[0], state.Players[1]), Is.False);
        }

        [Test]
        public void ShovingGivesAwayBothOfThem()
        {
            MatchState state = OpenMatch(new GridPos(6, 7), new GridPos(7, 7));
            state.Players[0].Facing = Direction.Right;

            Run(state, 1, PlayerInput.Pushing(Direction.Right), PlayerInput.None);

            Assert.That(state.Players[0].RevealTicksRemaining, Is.GreaterThan(0), "the shover");
            Assert.That(state.Players[1].RevealTicksRemaining, Is.GreaterThan(0), "and the shoved");
        }

        [Test]
        public void NobodyShovesWhatTheyCannotSee()
        {
            MatchState state = OpenMatch(new GridPos(6, 7), new GridPos(7, 7));
            state.Arena[new GridPos(7, 7)] = TileKind.Bush;
            state.Players[0].Facing = Direction.Right;

            Run(state, 1, PlayerInput.Pushing(Direction.Right), PlayerInput.None);

            Assert.That(state.Players[1].Shoved, Is.False);
        }

        [Test]
        public void ADeadPlayerIsNotSeen()
        {
            MatchState state = OpenMatch(new GridPos(2, 7), new GridPos(9, 7));
            state.Players[1].Alive = false;

            Assert.That(Vision.CanSee(state, state.Players[0], state.Players[1]), Is.False);
        }

        [Test]
        public void AnEmptyTileReadsAsEmptyOnlyWhenItIsNotCover()
        {
            MatchState state = OpenMatch(new GridPos(2, 7));
            state.Arena[new GridPos(9, 7)] = TileKind.Bush;

            Assert.That(Vision.CanSeeItIsEmpty(state, new GridPos(2, 7), new GridPos(6, 7)), Is.True);
            Assert.That(
                Vision.CanSeeItIsEmpty(state, new GridPos(2, 7), new GridPos(9, 7)),
                Is.False,
                "seeing a bush says nothing about what is inside it");
        }
    }
}
