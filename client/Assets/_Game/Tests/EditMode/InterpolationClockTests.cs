using Blastlands.Core.Net;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class InterpolationClockTests
    {
        private const int Unit = InterpolationClock.UnitsPerTick;
        private const int Delay = 3;

        private static InterpolationClock Clock()
        {
            return new InterpolationClock(Delay, 15);
        }

        [Test]
        public void TheFirstSnapshotStartsTheClockTheDelayBehindIt()
        {
            InterpolationClock clock = Clock();
            clock.Heard(100);

            Assert.That(clock.RenderTime, Is.EqualTo((100 - Delay) * (long)Unit));
            Assert.That(clock.ServerTick, Is.EqualTo(100));
        }

        [Test]
        public void SnapshotsArrivingOnTimeKeepItExactlyTheDelayBehind()
        {
            InterpolationClock clock = Clock();
            for (int tick = 0; tick < 300; tick++)
            {
                clock.Heard(tick);
                clock.Advance(Unit);
                Assert.That(clock.RenderTime, Is.EqualTo((tick + 1 - Delay) * (long)Unit), $"tick {tick}");
            }
        }

        [Test]
        public void JitteredArrivalsNeverTurnIntoAJumpOrAStepBackwards()
        {
            InterpolationClock clock = Clock();
            int[] arrivalsPerFrame = { 1, 0, 2, 0, 0, 0, 2, 1, 0, 0, 0, 0 };
            int next = 0;
            int frameUnits = Unit / 2;
            long before = 0;

            for (int frame = 0; frame < 2400; frame++)
            {
                int burst = arrivalsPerFrame[frame % arrivalsPerFrame.Length];
                for (int i = 0; i < burst; i++)
                {
                    clock.Heard(next++);
                }

                if (!clock.Started)
                {
                    continue;
                }

                before = clock.RenderTime;
                clock.Advance(frameUnits);
                long step = clock.RenderTime - before;

                if (frame > 100)
                {
                    Assert.That(step, Is.GreaterThanOrEqualTo(0), $"went backwards at frame {frame}");
                    Assert.That(step, Is.LessThanOrEqualTo(frameUnits + (frameUnits / 10)), $"jumped at frame {frame}");
                    Assert.That(clock.RenderTime, Is.LessThanOrEqualTo(clock.NewestTick * (long)Unit), $"ran past the buffer at frame {frame}");
                }
            }
        }

        [Test]
        public void ALossBurstRunsItUpToTheNewestSnapshotAndNoFurther()
        {
            InterpolationClock clock = Clock();
            clock.Heard(50);

            for (int frame = 0; frame < 20; frame++)
            {
                clock.Advance(Unit);
            }

            Assert.That(clock.RenderTime, Is.EqualTo(50 * (long)Unit));
            Assert.That(clock.ServerTick, Is.EqualTo(50 + Delay));
        }

        [Test]
        public void ALongStallIsCaughtUpInOneStepRatherThanFastForwarded()
        {
            InterpolationClock clock = Clock();
            clock.Heard(10);
            clock.Heard(200);
            clock.Advance(Unit / 30);

            Assert.That(clock.RenderTime, Is.EqualTo((200 - Delay) * (long)Unit));
        }

        [Test]
        public void ASmallLagIsWorkedOffGraduallyInsteadOfSnapped()
        {
            InterpolationClock clock = Clock();
            clock.Heard(10);
            clock.Heard(14);
            long before = clock.RenderTime;
            clock.Advance(Unit);

            Assert.That(clock.RenderTime - before, Is.EqualTo(Unit + (Unit / 10)));
        }

        [Test]
        public void TicksPastIsHowFarTheServerHasMovedSinceAState()
        {
            InterpolationClock clock = Clock();
            for (int tick = 40; tick <= 42; tick++)
            {
                clock.Heard(tick);
                clock.Advance(Unit);
            }

            Assert.That(clock.TicksPast(40), Is.EqualTo(3));
            Assert.That(clock.TicksPast(43), Is.EqualTo(0));
            Assert.That(clock.TicksPast(50), Is.EqualTo(0));
        }
    }
}
