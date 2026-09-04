using Blastlands.Runtime;
using NUnit.Framework;

namespace Blastlands.Runtime.Tests
{
    // When a bomb is close enough to going off to warn about. The point of the cue is a
    // blast you cannot see yet, so the failures that matter are the ones that make it
    // useless: warning too late to move, or warning so often it stops meaning anything.
    public class BombFuseTests
    {
        private const int Ticks = 30;
        private const int NormalFuse = 90;

        [Test]
        public void ABombJustDroppedIsNotWarnedAbout()
        {
            Assert.That(BombFuse.IsWarning(NormalFuse, NormalFuse, Ticks), Is.False);
        }

        [Test]
        public void TheWarningLandsWithTimeLeftToMove()
        {
            // A second of notice, at the tick it becomes a second. Warning at the tick
            // the bomb goes off would be an explosion with a click in front of it.
            Assert.That(BombFuse.IsWarning(Ticks + 1, NormalFuse, Ticks), Is.False);
            Assert.That(BombFuse.IsWarning(Ticks, NormalFuse, Ticks), Is.True);
            Assert.That(BombFuse.IsWarning(1, NormalFuse, Ticks), Is.True);
        }

        [Test]
        public void AFuseShorterThanTheWarningIsNotWarnedAboutAtAll()
        {
            // The one that would sound broken. A short fuse would be inside its warning
            // window the instant it lands, so the drop and the warning would play a tick
            // apart and read as one stuttering sound rather than as two events.
            Assert.That(BombFuse.IsWarning(20, 20, Ticks), Is.False);
            Assert.That(BombFuse.IsWarning(5, 20, Ticks), Is.False);
        }

        [Test]
        public void AFuseExactlyAsLongAsTheWarningIsAlsoRefused()
        {
            // The boundary of the case above: warning at the moment of the drop is the
            // same stutter, so the whole fuse has to be longer than the notice.
            Assert.That(BombFuse.IsWarning(Ticks, Ticks, Ticks), Is.False);
            Assert.That(BombFuse.IsWarning(Ticks, Ticks + 1, Ticks), Is.True);
        }

        [Test]
        public void ABombThatHasAlreadyGoneOffIsNotWarnedAbout()
        {
            Assert.That(BombFuse.IsWarning(0, NormalFuse, Ticks), Is.False);
            Assert.That(BombFuse.IsWarning(-1, NormalFuse, Ticks), Is.False);
        }

        [Test]
        public void AMatchWithoutATickRateWarnsAboutNothing()
        {
            // Rather than dividing by it or comparing against zero, which would make
            // every bomb permanently inside its warning window.
            Assert.That(BombFuse.IsWarning(1, NormalFuse, 0), Is.False);
        }

        [Test]
        public void TheNoticeIsTheSameLengthWhateverTheTickRate()
        {
            // The warning is a second of real time. Expressed in ticks it has to follow
            // the tick rate, or the same bomb would warn twice as early on a server
            // running twice as fast.
            Assert.That(BombFuse.WarningTicks(60), Is.EqualTo(60));
            Assert.That(BombFuse.IsWarning(45, 180, 60), Is.True);
            Assert.That(BombFuse.IsWarning(45, 180, 30), Is.False);
        }
    }
}
