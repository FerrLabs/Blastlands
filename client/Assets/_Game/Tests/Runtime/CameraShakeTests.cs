using Blastlands.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Blastlands.Runtime.Tests
{
    // How hard a blast is felt, and for how long. The camera is how a player reads the
    // board, so the failure worth guarding against is not "no shake", it is a shake that
    // never stops or that throws the view far enough to lose track of where you are.
    public class CameraShakeTests
    {
        private const float Reach = CameraShake.ReachTiles;
        private const int BigBlast = 12;
        private const int SmallBlast = 3;
        private const float MaxOffset = 0.32f;

        [Test]
        public void ABlastYouCouldNotHaveSeenIsNotFelt()
        {
            Assert.That(CameraShake.StrengthOf(Reach, BigBlast, Reach), Is.EqualTo(0f));
            Assert.That(CameraShake.StrengthOf(Reach + 5f, BigBlast, Reach), Is.EqualTo(0f));
        }

        [Test]
        public void CloserIsHarder()
        {
            float near = CameraShake.StrengthOf(1f, BigBlast, Reach);
            float middle = CameraShake.StrengthOf(4f, BigBlast, Reach);
            float far = CameraShake.StrengthOf(8f, BigBlast, Reach);

            Assert.That(near, Is.GreaterThan(middle));
            Assert.That(middle, Is.GreaterThan(far));
            Assert.That(far, Is.GreaterThan(0f), "a blast still inside the view registers");
        }

        [Test]
        public void BiggerIsHarderAtTheSameDistance()
        {
            Assert.That(
                CameraShake.StrengthOf(2f, BigBlast, Reach),
                Is.GreaterThan(CameraShake.StrengthOf(2f, SmallBlast, Reach)));
        }

        [Test]
        public void ASmallBombIsStillFeltRatherThanIgnored()
        {
            // Zero for a small blast would read as the shake being broken rather than as
            // the bomb being small.
            Assert.That(CameraShake.StrengthOf(0f, 1, Reach), Is.GreaterThan(0f));
        }

        [Test]
        public void NothingHappenedIsNotAShake()
        {
            Assert.That(CameraShake.StrengthOf(0f, 0, Reach), Is.EqualTo(0f));
        }

        [Test]
        public void TwoBlastsAtOnceAreOneEventAndNotTwo()
        {
            // Summing them would throw the camera twice as far for something a player
            // sees as a single explosion, which reads as a bug rather than as force.
            var shake = new CameraShake(0f);

            shake.Felt(0.5f);
            shake.Felt(0.4f);

            Assert.That(shake.Strength, Is.EqualTo(0.5f));
        }

        [Test]
        public void ABiggerBlastDuringAShakeTakesOver()
        {
            var shake = new CameraShake(0f);

            shake.Felt(0.3f);
            shake.Felt(0.9f);

            Assert.That(shake.Strength, Is.EqualTo(0.9f));
        }

        [Test]
        public void AShakeStops()
        {
            // The one that matters. A shake that decays to nearly nothing and stays there
            // is a camera that never settles, and every frame after the explosion is
            // slightly wrong forever.
            var shake = new CameraShake(0f);
            shake.Felt(1f);

            for (int frame = 0; frame < 120; frame++)
            {
                shake.Advance(1f / 60f, MaxOffset);
            }

            Assert.That(shake.Strength, Is.EqualTo(0f));
            Assert.That(shake.Advance(1f / 60f, MaxOffset), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void TheViewIsNeverThrownFurtherThanItWasAllowed()
        {
            // Losing track of the board is worse than feeling nothing, so the offset is
            // bounded whatever the blast.
            var shake = new CameraShake(0.4f);
            shake.Felt(1f);

            for (int frame = 0; frame < 30; frame++)
            {
                Vector2 offset = shake.Advance(1f / 60f, MaxOffset);

                Assert.That(Mathf.Abs(offset.x), Is.LessThanOrEqualTo(MaxOffset));
                Assert.That(Mathf.Abs(offset.y), Is.LessThanOrEqualTo(MaxOffset));
            }
        }

        [Test]
        public void TwoViewportsDoNotShakeInLockstep()
        {
            // Split-screen. The same bomb reaching two people should read as two people
            // feeling it, not as the whole window sliding.
            var first = new CameraShake(0f);
            var second = new CameraShake(0.41f);

            first.Felt(1f);
            second.Felt(1f);

            Vector2 a = first.Advance(1f / 60f, MaxOffset);
            Vector2 b = second.Advance(1f / 60f, MaxOffset);

            Assert.That(a, Is.Not.EqualTo(b));
        }
    }
}
