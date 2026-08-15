using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class WallRegrowthTests
    {
        private static readonly MatchSettings Settings = MatchSettings.Default;

        private static MatchState OpenMatch(params GridPos[] spawns)
        {
            var state = new MatchState(new Arena(9, 9), Settings, 5u);
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
        public void ADestroyedBlockIsScheduledToComeBack()
        {
            MatchState state = OpenMatch(new GridPos(1, 1), new GridPos(7, 7));
            state.Arena[new GridPos(2, 1)] = TileKind.SoftBlock;
            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(1, 1), 99, 2), 1));

            Run(state, 2, PlayerInput.None, PlayerInput.None);

            Assert.That(state.Arena[new GridPos(2, 1)], Is.EqualTo(TileKind.Floor), "the blast cleared it");
            Assert.That(state.RegrowingWalls.Count, Is.EqualTo(1));
            Assert.That(state.RegrowingWalls[0].Tile, Is.EqualTo(new GridPos(2, 1)));
        }

        [Test]
        public void TheTileIsWalkableForTheWholeCountdown()
        {
            // The countdown is the warning. A tile that is already impassable while the
            // marker is showing tells the player nothing they can act on.
            MatchState state = OpenMatch(new GridPos(4, 4));
            state.ScheduleRegrowth(new GridPos(5, 4), TileKind.SoftBlock, Settings.WallRegrowTicks);

            Run(state, Settings.WallRegrowTicks - 2, PlayerInput.None);

            Assert.That(state.Arena[new GridPos(5, 4)], Is.EqualTo(TileKind.Floor));
            Assert.That(state.RegrowingWalls.Count, Is.EqualTo(1), "still pending");
        }

        [Test]
        public void TheWallIsBackWhenTheCountdownRunsOut()
        {
            MatchState state = OpenMatch(new GridPos(1, 1));
            state.ScheduleRegrowth(new GridPos(5, 4), TileKind.SoftBlock, Settings.WallRegrowTicks);

            Run(state, Settings.WallRegrowTicks + 1, PlayerInput.None);

            Assert.That(state.Arena[new GridPos(5, 4)], Is.EqualTo(TileKind.SoftBlock));
            Assert.That(state.RegrowingWalls, Is.Empty, "and it stops being pending");
        }

        [Test]
        public void APlayerCaughtInAClosingWallIsShovedOutAlive()
        {
            // Bombs are the only thing that kills. A wall that crushed would be a second
            // source of death and would break that rule for a mechanic nobody chose to
            // walk into.
            MatchState state = OpenMatch(new GridPos(4, 4));
            state.ScheduleRegrowth(new GridPos(4, 4), TileKind.SoftBlock, 2);

            Run(state, 3, PlayerInput.None);

            Assert.That(state.Players[0].Alive, Is.True, "a wall never kills");
            Assert.That(state.Players[0].Tile, Is.Not.EqualTo(new GridPos(4, 4)), "it moved them");
            Assert.That(state.Arena[new GridPos(4, 4)], Is.EqualTo(TileKind.SoftBlock));
        }

        [Test]
        public void AWallWaitsRatherThanCloseOverALiveBomb()
        {
            MatchState state = OpenMatch(new GridPos(1, 1), new GridPos(7, 7));
            state.ScheduleRegrowth(new GridPos(5, 4), TileKind.SoftBlock, 2);
            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(5, 4), 99, 1), 400));

            Run(state, 3, PlayerInput.None, PlayerInput.None);

            Assert.That(state.Arena[new GridPos(5, 4)], Is.EqualTo(TileKind.Floor), "not walled over");
            Assert.That(state.RegrowingWalls.Count, Is.EqualTo(1), "it comes back to try again");
        }

        [Test]
        public void AWallWithNowhereToShoveWaitsInsteadOfKilling()
        {
            // A player sealed on all four sides cannot be moved, so the wall gives up
            // for now rather than taking the only option left.
            MatchState state = OpenMatch(new GridPos(4, 4));
            state.Arena[new GridPos(3, 4)] = TileKind.HardBlock;
            state.Arena[new GridPos(5, 4)] = TileKind.HardBlock;
            state.Arena[new GridPos(4, 3)] = TileKind.HardBlock;
            state.Arena[new GridPos(4, 5)] = TileKind.HardBlock;
            state.ScheduleRegrowth(new GridPos(4, 4), TileKind.SoftBlock, 2);

            Run(state, 3, PlayerInput.None);

            Assert.That(state.Players[0].Alive, Is.True);
            Assert.That(state.Players[0].Tile, Is.EqualTo(new GridPos(4, 4)), "still standing there");
            Assert.That(state.Arena[new GridPos(4, 4)], Is.EqualTo(TileKind.Floor));
            Assert.That(state.RegrowingWalls.Count, Is.EqualTo(1));
        }

        [Test]
        public void AClosingWallTakesBackWhateverWasLyingThere()
        {
            // Otherwise an unwanted pickup would hold a corridor open for the rest of
            // the round, which is a stalemate by another route.
            MatchState state = OpenMatch(new GridPos(1, 1));
            state.AddLooseBomb(new GridPos(5, 4));
            state.ScheduleRegrowth(new GridPos(5, 4), TileKind.SoftBlock, 2);

            Run(state, 3, PlayerInput.None);

            Assert.That(state.Arena[new GridPos(5, 4)], Is.EqualTo(TileKind.SoftBlock));
            Assert.That(state.LooseBombIndexAt(new GridPos(5, 4)), Is.LessThan(0), "the one on that tile is gone");
        }

        [Test]
        public void ARegrownBlockCanBeBlownUpAndComeBackAgain()
        {
            MatchState state = OpenMatch(new GridPos(1, 1), new GridPos(7, 7));
            state.ScheduleRegrowth(new GridPos(5, 4), TileKind.SoftBlock, 2);
            Run(state, 3, PlayerInput.None, PlayerInput.None);
            Assert.That(state.Arena[new GridPos(5, 4)], Is.EqualTo(TileKind.SoftBlock));

            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(4, 4), 99, 2), 1));
            Run(state, 2, PlayerInput.None, PlayerInput.None);

            Assert.That(state.Arena[new GridPos(5, 4)], Is.EqualTo(TileKind.Floor));
            Assert.That(state.RegrowingWalls.Count, Is.EqualTo(1), "and is queued to return");
        }

        [Test]
        public void TheArenaStopsEmptyingOutOverALongMatch()
        {
            // The reason this mechanic exists: without it the board goes static and two
            // careful players circle each other until the timer.
            MatchState state = MatchFactory.Create(ArenaSettings.Default, Settings, 4, 808u);

            int start = CountSoftBlocks(state);
            var brains = new BotBrain[4];
            for (int i = 0; i < brains.Length; i++)
            {
                brains[i] = new BotBrain(i, BotSettings.Normal);
            }

            var inputs = new PlayerInput[4];
            for (int tick = 0; tick < 2500; tick++)
            {
                for (int i = 0; i < brains.Length; i++)
                {
                    inputs[i] = brains[i].Think(state);
                }

                MatchSim.Tick(state, inputs);
            }

            int left = CountSoftBlocks(state);
            Assert.That(left, Is.GreaterThan(start / 4), $"arena emptied to {left} of {start}");
        }

        private static int CountSoftBlocks(MatchState state)
        {
            int count = 0;
            for (int y = 0; y < state.Arena.Height; y++)
            {
                for (int x = 0; x < state.Arena.Width; x++)
                {
                    if (state.Arena[new GridPos(x, y)] == TileKind.SoftBlock)
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}
