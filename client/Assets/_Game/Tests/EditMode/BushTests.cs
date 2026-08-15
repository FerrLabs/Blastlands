using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class BushTests
    {
        private static readonly MatchSettings Settings = MatchSettings.Default.WithTilesPerLooseBomb(0);
        private const int Full = StickReader.Range;

        private static MatchState OpenMatch(params GridPos[] spawns)
        {
            var state = new MatchState(new Arena(15, 15), Settings, 6u);
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
        public void APlayerWalksStraightIntoABush()
        {
            // The whole distinction: a wall is cover you stand behind, a bush is cover
            // you stand in.
            MatchState state = OpenMatch(new GridPos(3, 7));
            state.Arena[new GridPos(5, 7)] = TileKind.Bush;

            Run(state, 40, new PlayerInput(Full, 0, false, false));

            Assert.That(state.Players[0].Tile.X, Is.GreaterThanOrEqualTo(5), "it did not stop at the hedge");
        }

        [Test]
        public void AWallStillStopsThem()
        {
            MatchState state = OpenMatch(new GridPos(3, 7));
            state.Arena[new GridPos(5, 7)] = TileKind.SoftBlock;

            Run(state, 40, new PlayerInput(Full, 0, false, false));

            Assert.That(state.Players[0].Tile.X, Is.LessThan(5));
        }

        [Test]
        public void NothingSeesThroughABush()
        {
            var arena = new Arena(15, 15);
            arena[new GridPos(5, 7)] = TileKind.Bush;

            Assert.That(LineOfSight.Between(arena, new GridPos(3, 7), new GridPos(7, 7), true), Is.False);
            Assert.That(LineOfSight.Between(arena, new GridPos(3, 7), new GridPos(4, 7), true), Is.True, "the near side is clear");
        }

        [Test]
        public void ABlastStopsAtABushAndBurnsIt()
        {
            var arena = new Arena(15, 15);
            for (int y = 0; y < 15; y++)
            {
                arena[new GridPos(6, y)] = TileKind.Bush;
            }

            var bombs = new[] { new Bomb(new GridPos(4, 7), 0, 4) };
            ExplosionResult result = ExplosionResolver.Resolve(arena, bombs, new[] { 0 });

            Assert.That(result.FlameTiles, Has.Member(new GridPos(6, 7)), "the bush is hit");
            Assert.That(result.DestroyedSoftBlocks, Has.Member(new GridPos(6, 7)), "and destroyed");
            Assert.That(result.FlameTiles, Has.No.Member(new GridPos(7, 7)), "and shelters what is behind");
        }

        [Test]
        public void ABushGrowsBackAsABushRatherThanAWall()
        {
            // Without remembering the kind, every hiding place in the arena quietly
            // turns into a wall over the course of a round.
            MatchState state = OpenMatch(new GridPos(1, 1), new GridPos(13, 13));
            state.Arena[new GridPos(5, 4)] = TileKind.Bush;
            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(4, 4), 99, 2), 1));

            Run(state, 3, PlayerInput.None, PlayerInput.None);
            Assert.That(state.Arena[new GridPos(5, 4)], Is.EqualTo(TileKind.Floor), "blown up");
            Assert.That(state.RegrowingWalls[0].Kind, Is.EqualTo(TileKind.Bush));

            Run(state, Settings.WallRegrowTicks + 2, PlayerInput.None, PlayerInput.None);

            Assert.That(state.Arena[new GridPos(5, 4)], Is.EqualTo(TileKind.Bush));
        }

        [Test]
        public void ABushGrowingBackAroundAPlayerLeavesThemWhereTheyAre()
        {
            // A wall has to shove them out; a bush closing over someone is the mechanic
            // working, not a problem to solve.
            MatchState state = OpenMatch(new GridPos(7, 7));
            SubPos before = state.Players[0].Position;
            state.ScheduleRegrowth(new GridPos(7, 7), TileKind.Bush, 2);

            Run(state, 3, PlayerInput.None);

            Assert.That(state.Arena[new GridPos(7, 7)], Is.EqualTo(TileKind.Bush));
            Assert.That(state.Players[0].Position, Is.EqualTo(before), "they are standing in it");
            Assert.That(state.Players[0].Alive, Is.True);
        }

        [Test]
        public void ABombCanBeLeftLyingInABush()
        {
            MatchState state = OpenMatch(new GridPos(3, 7));
            state.Arena[new GridPos(5, 7)] = TileKind.Bush;
            state.AddLooseBomb(new GridPos(5, 7));

            state.Players[0].BombsHeld = 0;
            Run(state, 40, new PlayerInput(Full, 0, false, false));

            Assert.That(state.Players[0].BombsHeld, Is.EqualTo(1), "picked up out of the hedge");
        }

        [Test]
        public void TheGeneratorProducesBothKinds()
        {
            Arena arena = ArenaGenerator.Generate(ArenaSettings.Default, 2026u);

            int bushes = 0;
            int walls = 0;
            for (int y = 0; y < arena.Height; y++)
            {
                for (int x = 0; x < arena.Width; x++)
                {
                    TileKind kind = arena[new GridPos(x, y)];
                    if (kind == TileKind.Bush)
                    {
                        bushes++;
                    }
                    else if (kind == TileKind.SoftBlock)
                    {
                        walls++;
                    }
                }
            }

            Assert.That(bushes, Is.GreaterThan(0), "no cover to stand in");
            Assert.That(walls, Is.GreaterThan(0), "no cover to stand behind");
        }
    }
}
