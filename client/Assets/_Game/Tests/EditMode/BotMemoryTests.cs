using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    // The half of vision that makes it a mechanic rather than a rendering effect: a bot
    // reasoning about where it believes people are, and being wrong about it.
    //
    // These assert on the belief rather than on the step, deliberately. The first version
    // of this file read the bot's chosen direction, and every one of its tests passed for
    // the wrong reason: the planner walks Right when it has nothing to do, and towards
    // any destructible tile it can reach, so "walks Right" came out of both seeing the
    // opponent and seeing nothing at all. One end-to-end test at the bottom ties the
    // belief back to an actual decision.
    public class BotMemoryTests
    {
        private static readonly MatchSettings Settings = MatchSettings.Default.WithTilesPerLooseBomb(0);

        private static MatchState Match(GridPos hunter, GridPos prey)
        {
            var state = new MatchState(new Arena(15, 15), Settings, 5u);
            state.AddPlayer(hunter);
            state.AddPlayer(prey);
            return state;
        }

        private static BotBrain Watching(MatchState state, BotSettings level, int ticks)
        {
            var brain = new BotBrain(0, level);
            for (int i = 0; i < ticks; i++)
            {
                brain.Think(state);
                state.Tick++;
            }

            return brain;
        }

        [Test]
        public void ABotPlacesAnOpponentItCanSee()
        {
            MatchState state = Match(new GridPos(2, 7), new GridPos(6, 7));

            BotBrain brain = Watching(state, BotSettings.Hard, 1);

            Assert.That(brain.BelievesOpponentAt(new GridPos(6, 7)), Is.True);
        }

        [Test]
        public void ABotDoesNotPlaceAnOpponentHidingInABush()
        {
            MatchState state = Match(new GridPos(2, 7), new GridPos(6, 7));
            state.Arena[new GridPos(6, 7)] = TileKind.Bush;

            BotBrain brain = Watching(state, BotSettings.Hard, 1);

            Assert.That(brain.BelievesOpponentAt(new GridPos(6, 7)), Is.False);
        }

        [Test]
        public void ABotDoesNotPlaceAnOpponentBehindAWall()
        {
            MatchState state = Match(new GridPos(2, 7), new GridPos(6, 7));
            for (int y = 0; y < 15; y++)
            {
                state.Arena[new GridPos(4, y)] = TileKind.HardBlock;
            }

            BotBrain brain = Watching(state, BotSettings.Hard, 1);

            Assert.That(brain.BelievesOpponentAt(new GridPos(6, 7)), Is.False);
        }

        [Test]
        public void ABotHoldsOnToWhereItLastSawSomebody()
        {
            // Seen in the open, then the cover closes over them. The bot is entitled to
            // go on believing they are there, and does.
            MatchState state = Match(new GridPos(2, 7), new GridPos(6, 7));
            BotBrain brain = Watching(state, BotSettings.Hard, 1);

            state.Arena[new GridPos(6, 7)] = TileKind.Bush;
            brain.Think(state);

            Assert.That(brain.BelievesOpponentAt(new GridPos(6, 7)), Is.True);
        }

        [Test]
        public void ASightingOldEnoughToBeWorthlessIsDropped()
        {
            MatchState state = Match(new GridPos(2, 7), new GridPos(6, 7));
            BotBrain brain = Watching(state, BotSettings.Hard, 1);

            state.Arena[new GridPos(6, 7)] = TileKind.Bush;
            state.Tick += BotSettings.Hard.MemoryTicks + 1;
            brain.Think(state);

            Assert.That(brain.BelievesOpponentAt(new GridPos(6, 7)), Is.False);
        }

        [Test]
        public void AnEasyBotForgetsSoonerThanAHardOne()
        {
            Assert.That(BotSettings.Easy.MemoryTicks, Is.LessThan(BotSettings.Normal.MemoryTicks));
            Assert.That(BotSettings.Normal.MemoryTicks, Is.LessThan(BotSettings.Hard.MemoryTicks));

            MatchState easy = Match(new GridPos(2, 7), new GridPos(6, 7));
            MatchState hard = Match(new GridPos(2, 7), new GridPos(6, 7));
            BotBrain easyBrain = Watching(easy, BotSettings.Easy, 1);
            BotBrain hardBrain = Watching(hard, BotSettings.Hard, 1);

            easy.Arena[new GridPos(6, 7)] = TileKind.Bush;
            hard.Arena[new GridPos(6, 7)] = TileKind.Bush;

            int elapsed = BotSettings.Easy.MemoryTicks + 1;
            easy.Tick += elapsed;
            hard.Tick += elapsed;
            easyBrain.Think(easy);
            hardBrain.Think(hard);

            Assert.That(easyBrain.BelievesOpponentAt(new GridPos(6, 7)), Is.False, "still chasing a ghost");
            Assert.That(hardBrain.BelievesOpponentAt(new GridPos(6, 7)), Is.True, "gave up too early");
        }

        [Test]
        public void ASightingTheBotCanSeeIsWrongIsDroppedAtOnce()
        {
            // Watching the tile they were on and finding it empty is different from
            // losing them: the belief is disproven rather than merely old, and a bot
            // that keeps hunting a corner it can see is empty looks broken, not fooled.
            MatchState state = Match(new GridPos(2, 7), new GridPos(6, 7));
            BotBrain brain = Watching(state, BotSettings.Hard, 1);
            Assert.That(brain.BelievesOpponentAt(new GridPos(6, 7)), Is.True);

            state.Players[1].Position = SubPos.AtTileCentre(new GridPos(6, 2));
            state.Arena[new GridPos(6, 2)] = TileKind.Bush;
            brain.Think(state);

            Assert.That(brain.BelievesOpponentAt(new GridPos(6, 7)), Is.False);
        }

        [Test]
        public void ADeadOpponentIsForgotten()
        {
            MatchState state = Match(new GridPos(2, 7), new GridPos(6, 7));
            BotBrain brain = Watching(state, BotSettings.Hard, 1);

            state.Players[1].Alive = false;
            state.Arena[new GridPos(6, 7)] = TileKind.Bush;
            brain.Think(state);

            Assert.That(brain.BelievesOpponentAt(new GridPos(6, 7)), Is.False);
        }

        [Test]
        public void ABotBombsAnOpponentInTheOpenAndNotOneInABush()
        {
            // The end-to-end half: the belief has to reach a decision, or the model is
            // just bookkeeping. Two tiles apart is inside the starting fire range and
            // outside the bot's own neighbourhood, so the bush cannot pull the decision
            // through the "there is a destructible block next to me" route instead.
            MatchState open = Match(new GridPos(6, 7), new GridPos(8, 7));
            MatchState hidden = Match(new GridPos(6, 7), new GridPos(8, 7));
            hidden.Arena[new GridPos(8, 7)] = TileKind.Bush;

            Assert.That(new BotBrain(0, BotSettings.Hard).Think(open).DropBomb, Is.True);
            Assert.That(new BotBrain(0, BotSettings.Hard).Think(hidden).DropBomb, Is.False);
        }

        [Test]
        public void TwoBotsWithTheSameHistoryStillDecideIdentically()
        {
            // Memory is per-brain state, which is exactly the kind of thing that breaks
            // a lockstep simulation if it ever depends on anything but the ticks it saw.
            MatchState first = MatchFactory.Create(ArenaSettings.Default, Settings, 4, 77u);
            MatchState second = MatchFactory.Create(ArenaSettings.Default, Settings, 4, 77u);
            var a = new BotBrain(0, BotSettings.Normal);
            var b = new BotBrain(0, BotSettings.Normal);

            var inputsA = new PlayerInput[4];
            var inputsB = new PlayerInput[4];

            for (int tick = 0; tick < 120; tick++)
            {
                inputsA[0] = a.Think(first);
                inputsB[0] = b.Think(second);

                Assert.That(inputsA[0].Move, Is.EqualTo(inputsB[0].Move), $"diverged at tick {tick}");
                Assert.That(inputsA[0].DropBomb, Is.EqualTo(inputsB[0].DropBomb), $"diverged at tick {tick}");

                MatchSim.Tick(first, inputsA);
                MatchSim.Tick(second, inputsB);
            }
        }
    }
}
