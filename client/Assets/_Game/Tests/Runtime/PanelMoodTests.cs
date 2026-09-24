using Blastlands.Core;
using Blastlands.Runtime;
using NUnit.Framework;

namespace Blastlands.Runtime.Tests
{
    // How brightly a player's corner is shown. Nothing here fails loudly: a wrong branch
    // just tells the player something untrue about who is alive or who took the round,
    // which is worse than a panel that does not draw at all.
    public class PanelMoodTests
    {
        [Test]
        public void WhileTheRoundRunsBrightnessIsWhetherYouAreAlive()
        {
            Assert.That(
                PanelMood.AlphaFor(true, RoundOutcome.Running, false),
                Is.EqualTo(PanelMood.Full));

            Assert.That(
                PanelMood.AlphaFor(false, RoundOutcome.Running, false),
                Is.EqualTo(PanelMood.Dead));
        }

        [Test]
        public void ADeadPlayerIsStillLegible()
        {
            // They keep their corner and their numbers. What somebody was carrying when
            // they died is worth seeing, so this must not be zero.
            Assert.That(PanelMood.Dead, Is.GreaterThan(0f));
            Assert.That(PanelMood.Dead, Is.LessThan(PanelMood.Full));
        }

        [Test]
        public void WhenTheRoundIsWonTheWinnerIsTheOnlyBrightCorner()
        {
            Assert.That(
                PanelMood.AlphaFor(true, RoundOutcome.Winner, true),
                Is.EqualTo(PanelMood.Full));

            Assert.That(
                PanelMood.AlphaFor(false, RoundOutcome.Winner, false),
                Is.EqualTo(PanelMood.Beaten));
        }

        [Test]
        public void ALivingLoserIsDimmedTooRatherThanLeftBright()
        {
            // Sudden death can end a round with more than one player standing. Leaving
            // every survivor bright would make the summary say nothing about who won.
            Assert.That(
                PanelMood.AlphaFor(true, RoundOutcome.Winner, false),
                Is.EqualTo(PanelMood.Beaten));
        }

        [Test]
        public void ADrawDimsEverybody()
        {
            // Which is what a draw looks like: no corner is the answer. Mutual
            // destruction is common here, so this is not a rare path.
            Assert.That(
                PanelMood.AlphaFor(false, RoundOutcome.Draw, false),
                Is.EqualTo(PanelMood.Beaten));

            Assert.That(
                PanelMood.AlphaFor(true, RoundOutcome.Draw, false),
                Is.EqualTo(PanelMood.Beaten));
        }

        [Test]
        public void TheSummaryDimsHarderThanDyingDoes()
        {
            // The whole point of the round-over treatment. If a beaten player were shown
            // at the same brightness as a corpse mid-round, the end of the round would
            // look identical to the middle of it.
            Assert.That(PanelMood.Beaten, Is.LessThan(PanelMood.Dead));
        }

        [Test]
        public void ASurvivedRunKeepsEverySurvivorLit()
        {
            Assert.That(PanelMood.AlphaFor(true, RoundOutcome.Survived, false), Is.EqualTo(PanelMood.Full));
            Assert.That(PanelMood.AlphaFor(false, RoundOutcome.Survived, false), Is.EqualTo(PanelMood.Dead));
        }
    }
}
