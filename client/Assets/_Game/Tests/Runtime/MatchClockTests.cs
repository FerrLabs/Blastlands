using Blastlands.Core;
using Blastlands.Runtime;
using NUnit.Framework;

namespace Blastlands.Runtime.Tests
{
    // How long is left before the coast starts closing. The failure that matters is the
    // quiet one: a bar that says there is time when there is not, or that claims a
    // deadline exists in a match that has none.
    public class MatchClockTests
    {
        private static MatchState Match(SuddenDeathSettings suddenDeath)
        {
            var settings = MatchSettings.Default.WithSuddenDeath(suddenDeath);
            var state = new MatchState(new Arena(9, 9), settings, 1u);
            state.AddPlayer(new GridPos(1, 1));
            return state;
        }

        private static MatchState AtTick(int tick)
        {
            MatchState state = Match(SuddenDeathSettings.Default);
            var inputs = new PlayerInput[state.Players.Count];

            for (int i = 0; i < tick; i++)
            {
                MatchSim.Tick(state, inputs);
            }

            return state;
        }

        [Test]
        public void AFreshMatchHasAllOfItsTime()
        {
            Assert.That(MatchClock.Remaining(Match(SuddenDeathSettings.Default)), Is.EqualTo(1f));
        }

        [Test]
        public void TimeRunsOutExactlyWhenTheFirstRingLands()
        {
            int start = SuddenDeathSettings.Default.StartTicks;

            Assert.That(MatchClock.Remaining(AtTick(start - 1)), Is.GreaterThan(0f));
            Assert.That(MatchClock.Remaining(AtTick(start)), Is.EqualTo(0f));
        }

        [Test]
        public void TheBarStaysEmptyOnceTheCoastIsClosing()
        {
            // Not wrapping round to full, which is what an unclamped subtraction would
            // do the moment the tick passes the deadline.
            int start = SuddenDeathSettings.Default.StartTicks;

            Assert.That(MatchClock.Remaining(AtTick(start + 300)), Is.EqualTo(0f));
            Assert.That(MatchClock.Closing(AtTick(start + 300)), Is.True);
        }

        [Test]
        public void ClosingOnlyBeginsWhenItActuallyBegins()
        {
            int start = SuddenDeathSettings.Default.StartTicks;

            Assert.That(MatchClock.Closing(AtTick(start - 1)), Is.False);
            Assert.That(MatchClock.Closing(AtTick(start)), Is.True);
        }

        [Test]
        public void TheWarningComesBeforeTheRingAndNotWithIt()
        {
            // A warning that arrives at the same moment as the thing it warns about is
            // not a warning. It has to land while there is still time to move.
            int start = SuddenDeathSettings.Default.StartTicks;
            int warningAt = start - (int)(start * MatchClock.WarningShare) + 1;

            Assert.That(MatchClock.Warning(AtTick(warningAt)), Is.True);
            Assert.That(MatchClock.Warning(AtTick(start / 2)), Is.False, "warning far too early");
            Assert.That(MatchClock.Warning(AtTick(start)), Is.False, "still warning once it has begun");
        }

        [Test]
        public void AMatchWithoutSuddenDeathHasNoDeadlineToShow()
        {
            // The HUD hides the bar on this. Left visible and full it would be claiming
            // there is a clock running when there is not.
            MatchState state = Match(SuddenDeathSettings.Off);

            Assert.That(MatchClock.HasDeadline(state), Is.False);
            Assert.That(MatchClock.Closing(state), Is.False);
            Assert.That(MatchClock.Warning(state), Is.False);
        }

        [Test]
        public void ThereIsNoClockWithoutAMatch()
        {
            Assert.That(MatchClock.HasDeadline(null), Is.False);
            Assert.That(MatchClock.Warning(null), Is.False);
        }
    }
}
