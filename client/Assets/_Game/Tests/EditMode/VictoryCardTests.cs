using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class VictoryCardTests
    {
        [Test]
        public void AWinOnTheTieBreakSaysHowItWasWon()
        {
            var state = new MatchState(new Arena(15, 15), MatchSettings.Default, 1u);
            state.AddPlayer(new GridPos(0, 6));
            state.AddPlayer(new GridPos(0, 3));
            state.Players[0].Alive = false;
            state.Players[1].Alive = false;
            state.Outcome = RoundOutcome.Winner;
            state.WinnerId = 0;

            Assert.That(VictoryCard.ForRound(state, 0).Detail, Does.Contain("nearest the middle"));
            Assert.That(VictoryCard.ForRound(state, 1).Detail, Does.Contain("nearest the middle"));
            Assert.That(VictoryCard.ForRound(state, 1).Heading, Is.EqualTo("DEFEAT"));
        }

        private static MatchState Ended(RoundOutcome outcome, int winner)
        {
            MatchState state = MatchFactory.Create(
                ArenaSettings.Default, MatchSettings.Default, 4, 7u, CharacterKits.ForSeat);
            state.Players[2].IsBot = true;
            state.Outcome = outcome;
            state.WinnerId = winner;
            return state;
        }

        [Test]
        public void TheLastOneStandingIsToldTheyWon()
        {
            VictoryCard card = VictoryCard.ForRound(Ended(RoundOutcome.Winner, 1), 1);

            Assert.That(card.Tone, Is.EqualTo(VictoryTone.Won));
            Assert.That(card.Heading, Is.EqualTo("VICTORY"));
            Assert.That(card.Winner, Is.EqualTo(1));
            Assert.That(card.Character, Is.EqualTo(CharacterKits.ForSeat(1)), "the winner is shown as who they played");
        }

        [Test]
        public void EveryoneElseIsToldWhoBeatThem()
        {
            VictoryCard card = VictoryCard.ForRound(Ended(RoundOutcome.Winner, 2), 0);

            Assert.That(card.Tone, Is.EqualTo(VictoryTone.Lost));
            Assert.That(card.Detail, Is.EqualTo("BOT 3 wins."));
            Assert.That(card.Winner, Is.EqualTo(2));
        }

        [Test]
        public void ADrawHasNobodyToShow()
        {
            VictoryCard card = VictoryCard.ForRound(Ended(RoundOutcome.Draw, -1), 0);

            Assert.That(card.Tone, Is.EqualTo(VictoryTone.Drawn));
            Assert.That(card.HasWinner, Is.False);
        }

        [Test]
        public void AWinnerNotOnTheBoardIsReadAsADraw()
        {
            Assert.That(VictoryCard.ForRound(Ended(RoundOutcome.Winner, 9), 0).HasWinner, Is.False);
        }

        [Test]
        public void TheSeriesCardNamesTheChampionAndTheScore()
        {
            MatchState state = Ended(RoundOutcome.Winner, 0);
            var series = new MatchSeries(4, 2);
            series.Record(RoundOutcome.Winner, 0);
            series.Record(RoundOutcome.Winner, 3);
            series.Record(RoundOutcome.Winner, 0);

            VictoryCard card = VictoryCard.ForSeries(series, state);

            Assert.That(card.Tone, Is.EqualTo(VictoryTone.Won));
            Assert.That(card.Detail, Is.EqualTo("PLAYER 1 takes the series, 2 to 0 to 0 to 1."));
            Assert.That(card.Winner, Is.EqualTo(0));
        }

        [Test]
        public void ASeriesABotTookIsALoss()
        {
            MatchState state = Ended(RoundOutcome.Winner, 2);
            var series = new MatchSeries(4, 1);
            series.Record(RoundOutcome.Winner, 2);

            Assert.That(VictoryCard.ForSeries(series, state).Tone, Is.EqualTo(VictoryTone.Lost));
        }

        [Test]
        public void ASeriesStillBeingPlayedHasNoChampionToShow()
        {
            var series = new MatchSeries(4, 3);
            series.Record(RoundOutcome.Winner, 1);

            Assert.That(VictoryCard.ForSeries(series, Ended(RoundOutcome.Winner, 1)).HasWinner, Is.False);
        }

        [Test]
        public void ARunSaysHowFarItGotRatherThanWhoWon()
        {
            var state = new MatchState(new Arena(9, 9), MatchSettings.SurvivalMode, 1u);
            state.AddPlayer(new GridPos(1, 1));
            state.Wave = 3;
            state.ZombiesSlain = 11;
            state.Outcome = RoundOutcome.Overrun;

            VictoryCard lost = VictoryCard.ForRound(state, 0);
            Assert.That(lost.Tone, Is.EqualTo(VictoryTone.Lost));
            Assert.That(lost.Heading, Is.EqualTo("OVERRUN"));
            Assert.That(lost.Detail, Does.Contain("wave 3 of 5").And.Contain("11 zombies"));
            Assert.That(lost.HasWinner, Is.False);

            state.Outcome = RoundOutcome.Survived;
            Assert.That(VictoryCard.ForRun(state).Tone, Is.EqualTo(VictoryTone.Won));
        }
    }
}
