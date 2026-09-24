using Blastlands.Runtime;
using NUnit.Framework;

namespace Blastlands.Runtime.Tests
{
    // The one place frame time is allowed to reach the simulation, so it is the one
    // place a frame rate could change how a match plays out.
    public class TickPacerTests
    {
        private const int Rate = 30;
        private const int Cap = 5;

        private static TickPacer Pacer()
        {
            return new TickPacer(Rate, Cap);
        }

        [Test]
        public void TheFractionIsHowFarIntoTheNextTickTheClockIs()
        {
            TickPacer pacer = Pacer();

            Assert.That(pacer.Advance(1f / Rate * 1.5f), Is.EqualTo(1));
            Assert.That(pacer.Fraction, Is.EqualTo(0.5f).Within(0.01f));

            pacer.Advance(1f / Rate * 0.25f);
            Assert.That(pacer.Fraction, Is.EqualTo(0.75f).Within(0.01f));
        }

        [Test]
        public void AFrameShorterThanATickRunsNothingYet()
        {
            Assert.That(Pacer().Advance(0.01f), Is.EqualTo(0));
        }

        [Test]
        public void TheLeftoverIsCarriedRatherThanDropped()
        {
            // 20ms frames are 50 a second against 30 ticks, so a tick is owed every one
            // and a half frames and most frames run none. Dropping what is left over
            // each time would run 30 ticks a second at 30fps and none at all at 50, and
            // the same match would play at different speeds on different screens.
            //
            // Counted over frames rather than per frame: 1f/30f is a hair over a
            // thirtieth, so a frame landing exactly on the boundary can fall either side
            // of it. The running total cannot.
            TickPacer pacer = Pacer();
            int ticks = 0;

            for (int frame = 0; frame < 3; frame++)
            {
                ticks += pacer.Advance(0.02f);
            }

            Assert.That(ticks, Is.EqualTo(1), "60ms is one and four fifths of a tick");

            for (int frame = 0; frame < 3; frame++)
            {
                ticks += pacer.Advance(0.02f);
            }

            Assert.That(ticks, Is.EqualTo(3), "120ms is three and three fifths of a tick");
        }

        [Test]
        public void ASlowFrameRunsEveryTickItOwes()
        {
            // 110ms is three and a third ticks at 30Hz, and the match has to advance all
            // three or the simulation falls behind the clock everybody else is on.
            Assert.That(Pacer().Advance(0.11f), Is.EqualTo(3));
        }

        [Test]
        public void AStallCannotAskForMoreTicksThanTheCap()
        {
            Assert.That(Pacer().Advance(10f), Is.EqualTo(Cap));
        }

        [Test]
        public void TheDebtFromAStallIsWrittenOffRatherThanCarried()
        {
            // Ten seconds is three hundred ticks and the cap allows five. Keeping the
            // remaining 295 would cap every frame after it too, running flat out and
            // never catching up, which reads as the game stuck in fast forward.
            TickPacer pacer = Pacer();

            Assert.That(pacer.Advance(10f), Is.EqualTo(Cap));
            Assert.That(pacer.Advance(0.01f), Is.EqualTo(0), "still paying off the stall");
        }

        [Test]
        public void OneSecondOfFramesIsOneSecondOfTicksWhateverTheFrameRate()
        {
            // The property the whole fixed step exists for. Same second of wall clock,
            // three different frame rates, same number of ticks.
            foreach (int fps in new[] { 30, 60, 144 })
            {
                TickPacer pacer = Pacer();
                int ticks = 0;

                for (int frame = 0; frame < fps; frame++)
                {
                    ticks += pacer.Advance(1f / fps);
                }

                Assert.That(ticks, Is.EqualTo(Rate).Within(1), $"at {fps} fps");
            }
        }

        [Test]
        public void ANonsenseRateDoesNotDivideByZero()
        {
            Assert.That(new TickPacer(0, Cap).Advance(1f), Is.LessThanOrEqualTo(Cap));
            Assert.That(new TickPacer(Rate, 0).Advance(1f), Is.GreaterThan(0));
        }
    }
}
