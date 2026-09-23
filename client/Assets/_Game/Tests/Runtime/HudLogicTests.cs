using NUnit.Framework;
using UnityEngine;

namespace Blastlands.Runtime.Tests
{
    public class HudLogicTests
    {
        [TestCase(HudAction.Bomb, PromptGlyph.KeyE)]
        [TestCase(HudAction.Dash, PromptGlyph.KeySpace)]
        [TestCase(HudAction.Shove, PromptGlyph.MouseLeft)]
        [TestCase(HudAction.Ability, PromptGlyph.MouseRight)]
        public void TheKeyboardPromptsMatchTheKeyboardBindings(HudAction action, PromptGlyph expected)
        {
            Assert.That(ActionPrompts.For(InputDeviceKind.Keyboard, action), Is.EqualTo(expected));
        }

        [TestCase(InputDeviceKind.Xbox)]
        [TestCase(InputDeviceKind.PlayStation)]
        public void EveryPadShowsThePadBindings(InputDeviceKind pad)
        {
            Assert.That(ActionPrompts.For(pad, HudAction.Bomb), Is.EqualTo(PromptGlyph.PadSouth));
            Assert.That(ActionPrompts.For(pad, HudAction.Dash), Is.EqualTo(PromptGlyph.PadShoulder));
            Assert.That(ActionPrompts.For(pad, HudAction.Shove), Is.EqualTo(PromptGlyph.PadWest));
            Assert.That(ActionPrompts.For(pad, HudAction.Ability), Is.EqualTo(PromptGlyph.PadNorth));
        }

        [Test]
        public void OnlyKeyboardGlyphsAreDrawnAsKeys()
        {
            foreach (HudAction action in new[] { HudAction.Bomb, HudAction.Dash, HudAction.Shove, HudAction.Ability })
            {
                Assert.That(ActionPrompts.OnKeyboard(ActionPrompts.For(InputDeviceKind.Keyboard, action)), Is.True);
                Assert.That(ActionPrompts.OnKeyboard(ActionPrompts.For(InputDeviceKind.Xbox, action)), Is.False);
            }
        }

        [TestCase(3060, "1:42")]
        [TestCase(61, "0:03")]
        [TestCase(30, "0:01")]
        [TestCase(0, "0:00")]
        [TestCase(-45, "0:00")]
        public void TheClockRoundsTheLastSecondUp(int ticksLeft, string expected)
        {
            Assert.That(HudText.Clock(ticksLeft, 30), Is.EqualTo(expected));
        }

        [TestCase(0, 90, 1f)]
        [TestCase(90, 90, 0f)]
        [TestCase(45, 90, 0.5f)]
        [TestCase(120, 90, 0f)]
        [TestCase(10, 0, 1f)]
        public void ACooldownFillsAsItRecovers(int remaining, int total, float expected)
        {
            Assert.That(HudActions.Recovered(remaining, total), Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void AnOffsetPointsAwayFromTheCornerItIsMeasuredFrom()
        {
            var offset = new Vector2(24f, 10f);

            Assert.That(HudPlacement.Inward(new Vector2(0f, 0f), offset), Is.EqualTo(new Vector2(24f, 10f)));
            Assert.That(HudPlacement.Inward(new Vector2(1f, 0f), offset), Is.EqualTo(new Vector2(-24f, 10f)));
            Assert.That(HudPlacement.Inward(new Vector2(0f, 1f), offset), Is.EqualTo(new Vector2(24f, -10f)));
            Assert.That(HudPlacement.Inward(new Vector2(1f, 1f), offset), Is.EqualTo(new Vector2(-24f, -10f)));
            Assert.That(HudPlacement.Inward(new Vector2(0.5f, 1f), offset), Is.EqualTo(new Vector2(24f, -10f)));
        }
        [Test]
        public void ABlockGivesWayOnlyWhenAPlayerIsBehindIt()
        {
            var block = new Rect(0f, 800f, 500f, 280f);

            Assert.That(HudFade.Covers(block, new[] { new Vector2(900f, 500f) }, 24f), Is.False);
            Assert.That(HudFade.Covers(block, new[] { new Vector2(900f, 500f), new Vector2(120f, 900f) }, 24f), Is.True);
            Assert.That(HudFade.Covers(block, new[] { new Vector2(510f, 900f) }, 24f), Is.True, "the padding should catch a player at the edge");
            Assert.That(HudFade.Covers(block, new Vector2[0], 24f), Is.False);
        }

        [Test]
        public void ABlockWithNothingDrawnNeverGivesWay()
        {
            Assert.That(HudFade.Covers(Rect.zero, new[] { Vector2.zero }, 24f), Is.False);
        }
    }
}
