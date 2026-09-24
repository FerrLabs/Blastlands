using NUnit.Framework;
using UnityEngine;

namespace Blastlands.Runtime.Tests
{
    public class TickBlendTests
    {
        [Test]
        public void BetweenTwoTicksThePlayerIsDrawnPartWayRatherThanAtEitherEnd()
        {
            var blend = new TickBlend();
            blend.Show(Vector3.zero, false, 0f);

            Vector3 half = blend.Show(new Vector3(1f, 0f, 0f), true, 0.5f);

            Assert.That(half.x, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void ANewTickStartsFromWhereThePlayerIsDrawnSoNothingJumpsBack()
        {
            var blend = new TickBlend();
            blend.Show(Vector3.zero, false, 0f);
            blend.Show(new Vector3(1f, 0f, 0f), true, 0.5f);

            Vector3 next = blend.Show(new Vector3(2f, 0f, 0f), true, 0f);

            Assert.That(next.x, Is.EqualTo(0.5f).Within(0.0001f), "the next tick picks up from the drawn position");
        }

        [Test]
        public void StandingStillStaysPut()
        {
            var blend = new TickBlend();
            blend.Show(Vector3.one, false, 0f);

            for (int frame = 0; frame < 5; frame++)
            {
                Assert.That(blend.Show(Vector3.one, frame % 2 == 0, frame * 0.2f), Is.EqualTo(Vector3.one));
            }
        }

        [Test]
        public void AJumpTooFarToBeAStepIsSnappedNotSlidAcrossTheBoard()
        {
            var blend = new TickBlend();
            blend.Show(Vector3.zero, false, 0f);

            Assert.That(blend.Show(new Vector3(9f, 0f, 0f), true, 0.1f).x, Is.EqualTo(9f));
        }
    }
}
