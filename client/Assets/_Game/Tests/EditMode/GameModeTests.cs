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
        public void AClassicSpawnHasSomewhereToGoBeforeYouDigAnything()
        {
            // The failure this guards is not a crash, it is a board nobody wants to play:
            // packed tight enough and a player starts sealed into their spawn pocket with
            // no move available but to bomb the wall in front of them.
            //
            // Measured over the same twenty seeds this asserts on, a spawn reaches 40.7
            // tiles on average. At the 75% this board shipped with it was 9.3.
            //
            // The bar is 20 rather than something close to 40.7 on purpose. It is there
            // to catch a board packed back into spawn pockets, not to pin one particular
            // tuning: 45% is a defensible number too and gives 27.4, and a bar that
            // failed on it would be blocking a reasonable retune rather than a bug.
            int reachable = 0;
            int spawns = 0;

            for (uint seed = 1; seed <= 20; seed++)
            {
                MatchState state = MatchFactory.Create(
                    ArenaSettings.Classic, MatchSettings.For(GameMode.Classic), 4, seed);

                for (int i = 0; i < state.Players.Count; i++)
                {
                    spawns++;
                    reachable += WalkableFrom(state.Arena, state.Players[i].Tile);
                }
            }

            Assert.That(reachable / (double)spawns, Is.GreaterThan(20d));
        }

        private static int WalkableFrom(Arena arena, GridPos start)
        {
            var seen = new System.Collections.Generic.HashSet<GridPos> { start };
            var queue = new System.Collections.Generic.Queue<GridPos>();
            queue.Enqueue(start);

            var steps = new[]
            {
                new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1)
            };

            while (queue.Count > 0)
            {
                GridPos at = queue.Dequeue();
                foreach (GridPos step in steps)
                {
                    var next = new GridPos(at.X + step.X, at.Y + step.Y);
                    if (!arena.Contains(next) || seen.Contains(next))
                    {
                        continue;
                    }

                    if (arena[next] != TileKind.Floor)
                    {
                        continue;
                    }

                    seen.Add(next);
                    queue.Enqueue(next);
                }
            }

            return seen.Count;
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
        public void BlindedSharesTheClassicBoardRatherThanGettingAnIsland()
        {
            // The reason generation asks for a board rather than testing the mode. A
            // check on the mode would have quietly handed this one an island, and the
            // difference is the whole point of the pairing.
            Arena classic = ArenaGenerator.Generate(ArenaSettings.Classic, 11u).Arena;

            Assert.That(ArenaSettings.Classic.Board, Is.EqualTo(BoardKind.Lattice));
            Assert.That(classic[new GridPos(4, 6)], Is.EqualTo(TileKind.HardBlock), "no lattice");

            for (int y = 0; y < classic.Height; y++)
            {
                for (int x = 0; x < classic.Width; x++)
                {
                    Assert.That(classic[new GridPos(x, y)], Is.Not.EqualTo(TileKind.Void), "an island crept in");
                }
            }
        }

        [Test]
        public void OnlyBlindedHidesAnyone()
        {
            // The pair that separates the two classic modes. Same board, same wall,
            // opposite answer, so neither can drift into the other.
            MatchState plain = Board(MatchSettings.Classic, new GridPos(2, 7), new GridPos(9, 7));
            MatchState blinded = Board(MatchSettings.ClassicBlinded, new GridPos(2, 7), new GridPos(9, 7));

            foreach (MatchState state in new[] { plain, blinded })
            {
                for (int y = 0; y < 15; y++)
                {
                    state.Arena[new GridPos(5, y)] = TileKind.HardBlock;
                }
            }

            Assert.That(
                Vision.CanSee(plain, plain.Players[0], plain.Players[1]),
                Is.True,
                "the real bomberman started hiding people");
            Assert.That(
                Vision.CanSee(blinded, blinded.Players[0], blinded.Players[1]),
                Is.False,
                "blinded showed someone through a wall");
        }

        [Test]
        public void BlindedKeepsEverythingElseAboutClassic()
        {
            // Only sight changes. If any of these drifted, blinded would quietly be a
            // third rule set rather than a lighting switch on the second.
            RuleSet classic = RuleSet.Classic;
            RuleSet blinded = RuleSet.ClassicBlinded;

            Assert.That(blinded.BombsReturn, Is.EqualTo(classic.BombsReturn));
            Assert.That(blinded.AllowsDash, Is.EqualTo(classic.AllowsDash));
            Assert.That(blinded.AllowsShove, Is.EqualTo(classic.AllowsShove));
            Assert.That(blinded.HidesTheUnseen, Is.Not.EqualTo(classic.HidesTheUnseen));
        }

        [Test]
        public void EveryModeResolvesToItsOwnRules()
        {
            Assert.That(RuleSet.For(GameMode.Arena).AllowsDash, Is.True);
            Assert.That(RuleSet.For(GameMode.Classic).AllowsDash, Is.False);
            Assert.That(RuleSet.For(GameMode.ClassicBlinded).AllowsDash, Is.False);

            Assert.That(RuleSet.For(GameMode.Arena).HidesTheUnseen, Is.True);
            Assert.That(RuleSet.For(GameMode.Classic).HidesTheUnseen, Is.False);
            Assert.That(RuleSet.For(GameMode.ClassicBlinded).HidesTheUnseen, Is.True);

            Assert.That(MatchSettings.For(GameMode.Classic).Rules.HidesTheUnseen, Is.False);
            Assert.That(MatchSettings.For(GameMode.ClassicBlinded).Rules.HidesTheUnseen, Is.True);
            Assert.That(MatchSettings.For(GameMode.Arena).Rules.BombsReturn, Is.False);
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
        public void BotsStillFightWithTheLightsOff()
        {
            // Blinded changes what a bot knows, not what it can do, so it needs its own
            // bar: the others measure bots that can see.
            //
            // Twenty-four matches rather than twelve, and the bar moved with the escape
            // window. Deaths here count suicides as well as kills, and a bot that plans
            // its way out of its own blast stops supplying the first kind: over twelve
            // matches this fell from 22 to 13 without a single bot becoming less
            // willing to fight, since they bomb 2.6 times more than they did.
            //
            // So the figure is re-measured rather than kept: 22 of 96 over twenty-four
            // matches. Against 15 for the same bots with their hunting disabled, which
            // is what this is here to catch, so the bar sits at 18. At twelve matches
            // the two readings were 13 and 8, too close together to tell apart.
            MatchSettings settings = MatchSettings.ClassicBlinded.WithSuddenDeath(SuddenDeathSettings.Off);
            int deaths = 0;

            for (uint seed = 1; seed <= 24; seed++)
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

            Assert.That(deaths, Is.GreaterThanOrEqualTo(18), "blinded bots have stopped finding each other at all");
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
