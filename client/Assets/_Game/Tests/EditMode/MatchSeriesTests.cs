using System;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    // The score across rounds. The failures worth guarding are the ones that lose a
    // point or never end: a draw that vanishes, a win credited to nobody, or a series
    // that keeps dealing rounds after somebody has already taken it.
    public class MatchSeriesTests
    {
        private static MatchSeries BestOfThree()
        {
            return new MatchSeries(4, 2);
        }

        [Test]
        public void AFreshSeriesIsLevelAndUndecided()
        {
            MatchSeries series = BestOfThree();

            Assert.That(series.RoundsPlayed, Is.Zero);
            Assert.That(series.Decided, Is.False);
            Assert.That(series.Champion, Is.EqualTo(MatchSeries.NoChampion));
            Assert.That(series.Leader, Is.EqualTo(MatchSeries.NoChampion), "nobody leads at nil all");

            for (int player = 0; player < series.PlayerCount; player++)
            {
                Assert.That(series.Wins(player), Is.Zero);
            }
        }

        [Test]
        public void AWinIsCreditedToTheWinnerAndNobodyElse()
        {
            MatchSeries series = BestOfThree();

            series.Record(RoundOutcome.Winner, 2);

            Assert.That(series.Wins(2), Is.EqualTo(1));
            Assert.That(series.Wins(0), Is.Zero);
            Assert.That(series.Wins(3), Is.Zero);
            Assert.That(series.RoundsPlayed, Is.EqualTo(1));
        }

        [Test]
        public void ADrawCountsAsARoundThatNobodyWon()
        {
            // The one that decides whether a series can end. Simultaneous death is
            // common here, and a draw that did not count as played would leave a match
            // dealing rounds forever on a board where everyone keeps trading kills.
            MatchSeries series = BestOfThree();

            series.Record(RoundOutcome.Draw, MatchSeries.NoChampion);

            Assert.That(series.RoundsPlayed, Is.EqualTo(1));
            Assert.That(series.Decided, Is.False);

            for (int player = 0; player < series.PlayerCount; player++)
            {
                Assert.That(series.Wins(player), Is.Zero, "a draw scored for somebody");
            }
        }

        [Test]
        public void ReachingTheTargetDecidesIt()
        {
            MatchSeries series = BestOfThree();

            series.Record(RoundOutcome.Winner, 1);
            Assert.That(series.Decided, Is.False, "one win of two ended it");

            series.Record(RoundOutcome.Winner, 1);

            Assert.That(series.Decided, Is.True);
            Assert.That(series.Champion, Is.EqualTo(1));
        }

        [Test]
        public void ADecidedSeriesStopsCounting()
        {
            // The driver stops dealing rounds when this says so, but a late result can
            // still arrive from the round that was already in flight. It must not
            // rewrite the champion.
            MatchSeries series = BestOfThree();
            series.Record(RoundOutcome.Winner, 1);
            series.Record(RoundOutcome.Winner, 1);

            series.Record(RoundOutcome.Winner, 3);

            Assert.That(series.Champion, Is.EqualTo(1));
            Assert.That(series.Wins(3), Is.Zero);
            Assert.That(series.RoundsPlayed, Is.EqualTo(2));
        }

        [Test]
        public void TheChampionIsWhoeverGotThereFirst()
        {
            MatchSeries series = new MatchSeries(2, 2);

            series.Record(RoundOutcome.Winner, 0);
            series.Record(RoundOutcome.Winner, 1);
            series.Record(RoundOutcome.Winner, 1);

            Assert.That(series.Champion, Is.EqualTo(1));
        }

        [Test]
        public void LevelAtTheTopIsNotALead()
        {
            MatchSeries series = new MatchSeries(4, 3);

            series.Record(RoundOutcome.Winner, 0);
            Assert.That(series.Leader, Is.EqualTo(0));

            series.Record(RoundOutcome.Winner, 2);
            Assert.That(series.Leader, Is.EqualTo(MatchSeries.NoChampion), "one all is not a lead");

            series.Record(RoundOutcome.Winner, 2);
            Assert.That(series.Leader, Is.EqualTo(2));
        }

        [Test]
        public void AWinnerNobodyRecognisesScoresForNobody()
        {
            // A winner id out of range means the round result and the series disagree
            // about who is playing. Growing the array or throwing would both be worse
            // than dropping the point: the round still happened.
            MatchSeries series = BestOfThree();

            series.Record(RoundOutcome.Winner, 9);
            series.Record(RoundOutcome.Winner, -1);

            Assert.That(series.RoundsPlayed, Is.EqualTo(2));
            Assert.That(series.Decided, Is.False);
        }

        [Test]
        public void ARoundStillBeingPlayedHasNoResultToRecord()
        {
            MatchSeries series = BestOfThree();

            TestDelegate stillRunning = () => series.Record(RoundOutcome.Running, 0);

            Assert.Throws<ArgumentOutOfRangeException>(stillRunning);
        }

        [Test]
        public void ASeriesNobodyCanWinIsRefused()
        {
            TestDelegate noRounds = () => new MatchSeries(4, 0);
            TestDelegate noPlayers = () => new MatchSeries(0, 2);

            Assert.Throws<ArgumentOutOfRangeException>(noRounds);
            Assert.Throws<ArgumentOutOfRangeException>(noPlayers);
        }

        [Test]
        public void AskingAboutSomebodyWhoIsNotPlayingIsNotACrash()
        {
            // The HUD walks player slots, and a series built for two in a four-slot HUD
            // would otherwise take the whole panel down with it.
            MatchSeries series = new MatchSeries(2, 2);

            Assert.That(series.Wins(7), Is.Zero);
            Assert.That(series.Wins(-1), Is.Zero);
        }
    }
}
