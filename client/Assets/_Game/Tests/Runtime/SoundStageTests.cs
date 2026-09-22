using Blastlands.Runtime;
using NUnit.Framework;

namespace Blastlands.Runtime.Tests
{
    public class SoundStageTests
    {
        private const float HalfWidth = 7f;

        [Test]
        public void AnythingOnScreenIsHeardInFull()
        {
            Assert.That(SoundStage.LoudnessAt(0f, HalfWidth), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(SoundStage.LoudnessAt(HalfWidth - 0.5f, HalfWidth), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(SoundStage.LoudnessAt(HalfWidth, HalfWidth), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void JustOffScreenStaysLoudEnoughToReactTo()
        {
            float adjacent = SoundStage.LoudnessAt(HalfWidth + 1f, HalfWidth);

            Assert.That(adjacent, Is.LessThan(1f), "it has to be tellable from one on screen");
            Assert.That(adjacent, Is.GreaterThan(0.7f), "but it is the cue the player acts on");
        }

        [Test]
        public void FurtherIsQuieter()
        {
            float near = SoundStage.LoudnessAt(HalfWidth + 1f, HalfWidth);
            float middle = SoundStage.LoudnessAt(HalfWidth + 4f, HalfWidth);
            float far = SoundStage.LoudnessAt(HalfWidth + 7f, HalfWidth);

            Assert.That(near, Is.GreaterThan(middle));
            Assert.That(middle, Is.GreaterThan(far));
        }

        [Test]
        public void AcrossTheBoardIsFaintRatherThanGone()
        {
            Assert.That(
                SoundStage.LoudnessAt(HalfWidth + SoundStage.FadeTiles, HalfWidth),
                Is.EqualTo(SoundStage.FarShare).Within(0.0001f));

            Assert.That(
                SoundStage.LoudnessAt(200f, HalfWidth),
                Is.EqualTo(SoundStage.FarShare).Within(0.0001f));
        }

        [Test]
        public void ASideIsPickedAndKept()
        {
            Assert.That(SoundStage.PanOf(0f, HalfWidth), Is.EqualTo(0f));
            Assert.That(SoundStage.PanOf(-4f, HalfWidth), Is.LessThan(0f));
            Assert.That(SoundStage.PanOf(4f, HalfWidth), Is.GreaterThan(0f));
            Assert.That(
                SoundStage.PanOf(-4f, HalfWidth),
                Is.EqualTo(-SoundStage.PanOf(4f, HalfWidth)).Within(0.0001f));
        }

        [Test]
        public void WhatYouCanSeeStaysNearerTheMiddleThanWhatYouCannot()
        {
            float onScreen = SoundStage.PanOf(HalfWidth, HalfWidth);
            float offScreen = SoundStage.PanOf(HalfWidth * 2f, HalfWidth);

            Assert.That(onScreen, Is.GreaterThan(0f));
            Assert.That(onScreen, Is.LessThan(offScreen));
            Assert.That(offScreen, Is.EqualTo(SoundStage.HardestPan).Within(0.0001f));
        }

        [Test]
        public void NoCueIsEverEmptiedOutOfOneSpeaker()
        {
            Assert.That(SoundStage.HardestPan, Is.LessThan(1f));
            Assert.That(SoundStage.PanOf(500f, HalfWidth), Is.EqualTo(SoundStage.HardestPan).Within(0.0001f));
            Assert.That(SoundStage.PanOf(-500f, HalfWidth), Is.EqualTo(-SoundStage.HardestPan).Within(0.0001f));
        }

        [Test]
        public void SplitScreenFallsBackToAFlatMixRatherThanAWrongOne()
        {
            Assert.That(SoundStage.PanOf(6f, 0f), Is.EqualTo(0f));
            Assert.That(SoundStage.LoudnessAt(0f, 0f), Is.EqualTo(1f).Within(0.0001f));
        }
    }
}
