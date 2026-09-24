using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class SuddenDeathTests
    {
        // Fast enough to assert on without simulating ninety seconds, same shape as the
        // real thing: a delay, then a ring every so often.
        private static readonly MatchSettings Settings = MatchSettings.Default
            .WithTilesPerLooseBomb(0)
            .WithSuddenDeath(new SuddenDeathSettings(10, 5));

        private static MatchState Open(params GridPos[] spawns)
        {
            var state = new MatchState(new Arena(15, 15), Settings, 7u);
            foreach (GridPos spawn in spawns)
            {
                state.AddPlayer(spawn);
            }

            return state;
        }

        private static void Run(MatchState state, int ticks)
        {
            var inputs = new PlayerInput[state.Players.Count];
            for (int i = 0; i < ticks; i++)
            {
                MatchSim.Tick(state, inputs);
            }
        }

        [Test]
        public void TheEdgeOfTheBoardGoesFirst()
        {
            MatchState state = Open(new GridPos(7, 7), new GridPos(6, 7));

            Run(state, 11);

            Assert.That(state.Arena[new GridPos(0, 7)], Is.EqualTo(TileKind.HardBlock), "the edge held");
            Assert.That(state.Arena[new GridPos(1, 7)], Is.EqualTo(TileKind.Floor), "it took the second ring too");
        }

        [Test]
        public void TheRingsWorkInward()
        {
            MatchState state = Open(new GridPos(7, 7), new GridPos(6, 7));

            Run(state, 21);

            Assert.That(state.SuddenDeathRings, Is.EqualTo(3));
            Assert.That(state.Arena[new GridPos(2, 7)], Is.EqualTo(TileKind.HardBlock), "the third ring held");
            Assert.That(state.Arena[new GridPos(3, 7)], Is.EqualTo(TileKind.Floor), "it went too deep");
        }

        [Test]
        public void AClosingRingKillsWhoeverStandsOnIt()
        {
            // The break with the regrowth rule, and the reason the mechanic works: a wall
            // that shoved instead would postpone the moment it had nowhere to shove to,
            // which is the stalemate this exists to end.
            MatchState state = Open(new GridPos(0, 7), new GridPos(7, 7));

            Run(state, 11);

            Assert.That(state.Players[0].Alive, Is.False, "stood on the coast and lived");
            Assert.That(state.Players[1].Alive, Is.True, "died in the middle of the board");
        }

        [Test]
        public void TheRingTakingTheLastTwoAtOnceGoesToWhoeverHeldOutNearestTheMiddle()
        {
            MatchState state = Open(new GridPos(0, 7), new GridPos(7, 7));
            state.Players[1].Position = new SubPos(SubPos.CentreOf(0), SubPos.CentreOf(3));
            state.Players[0].Position = new SubPos(SubPos.CentreOf(0), SubPos.CentreOf(6));

            Run(state, 11);

            Assert.That(state.Players[0].Alive, Is.False);
            Assert.That(state.Players[1].Alive, Is.False);
            Assert.That(state.Outcome, Is.EqualTo(RoundOutcome.Winner), "the ring cannot end a round in a draw");
            Assert.That(state.WinnerId, Is.EqualTo(0), "row 6 is nearer the middle of a 15 by 15 board than row 3");
        }

        [Test]
        public void TwoPlayersTheRingTakesAtTheSameDistanceStillDraw()
        {
            MatchState state = Open(new GridPos(0, 7), new GridPos(14, 7));

            Run(state, 11);

            Assert.That(state.Outcome, Is.EqualTo(RoundOutcome.Draw), "mirror images have nothing to split them");
        }

        [Test]
        public void ABlastThatTakesTheLastTwoIsStillADraw()
        {
            MatchState state = Open(new GridPos(7, 7), new GridPos(8, 7));
            state.AddFlame(new GridPos(7, 7), Settings.FlameTicks, 0);
            state.AddFlame(new GridPos(8, 7), Settings.FlameTicks, 0);

            Run(state, 1);

            Assert.That(state.Outcome, Is.EqualTo(RoundOutcome.Draw), "only the ring gets a tie-break");
        }

        [Test]
        public void ItTakesAnyoneWhoseBodyIsInIt()
        {
            // Movement is free, so the tile under a player's centre is not the whole of
            // where they are. Centred on tile 1 but leaning far enough that the body
            // covers tile 0, this player is two thirds inside the ring that closes.
            MatchState state = Open(new GridPos(1, 7), new GridPos(7, 7));
            state.Players[0].Position = new SubPos(306, SubPos.CentreOf(7));
            Assert.That(state.Players[0].Tile, Is.EqualTo(new GridPos(1, 7)), "the setup no longer leans");

            Run(state, 11);

            Assert.That(state.Players[0].Alive, Is.False, "survived with its body in the rock");
        }

        [Test]
        public void AClosingRingTakesTheBombLyingOnIt()
        {
            // A bomb sealed inside rock still counts down and still explodes, which puts
            // a blast where no player could have seen it coming.
            MatchState state = Open(new GridPos(7, 7), new GridPos(6, 7));
            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(0, 3), 99, 2), 600));

            Run(state, 11);

            Assert.That(state.HasBombAt(new GridPos(0, 3)), Is.False);
        }

        [Test]
        public void TurnedOffItNeverCloses()
        {
            var state = new MatchState(
                new Arena(15, 15),
                MatchSettings.Default.WithSuddenDeath(SuddenDeathSettings.Off),
                7u);
            state.AddPlayer(new GridPos(0, 7));
            state.AddPlayer(new GridPos(7, 7));

            Run(state, 200);

            Assert.That(state.SuddenDeathRings, Is.Zero);
            Assert.That(state.Arena[new GridPos(0, 7)], Is.EqualTo(TileKind.Floor));
        }

        [Test]
        public void ItLeavesTheIslandAlone()
        {
            // The coast of an eroded island is not the edge of the grid. A tile can sit
            // near the middle of the bounds and still be on the shore of a bay, and ring
            // one has to mean that shore rather than the rectangle.
            var arena = new Arena(15, 15);
            arena[new GridPos(7, 7)] = TileKind.Void;

            Assert.That(CoastDistance.Measure(arena)[(7 * 15) + 6], Is.EqualTo(1), "the bay is not a coast");
            Assert.That(CoastDistance.Measure(arena)[(7 * 15) + 5], Is.EqualTo(2));
        }

        [Test]
        public void EveryMatchEnds()
        {
            // The measurement #105 is actually about. Before this, twenty four-bot
            // matches decided 7 at 300 s and the same 7 at 600 s: waiting longer changed
            // nothing, because two survivors on an open island can dodge forever.
            //
            // Six thousand ticks is 200 s, comfortably past the point where the last ring
            // closes, so a match still running here is one nothing can end.
            for (uint seed = 1; seed <= 12; seed++)
            {
                MatchState state = MatchFactory.Create(ArenaSettings.Default, MatchSettings.Default, 4, seed);
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

        [Test]
        public void AClosingRingTakesARaisedWallWithIt()
        {
            MatchState state = Open(new GridPos(7, 7), new GridPos(6, 7));
            var tile = new GridPos(0, 5);
            state.Arena[tile] = TileKind.SoftBlock;
            state.AddRaisedWall(new RaisedWall(tile, 200));

            Run(state, 11);

            Assert.That(state.Arena[tile], Is.EqualTo(TileKind.HardBlock));
            Assert.That(state.RaisedWalls.Count, Is.Zero);
        }
    }
}
