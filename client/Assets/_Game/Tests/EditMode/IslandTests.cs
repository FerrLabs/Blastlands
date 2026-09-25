using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    // What the void does and, as much, what it deliberately does not do. It stops
    // movement and nothing else: you can see across a drop and a blast carries over one,
    // so the only thing the gap takes from you is the ground.
    public class IslandTests
    {
        private static readonly MatchSettings Settings = MatchSettings.Default.WithTilesPerLooseBomb(0);
        private const int Full = StickReader.Range;

        // A strip of island with open sky on both sides of the middle column.
        private static MatchState Chasm(int gap, params GridPos[] spawns)
        {
            var arena = new Arena(15, 15);
            for (int y = 0; y < 15; y++)
            {
                for (int x = 7; x < 7 + gap; x++)
                {
                    arena[new GridPos(x, y)] = TileKind.Void;
                }
            }

            var state = new MatchState(arena, Settings, 4u);
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
        public void NobodyWalksOffTheEdge()
        {
            MatchState state = Chasm(2, new GridPos(4, 7));

            Run(state, 60, new PlayerInput(Full, 0, false, false));

            Assert.That(state.Players[0].Tile.X, Is.LessThan(7), "walked out over the drop");
            Assert.That(state.Players[0].Alive, Is.True, "the drop is a wall, not a hazard");
        }

        [Test]
        public void ADashDoesNotCarryAnyoneOver()
        {
            // A dash is committed for its whole length and cannot be steered, which makes
            // it the obvious way to leave the island by accident.
            MatchState state = Chasm(2, new GridPos(5, 7));

            Run(state, 1, PlayerInput.Dashing(Direction.Right));
            Run(state, 40, PlayerInput.None);

            Assert.That(state.Players[0].Tile.X, Is.LessThan(7));
            Assert.That(state.Players[0].Alive, Is.True);
        }

        [Test]
        public void AShoveStopsAtTheCoastRatherThanPostingSomeoneIntoTheSky()
        {
            MatchState state = Chasm(2, new GridPos(5, 7), new GridPos(6, 7));
            state.Players[0].Facing = Direction.Right;

            Run(state, 20, PlayerInput.Pushing(Direction.Right), PlayerInput.None);

            Assert.That(state.Players[1].Tile.X, Is.LessThan(7));
            Assert.That(state.Players[1].Alive, Is.True);
        }

        [Test]
        public void SightCrossesTheDrop()
        {
            MatchState state = Chasm(2, new GridPos(4, 7), new GridPos(10, 7));

            Assert.That(Vision.CanSee(state, state.Players[0], state.Players[1]), Is.True);
            Assert.That(Vision.CanSee(state, state.Players[1], state.Players[0]), Is.True);
        }

        [Test]
        public void ABlastCrossesTheDropButLeavesNothingBurningOverIt()
        {
            // Treating the void as a wall would shelter anyone standing across a gap they
            // can plainly see over, which is not cover a player could reason about.
            var arena = new Arena(15, 15);
            for (int y = 0; y < 15; y++)
            {
                arena[new GridPos(6, y)] = TileKind.Void;
            }

            var bombs = new[] { new Bomb(new GridPos(5, 7), 0, 4) };
            ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0 }, BlastShape.Disc);

            Assert.That(result.FlameTiles, Has.No.Member(new GridPos(6, 7)), "fire hanging over the drop");
            Assert.That(result.FlameTiles, Has.Member(new GridPos(7, 7)), "the far side is sheltered");
        }

        [Test]
        public void EverySpawnStandsOnTheIsland()
        {
            for (uint seed = 1; seed <= 60; seed++)
            {
                GeneratedArena generated = ArenaGenerator.Generate(ArenaSettings.Default, seed);

                foreach (GridPos spawn in generated.Spawns)
                {
                    Assert.That(
                        Tiles.CanBeStoodOn(generated.Arena[spawn]),
                        Is.True,
                        $"seed {seed}: spawn {spawn} is over the sky");
                }
            }
        }

        [Test]
        public void TheBombSupplyIsMeasuredInGroundRatherThanInBounds()
        {
            // Counting the bounds stocks an island half under water like a full
            // rectangle, and the extra bombs kill the bots rather than the players.
            MatchState island = MatchFactory.Create(ArenaSettings.Default, MatchSettings.Default, 4, 1u);

            int ground = 0;
            for (int y = 0; y < island.Arena.Height; y++)
            {
                for (int x = 0; x < island.Arena.Width; x++)
                {
                    if (island.Arena[new GridPos(x, y)] != TileKind.Void)
                    {
                        ground++;
                    }
                }
            }

            int bounds = island.Arena.Width * island.Arena.Height;
            Assert.That(ground, Is.LessThan(bounds), "this seed produced no island at all");
            Assert.That(
                BombSpawner.TargetFor(island),
                Is.EqualTo(ground / MatchSettings.Default.TilesPerLooseBomb));
        }

    }
}
