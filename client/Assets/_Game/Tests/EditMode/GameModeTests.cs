using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    // What actually differs between the two modes. Each of these fails if the rule it
    // guards leaks across, which is the only reason a mode is worth having as data
    // rather than as a fork of the project.
    public class GameModeTests
    {
        private static MatchState Board(MatchSettings settings, params GridPos[] spawns)
        {
            var state = new MatchState(new Arena(15, 15), settings, 5u);
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
        public void ClassicLaysABorderAndAPillarLattice()
        {
            Arena arena = ArenaGenerator.Generate(ArenaSettings.Classic, 7u).Arena;

            Assert.That(arena[new GridPos(0, 5)], Is.EqualTo(TileKind.HardBlock), "the border is open");
            Assert.That(arena[new GridPos(4, 6)], Is.EqualTo(TileKind.HardBlock), "no pillar on an even pair");
            Assert.That(
                arena[new GridPos(3, 5)],
                Is.Not.EqualTo(TileKind.HardBlock),
                "a pillar landed on an odd pair, so the corridors are gone");
        }

        [Test]
        public void ClassicHasNoCoverToHideIn()
        {
            // No bushes and no drop: the board is a closed rectangle, so the only thing
            // between two players is a wall.
            Arena arena = ArenaGenerator.Generate(ArenaSettings.Classic, 7u).Arena;

            for (int y = 0; y < arena.Height; y++)
            {
                for (int x = 0; x < arena.Width; x++)
                {
                    TileKind kind = arena[new GridPos(x, y)];
                    Assert.That(kind, Is.Not.EqualTo(TileKind.Bush), $"a bush at {x},{y}");
                    Assert.That(kind, Is.Not.EqualTo(TileKind.Void), $"a hole at {x},{y}");
                }
            }
        }

        [Test]
        public void ABombComesBackToItsOwnerInClassic()
        {
            MatchState state = Board(MatchSettings.Classic, new GridPos(7, 7), new GridPos(1, 1));
            int held = state.Players[0].BombsHeld;
            Assert.That(held, Is.GreaterThan(0), "nothing to drop");

            Run(state, 1, PlayerInput.Dropping(), PlayerInput.None);
            Assert.That(state.Players[0].BombsHeld, Is.EqualTo(held - 1), "placing it cost nothing");

            Run(state, state.Settings.FuseTicks + 2, PlayerInput.None, PlayerInput.None);

            Assert.That(state.Players[0].BombsHeld, Is.EqualTo(held), "the bomb never came back");
        }

        [Test]
        public void ABombCaughtInAChainComesBackToo()
        {
            // The case the single-bomb test above cannot see. The resolver chain-triggers
            // a neighbour rather than waiting for its fuse, and those bombs are removed
            // like any other, so crediting only the fuses loses one from the owner's
            // pocket every time a chain happens.
            MatchState state = Board(MatchSettings.Classic, new GridPos(1, 1), new GridPos(13, 13));
            PlayerState owner = state.Players[0];
            owner.BombsHeld = 0;

            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(7, 7), owner.Id, 2), 1));
            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(8, 7), owner.Id, 2), 600));

            Run(state, 3, PlayerInput.None, PlayerInput.None);

            Assert.That(state.Bombs.Count, Is.Zero, "the chain did not reach the second bomb");
            Assert.That(owner.BombsHeld, Is.EqualTo(2), "the chained bomb never came back");
        }

        [Test]
        public void ABombIsSpentForGoodInArena()
        {
            // The other half of the same rule, so neither can quietly become the other.
            MatchSettings settings = MatchSettings.Default.WithTilesPerLooseBomb(0);
            MatchState state = Board(settings, new GridPos(7, 7), new GridPos(1, 1));
            int held = state.Players[0].BombsHeld;

            Run(state, 1, PlayerInput.Dropping(), PlayerInput.None);
            Run(state, settings.FuseTicks + 2, PlayerInput.None, PlayerInput.None);

            Assert.That(state.Players[0].BombsHeld, Is.EqualTo(held - 1), "Arena handed the bomb back");
        }

        [Test]
        public void ClassicHasNoDashAndNoShove()
        {
            MatchState dashing = Board(MatchSettings.Classic, new GridPos(7, 7), new GridPos(1, 1));
            SubPos before = dashing.Players[0].Position;
            Run(dashing, 1, PlayerInput.Dashing(Direction.Right), PlayerInput.None);

            Assert.That(dashing.Players[0].Dashing, Is.False, "a dash started");
            Assert.That(
                dashing.Players[0].Position.X - before.X,
                Is.LessThan(dashing.Settings.DashSpeed),
                "it moved at dash speed");

            MatchState shoving = Board(MatchSettings.Classic, new GridPos(6, 7), new GridPos(7, 7));
            shoving.Players[0].Facing = Direction.Right;
            Run(shoving, 1, PlayerInput.Pushing(Direction.Right), PlayerInput.None);

            Assert.That(shoving.Players[1].Shoved, Is.False, "a shove landed");
        }

        [Test]
        public void ClassicShowsTheWholeBoard()
        {
            // Everyone sees everyone, walls included. Cover is an Arena mechanic, and a
            // classic board has nowhere to use it.
            MatchState state = Board(MatchSettings.Classic, new GridPos(2, 7), new GridPos(9, 7));
            for (int y = 0; y < 15; y++)
            {
                state.Arena[new GridPos(5, y)] = TileKind.HardBlock;
            }

            Assert.That(Vision.CanSee(state, state.Players[0], state.Players[1]), Is.True);
        }

        [Test]
        public void ArenaStillHidesWhatItShould()
        {
            MatchState state = Board(MatchSettings.Default.WithTilesPerLooseBomb(0), new GridPos(2, 7), new GridPos(9, 7));
            state.Arena[new GridPos(9, 7)] = TileKind.Bush;

            Assert.That(Vision.CanSee(state, state.Players[0], state.Players[1]), Is.False);
        }

        [Test]
        public void ClassicLeavesNoLooseBombsLyingAround()
        {
            MatchState state = MatchFactory.Create(ArenaSettings.Classic, MatchSettings.Classic, 4, 3u);

            Run(state, state.Settings.BombRespawnTicks * 3, new PlayerInput[4]);

            Assert.That(state.LooseBombs.Count, Is.Zero, "the spawner is still stocking the board");
        }

        [Test]
        public void BotsFightOnAClassicBoardToo()
        {
            // The same guard the Arena ratchet is, for the same reason: every balance
            // number in this mode is measured against these bots, so an instrument that
            // stopped fighting would quietly invalidate all of them.
            //
            // Measured over twelve four-bot matches with sudden death off: 23 deaths of
            // 48, so the bar sits at 18. Arena scores 35 on the same harness. The gap is
            // the lattice: one-tile corridors and a pillar every other tile give far more
            // to hide behind, and bots that can hide do.
            MatchSettings settings = MatchSettings.Classic.WithSuddenDeath(SuddenDeathSettings.Off);
            int deaths = 0;

            for (uint seed = 1; seed <= 12; seed++)
            {
                MatchState state = MatchFactory.Create(ArenaSettings.Classic, settings, 4, seed);
                var brains = new BotBrain[4];
                for (int i = 0; i < brains.Length; i++)
                {
                    brains[i] = new BotBrain(i, BotSettings.Hard);
                }

                var inputs = new PlayerInput[4];
                for (int tick = 0; tick < 3000 && state.Outcome == RoundOutcome.Running; tick++)
                {
                    for (int i = 0; i < brains.Length; i++)
                    {
                        inputs[i] = brains[i].Think(state);
                    }

                    MatchSim.Tick(state, inputs);
                }

                deaths += 4 - state.AliveCount;
            }

            Assert.That(deaths, Is.GreaterThanOrEqualTo(18), "the bots have stopped fighting on the classic board");
        }

        [Test]
        public void EveryClassicMatchEnds()
        {
            // Sudden death carries over, so #105 is not a debt that gets repaid per mode.
            for (uint seed = 1; seed <= 8; seed++)
            {
                MatchState state = MatchFactory.Create(ArenaSettings.Classic, MatchSettings.Classic, 4, seed);
                var brains = new BotBrain[4];
                for (int i = 0; i < brains.Length; i++)
                {
                    brains[i] = new BotBrain(i, BotSettings.Hard);
                }

                var inputs = new PlayerInput[4];
                for (int tick = 0; tick < 6000 && state.Outcome == RoundOutcome.Running; tick++)
                {
                    for (int i = 0; i < brains.Length; i++)
                    {
                        inputs[i] = brains[i].Think(state);
                    }

                    MatchSim.Tick(state, inputs);
                }

                Assert.That(state.Outcome, Is.Not.EqualTo(RoundOutcome.Running), $"seed {seed} never ended");
            }
        }
    }
}
