using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class BotBrainTests
    {
        private static readonly MatchSettings Settings = MatchSettings.Default;

        private static MatchState OpenMatch(int width, int height, params GridPos[] spawns)
        {
            var state = new MatchState(new Arena(width, height), Settings, 7u);
            foreach (GridPos spawn in spawns)
            {
                state.AddPlayer(spawn);
            }

            return state;
        }

        private static MatchState SealedMatch(int width, int height, GridPos spawn)
        {
            var arena = new Arena(width, height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    arena[new GridPos(x, y)] = TileKind.HardBlock;
                }
            }

            arena[spawn] = TileKind.Floor;

            var state = new MatchState(arena, Settings, 7u);
            state.AddPlayer(spawn);
            return state;
        }

        private static int CountSoftBlocks(Arena arena)
        {
            int count = 0;
            for (int y = 0; y < arena.Height; y++)
            {
                for (int x = 0; x < arena.Width; x++)
                {
                    if (arena[new GridPos(x, y)] == TileKind.SoftBlock)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        [Test]
        public void ABotStandingInABlastWalksOutOfIt()
        {
            MatchState state = OpenMatch(9, 9, new GridPos(1, 1));
            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(1, 1), 99, 3), 20));

            PlayerInput decision = new BotBrain(0, BotSettings.Hard).Think(state);

            Assert.That(decision.Move, Is.Not.EqualTo(Direction.None), "the bot is sitting on a live bomb");
            Assert.That(decision.DropBomb, Is.False, "fleeing is not the moment to add another bomb");
        }

        [Test]
        public void ABotBombsAnAdjacentSoftBlockWhenItHasAWayOut()
        {
            MatchState state = OpenMatch(9, 9, new GridPos(1, 1));
            state.Arena[new GridPos(2, 1)] = TileKind.SoftBlock;

            Assert.That(new BotBrain(0, BotSettings.Hard).Think(state).DropBomb, Is.True);
        }

        [Test]
        public void ABotRefusesABombItCouldNotEscape()
        {
            // A pocket whose only exit is the soft block itself: bombing it is suicide.
            MatchState state = SealedMatch(5, 5, new GridPos(1, 1));
            state.Arena[new GridPos(1, 2)] = TileKind.SoftBlock;

            PlayerInput decision = new BotBrain(0, BotSettings.Hard).Think(state);

            Assert.That(decision.DropBomb, Is.False, "the bot would have nowhere to run");
        }

        [Test]
        public void TwoBrainsSeeingTheSameStateDecideIdentically()
        {
            MatchState state = MatchFactory.Create(ArenaSettings.Default, Settings, 4, 99u);

            PlayerInput first = new BotBrain(0, BotSettings.Normal).Think(state);
            PlayerInput second = new BotBrain(0, BotSettings.Normal).Think(state);

            Assert.That(first.Move, Is.EqualTo(second.Move));
            Assert.That(first.DropBomb, Is.EqualTo(second.DropBomb));
        }

        [Test]
        public void MostBotsLeftAloneSurviveThemselves()
        {
            // A Hard bot survives 30 of these 40 unsupervised matches, up from 13 before
            // the bombing check stopped accepting ground that merely burns later, and
            // down from 35 since walls started growing back and taking escape routes
            // with them. The bar stays below the real figure and moves up as the planner
            // does.
            //
            // An earlier version of this test ran one seed that happened to be among the
            // survivors, and reported the bots as safe for as long as it existed.
            int survived = 0;
            for (uint seed = 1; seed <= 40; seed++)
            {
                if (SurvivesAlone(seed, BotSettings.Hard, 3000))
                {
                    survived++;
                }
            }

            Assert.That(survived, Is.GreaterThanOrEqualTo(25), "bot self-preservation has regressed");
        }

        [Test]
        public void ABotKeepsMovingTheTickAfterDroppingABomb()
        {
            // Dropping carries no direction. Taking the heading from it parked the bot on
            // its own bomb for its whole reaction delay, which is the entire budget an
            // Easy bot has to get clear.
            foreach (BotSettings level in new[] { BotSettings.Easy, BotSettings.Normal, BotSettings.Hard })
            {
                for (uint seed = 1; seed <= 10; seed++)
                {
                    MatchState state = MatchFactory.Create(ArenaSettings.Default, Settings, 1, seed);
                    var brain = new BotBrain(0, level);
                    var inputs = new PlayerInput[1];

                    for (int tick = 0; tick < 400 && state.Players[0].Alive; tick++)
                    {
                        inputs[0] = brain.Think(state);
                        bool dropped = inputs[0].DropBomb;
                        MatchSim.Tick(state, inputs);

                        if (!dropped)
                        {
                            continue;
                        }

                        Assert.That(
                            brain.Think(state).Move,
                            Is.Not.EqualTo(Direction.None),
                            $"froze on its own bomb (seed {seed})");
                        break;
                    }
                }
            }
        }

        private static bool SurvivesAlone(uint seed, BotSettings level, int ticks)
        {
            MatchState state = MatchFactory.Create(ArenaSettings.Default, Settings, 1, seed);
            var brain = new BotBrain(0, level);
            var inputs = new PlayerInput[1];

            for (int tick = 0; tick < ticks; tick++)
            {
                inputs[0] = brain.Think(state);
                MatchSim.Tick(state, inputs);

                if (!state.Players[0].Alive)
                {
                    return false;
                }
            }

            return true;
        }

        [Test]
        public void BotsClearTheArenaRatherThanStandingStill()
        {
            MatchState state = MatchFactory.Create(ArenaSettings.Default, Settings, 4, 1234u);
            int before = CountSoftBlocks(state.Arena);

            var brains = new BotBrain[state.Players.Count];
            for (int i = 0; i < brains.Length; i++)
            {
                brains[i] = new BotBrain(i, BotSettings.Normal);
            }

            var inputs = new PlayerInput[brains.Length];
            for (int tick = 0; tick < 2000; tick++)
            {
                for (int i = 0; i < brains.Length; i++)
                {
                    inputs[i] = brains[i].Think(state);
                }

                MatchSim.Tick(state, inputs);
            }

            Assert.That(CountSoftBlocks(state.Arena), Is.LessThan(before), "the bots never destroyed anything");
        }

        [Test]
        public void AnEasyBotReactsMoreSlowlyThanAHardOne()
        {
            // Difficulty is a handicap, not hidden information: the easy bot simply
            // keeps its previous decision for longer.
            MatchState state = OpenMatch(9, 9, new GridPos(4, 4));
            var easy = new BotBrain(0, BotSettings.Easy);
            var hard = new BotBrain(0, BotSettings.Hard);

            easy.Think(state);
            hard.Think(state);

            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(4, 4), 99, 3), 40));

            Assert.That(hard.Think(state).Move, Is.Not.EqualTo(Direction.None), "the hard bot reacts at once");
            Assert.That(easy.Think(state).Move, Is.EqualTo(Direction.None), "the easy bot is still on its delay");
        }

        [Test]
        public void ADeadBotStopsProducingInput()
        {
            MatchState state = OpenMatch(9, 9, new GridPos(1, 1));
            state.Players[0].Alive = false;

            PlayerInput decision = new BotBrain(0, BotSettings.Hard).Think(state);

            Assert.That(decision.Move, Is.EqualTo(Direction.None));
            Assert.That(decision.DropBomb, Is.False);
        }
    }
}
