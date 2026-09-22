using System;
using Blastlands.Core.Net;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class CorrectionSmootherTests
    {
        [Test]
        public void TheFrameAfterACorrectionDrawsThePlayerWhereItAlreadyDrewThem()
        {
            var smoother = new CorrectionSmoother();
            var before = new SubPos(1000, 800);
            var after = new SubPos(1040, 770);

            smoother.Corrected(before, after);

            Assert.That(smoother.Apply(after), Is.EqualTo(before));
        }

        [Test]
        public void ACorrectionIsWorkedOffWithoutOvershootingOrStalling()
        {
            var smoother = new CorrectionSmoother();
            smoother.Corrected(new SubPos(1000, 800), new SubPos(1040, 770));

            int previous = Math.Abs(smoother.Offset.X);
            for (int tick = 0; tick < 40 && smoother.Offset.X != 0; tick++)
            {
                smoother.Tick();
                int now = Math.Abs(smoother.Offset.X);
                Assert.That(smoother.Offset.X, Is.LessThanOrEqualTo(0), $"crossed zero at tick {tick}");
                Assert.That(now, Is.LessThan(previous), $"stalled at tick {tick}");
                previous = now;
            }

            Assert.That(smoother.Offset, Is.EqualTo(new SubPos(0, 0)));
        }

        [Test]
        public void CorrectionsThatKeepComingAreAbsorbedIntoTheSameOffset()
        {
            var smoother = new CorrectionSmoother();
            smoother.Corrected(new SubPos(1000, 800), new SubPos(1020, 800));
            smoother.Tick();
            int first = smoother.Offset.X;
            smoother.Corrected(new SubPos(1100, 800), new SubPos(1110, 800));

            Assert.That(smoother.Offset.X, Is.EqualTo(first - 10));
        }

        [Test]
        public void ADivergenceTooBigToLieAboutIsSnapped()
        {
            var smoother = new CorrectionSmoother();

            smoother.Corrected(new SubPos(1000, 800), new SubPos(1000 + SubPos.UnitsPerTile + 1, 800));

            Assert.That(smoother.Offset, Is.EqualTo(new SubPos(0, 0)));
        }

        [Test]
        public void ASmallDivergenceOnTopOfAnOldOneStillSnapsWhenTheSumIsTooBig()
        {
            var smoother = new CorrectionSmoother();
            smoother.Corrected(new SubPos(0, 0), new SubPos(-SubPos.UnitsPerTile, 0));

            smoother.Corrected(new SubPos(0, 0), new SubPos(-8, 0));

            Assert.That(smoother.Offset, Is.EqualTo(new SubPos(0, 0)));
        }
    }
}
