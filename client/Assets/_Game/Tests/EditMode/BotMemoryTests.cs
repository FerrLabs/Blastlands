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
        public void ABotBombsAnOpponentItCanTrapAndNotOneItCannotSee()
        {
            // The end-to-end half: the belief has to reach a decision, or the model is
            // just bookkeeping.
            //
            // Trapped rather than merely in range, because that is the only reason the
            // bot spends a bomb on a person now. Bombing someone who is simply nearby was
            // measured not to kill anyone: a fuse is two and a half seconds and every bot
            // reads a blast map, so they walk out and the bomb is gone.
            MatchState seen = DeadEnd();
            MatchState hidden = DeadEnd();
            hidden.Arena[new GridPos(1, 7)] = TileKind.Bush;

            Assert.That(new BotBrain(0, BotSettings.Hard).Think(seen).DropBomb, Is.True);
            Assert.That(new BotBrain(0, BotSettings.Hard).Think(hidden).DropBomb, Is.False);
        }

        // A two-tile pocket with one way out, the bot standing in the mouth of it. The
        // bomb it drops is what seals the pocket: a bomb is solid once it is down, so
        // the only tiles left to the opponent are the two inside, and both burn.
        //
        // This is the only geometry that traps anyone. With a fuse of seventy-five ticks
        // and a radius of two, a target in an open corridor covers seven tiles before it
        // goes off and walks out of the blast at a stroll.
        private static MatchState DeadEnd()
        {
            var arena = new Arena(15, 15);
            foreach (GridPos wall in new[]
            {
                new GridPos(0, 7), new GridPos(1, 6), new GridPos(1, 8),
                new GridPos(2, 6), new GridPos(2, 8), new GridPos(3, 6), new GridPos(3, 8)
            })
            {
                arena[wall] = TileKind.HardBlock;
            }

            var state = new MatchState(arena, Settings, 5u);
            state.AddPlayer(new GridPos(3, 7));
            state.AddPlayer(new GridPos(1, 7));
            return state;
        }

        [Test]
        public void ABotShovesSomebodyIntoABlastItCanSeeComing()
        {
            // The shove was the one kill in the game the bots never used, and it is the
            // only thing available to them that lands on the tick it happens rather than
            // two and a half seconds later.
            var arena = new Arena(15, 15);
            var state = new MatchState(arena, Settings, 8u);
            state.AddPlayer(new GridPos(5, 7));
            state.AddPlayer(new GridPos(6, 7));
            state.Players[0].Facing = Direction.Right;

            // Somebody else's bomb, about to go off, covering the ground the target
            // would be carried across but not the tile the shover is standing on. The
            // fuse has to be short: a shove is over in a third of a second, so a bomb
            // that goes off later than that is one the target walks away from.
            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(9, 7), 99, 3), 3));

            PlayerInput decision = new BotBrain(0, BotSettings.Hard).Think(state);

            Assert.That(decision.Push, Is.True);
        }

        [Test]
        public void ABotDoesNotShoveSomebodyOntoSafeGround()
        {
            // Without this the shove is a tic rather than a plan: pushing whoever walks
            // past achieves nothing, costs the cooldown, and gives the bot away.
            var arena = new Arena(15, 15);
            var state = new MatchState(arena, Settings, 8u);
            state.AddPlayer(new GridPos(5, 7));
            state.AddPlayer(new GridPos(6, 7));
            state.Players[0].Facing = Direction.Right;

            Assert.That(new BotBrain(0, BotSettings.Hard).Think(state).Push, Is.False);
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
