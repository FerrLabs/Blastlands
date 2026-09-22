using NUnit.Framework;
using UnityEngine;

namespace Blastlands.Runtime.Tests
{
    public class HudLogicTests
    {
        [TestCase(HudAction.Bomb, PromptGlyph.KeyE)]
        [TestCase(HudAction.Dash, PromptGlyph.KeySpace)]
        [TestCase(HudAction.Shove, PromptGlyph.MouseLeft)]
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
        }

        [Test]
        public void OnlyKeyboardGlyphsAreDrawnAsKeys()
        {
            foreach (HudAction action in new[] { HudAction.Bomb, HudAction.Dash, HudAction.Shove })
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
    }
}
