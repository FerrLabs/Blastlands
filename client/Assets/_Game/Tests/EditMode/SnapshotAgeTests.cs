using Blastlands.Core.Net;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class SnapshotAgeTests
    {
        private static MatchState TwoApart()
        {
            var state = new MatchState(new Arena(15, 5), MatchSettings.Classic, 1u);
            state.AddPlayer(new GridPos(1, 1));
            state.AddPlayer(new GridPos(13, 3));
            return state;
        }

        private static readonly PlayerInput[] Idle = { PlayerInput.None, PlayerInput.None };

        [Test]
        public void AFlameAgedByTicksGoesOutOnTheTickTheSimulationPutsItOut()
        {
            for (int remaining = 1; remaining <= 6; remaining++)
            {
                for (int ticks = 0; ticks <= 8; ticks++)
                {
                    MatchState state = TwoApart();
                    var tile = new GridPos(7, 1);
                    state.AddFlame(tile, remaining, state.Tick);
                    bool aged = SnapshotAge.BurnsAfter(state.Flames[0], ticks);

                    for (int i = 0; i < ticks; i++)
                    {
                        MatchSim.Tick(state, Idle);
                    }

                    Assert.That(aged, Is.EqualTo(state.HasFlameAt(tile)), $"{remaining} left, {ticks} ticks on");
                }
            }
        }

        [Test]
        public void ABombAgedByTicksShowsTheFuseTheSimulationHasLeft()
        {
            MatchState state = TwoApart();
            var bomb = new ActiveBomb(new Bomb(new GridPos(7, 2), 0, 1, BombKind.Standard), 10);
            state.AddBomb(bomb);
            var snapshot = new ActiveBomb(bomb.Bomb, bomb.FuseTicks) { FuseRemaining = bomb.FuseRemaining };

            for (int ticks = 1; ticks < 10; ticks++)
            {
                MatchSim.Tick(state, Idle);
                Assert.That(SnapshotAge.FuseAfter(snapshot, ticks), Is.EqualTo(state.Bombs[0].FuseRemaining), $"{ticks} ticks on");
            }
        }

        [Test]
        public void AFuseNeverShowsLessThanNothingOrMoreThanItHad()
        {
            var bomb = new ActiveBomb(new Bomb(new GridPos(1, 1), 0, 1, BombKind.Standard), 10) { FuseRemaining = 4 };

            Assert.That(SnapshotAge.FuseAfter(bomb, 9), Is.EqualTo(0));
            Assert.That(SnapshotAge.FuseAfter(bomb, -3), Is.EqualTo(4));
        }
    }
}
