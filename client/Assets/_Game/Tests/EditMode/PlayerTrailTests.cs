using Blastlands.Core.Net;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class PlayerTrailTests
    {
        private const int Unit = InterpolationClock.UnitsPerTick;

        private static MatchState OneRunner()
        {
            var state = new MatchState(new Arena(40, 5), MatchSettings.Classic, 1u);
            state.AddPlayer(new GridPos(1, 1));
            state.AddPlayer(new GridPos(1, 3));
            return state;
        }

        private static void Put(PlayerTrail trail, MatchState state, int tick, int x)
        {
            state.Players[0].Position = new SubPos(x, 384);
            trail.Record(tick, state.Players);
        }

        private static SubPos At(PlayerTrail trail, long time)
        {
            Assert.That(trail.TrySample(0, time, out SubPos position), Is.True);
            return position;
        }

        [Test]
        public void HalfwayBetweenTwoSnapshotsIsHalfwayBetweenTheirPositions()
        {
            MatchState state = OneRunner();
            var trail = new PlayerTrail(2, 8);
            Put(trail, state, 10, 1000);
            Put(trail, state, 11, 1030);

            Assert.That(At(trail, (10 * Unit) + (Unit / 2)).X, Is.EqualTo(1015));
            Assert.That(At(trail, (10 * Unit) + (Unit / 2)).Y, Is.EqualTo(384));
        }

        [Test]
        public void ALostSnapshotIsBridgedByTheTicksEitherSideOfIt()
        {
            MatchState state = OneRunner();
            var trail = new PlayerTrail(2, 8);
            Put(trail, state, 10, 1000);
            Put(trail, state, 12, 1060);

            Assert.That(At(trail, 11 * Unit).X, Is.EqualTo(1030));
        }

        [Test]
        public void ATeleportHoldsItsStartUntilItsTickRatherThanSlidingAcrossTheBoard()
        {
            MatchState state = OneRunner();
            var trail = new PlayerTrail(2, 8);
            Put(trail, state, 10, 1000);
            Put(trail, state, 11, 1000 + (5 * SubPos.UnitsPerTile));

            Assert.That(At(trail, (10 * Unit) + 900).X, Is.EqualTo(1000));
            Assert.That(At(trail, 11 * Unit).X, Is.EqualTo(1000 + (5 * SubPos.UnitsPerTile)));
        }

        [Test]
        public void AFastRunAcrossLostSnapshotsIsNotMistakenForATeleport()
        {
            MatchState state = OneRunner();
            var trail = new PlayerTrail(2, 8);
            int perTick = MatchSettings.Classic.DashSpeed;
            Put(trail, state, 10, 1000);
            Put(trail, state, 14, 1000 + (4 * perTick));

            Assert.That(At(trail, 12 * Unit).X, Is.EqualTo(1000 + (2 * perTick)));
        }

        [Test]
        public void ADashIsFastButStillGlides()
        {
            MatchState state = OneRunner();
            var trail = new PlayerTrail(2, 8);
            Put(trail, state, 10, 1000);
            Put(trail, state, 11, 1000 + MatchSettings.Classic.DashSpeed);

            Assert.That(At(trail, (10 * Unit) + (Unit / 2)).X, Is.EqualTo(1000 + (MatchSettings.Classic.DashSpeed / 2)));
        }

        [Test]
        public void PastTheNewestSnapshotItHoldsRatherThanGuesses()
        {
            MatchState state = OneRunner();
            var trail = new PlayerTrail(2, 8);
            Put(trail, state, 10, 1000);
            Put(trail, state, 11, 1030);

            Assert.That(At(trail, 14 * Unit).X, Is.EqualTo(1030));
            Assert.That(At(trail, 2 * Unit).X, Is.EqualTo(1000));
        }

        [Test]
        public void ALateSnapshotOlderThanTheNewestIsIgnored()
        {
            MatchState state = OneRunner();
            var trail = new PlayerTrail(2, 8);
            Put(trail, state, 10, 1000);
            Put(trail, state, 12, 1060);
            Put(trail, state, 11, 5000);

            Assert.That(At(trail, 11 * Unit).X, Is.EqualTo(1030));
        }

        [Test]
        public void AFullRingDropsTheOldestAndKeepsInterpolatingTheNewest()
        {
            MatchState state = OneRunner();
            var trail = new PlayerTrail(2, 4);
            for (int tick = 0; tick < 11; tick++)
            {
                Put(trail, state, tick, 1000 + (tick * 20));
            }

            Assert.That(trail.Count, Is.EqualTo(4));
            Assert.That(At(trail, (9 * Unit) + (Unit / 4)).X, Is.EqualTo(1185));
            Assert.That(At(trail, 0).X, Is.EqualTo(1140));
        }

        [Test]
        public void EachPlayerIsSampledFromTheirOwnTrack()
        {
            MatchState state = OneRunner();
            var trail = new PlayerTrail(2, 8);
            state.Players[1].Position = new SubPos(2000, 900);
            Put(trail, state, 10, 1000);
            state.Players[1].Position = new SubPos(2000, 1000);
            Put(trail, state, 11, 1030);

            Assert.That(trail.TrySample(1, (10 * Unit) + (Unit / 2), out SubPos other), Is.True);
            Assert.That(other, Is.EqualTo(new SubPos(2000, 950)));
        }

        [Test]
        public void NothingRecordedMeansNothingToSample()
        {
            var trail = new PlayerTrail(2, 8);

            Assert.That(trail.TrySample(0, 0, out _), Is.False);
        }
    }
}
