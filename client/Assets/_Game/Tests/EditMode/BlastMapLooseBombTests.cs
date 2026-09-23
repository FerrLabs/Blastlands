using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    // A loose bomb lying on the floor is not a bomb yet, and becomes one the moment fire
    // reaches it. The danger map has to say so, or a bot reads clear ground where a
    // chain is about to run.
    //
    // Every case is a one-tile corridor. A blast in the open is a cross with a flare at
    // each tip, and which tiles that covers is the explosion resolver's business, tested
    // where it lives. Here the geometry has to be unambiguous so the assertions are
    // about timing.
    public class BlastMapLooseBombTests
    {
        private static readonly MatchSettings Settings =
            MatchSettings.Default.WithTilesPerLooseBomb(0).WithSuddenDeath(SuddenDeathSettings.Off);

        private static MatchState Corridor(int width)
        {
            var arena = new Arena(width, 3);
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    arena[new GridPos(x, y)] = TileKind.HardBlock;
                }
            }

            for (int x = 1; x < width - 1; x++)
            {
                arena[new GridPos(x, 1)] = TileKind.Floor;
            }

            var state = new MatchState(arena, Settings, 11u);
            state.AddPlayer(new GridPos(1, 1));
            return state;
        }

        [Test]
        public void ALooseBombInAComingBlastBurnsAfterItsOwnFuse()
        {
            MatchState state = Corridor(12);
            state.AddLooseBomb(new GridPos(5, 1));
            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(7, 1), 99, 2), 40));

            BlastMap blast = BlastMap.From(state);

            Assert.That(blast.TicksUntilFire(new GridPos(5, 1)), Is.EqualTo(40), "the blast reaches it at 40");
            Assert.That(
                blast.TicksUntilFire(new GridPos(3, 1)),
                Is.EqualTo(40 + Settings.LooseBombFuseTicks),
                "and what it throws down the corridor burns a fuse later");
        }

        [Test]
        public void ALooseBombNothingReachesIsNotADanger()
        {
            MatchState state = Corridor(12);
            state.AddLooseBomb(new GridPos(5, 1));

            BlastMap blast = BlastMap.From(state);

            Assert.That(blast.TicksUntilFire(new GridPos(5, 1)), Is.EqualTo(BlastMap.Never));
            Assert.That(blast.TicksUntilFire(new GridPos(3, 1)), Is.EqualTo(BlastMap.Never));
        }

        // The ordering case. The first loose bomb in the list is reached late by one
        // bomb and early by a chain, and the flames it throws have to move to the
        // earlier time rather than staying where the first pass wrote them.
        [Test]
        public void ALooseBombReachedSoonerThanFirstThoughtMovesItsFlamesWithIt()
        {
            MatchState state = Corridor(16);

            // List order matters: the one lit late comes first, so it is resolved at 200
            // before anything has lowered it.
            state.AddLooseBomb(new GridPos(7, 1));
            state.AddLooseBomb(new GridPos(9, 1));

            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(5, 1), 99, 2), 200));
            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(11, 1), 99, 2), 10));

            BlastMap blast = BlastMap.From(state);
            int fuse = Settings.LooseBombFuseTicks;

            Assert.That(
                blast.TicksUntilFire(new GridPos(9, 1)),
                Is.EqualTo(10),
                "the quick bomb reaches the second loose one straight away");

            Assert.That(
                blast.TicksUntilFire(new GridPos(7, 1)),
                Is.EqualTo(10 + fuse),
                "whose chain reaches the first one long before the slow bomb does");

            // Read as one explosion rather than two fuses in sequence: a loose bomb the
            // chain reaches is already among the definitions, so the resolver sets it
            // off with the rest. That calls the fire twelve ticks early, which is the
            // safe direction for something a bot flees on.
            //
            // What must not happen is the other direction, and it is what this case is
            // here for: before the re-light, this tile stayed recorded at 212, the slow
            // bomb's clock, while the ground it sits on was about to burn at 22.
            Assert.That(
                blast.TicksUntilFire(new GridPos(6, 1)),
                Is.EqualTo(10 + fuse),
                "what the first loose bomb throws burns on the chain's clock");

            Assert.That(
                blast.TicksUntilFire(new GridPos(6, 1)),
                Is.LessThan(200),
                "nothing may still be recorded against the bomb that was overtaken");
        }
    }
}
