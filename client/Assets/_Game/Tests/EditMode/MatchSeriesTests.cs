using System;
using System.Collections.Generic;
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
        public void TheSeedForTheNextRoundIsNeverZero()
        {
            // The one that matters. The driver reads a zero seed as "roll a random one",
            // so a single zero anywhere in the chain silently turns a reproducible series
            // back into an unreproducible one from that round on, and nothing about the
            // match looks wrong while it happens.
            for (uint seed = 1; seed <= 20000; seed++)
            {
                Assert.That(MatchSeries.NextSeed(seed), Is.Not.Zero, $"seed {seed} derived zero");
            }

            Assert.That(MatchSeries.NextSeed(0u), Is.Not.Zero, "zero has to derive something usable");
        }

        [Test]
        public void TheSameSeedAlwaysGivesTheSameNextRound()
        {
            // The whole point of deriving rather than rolling: one number describes a
            // whole series, so a bug report can be replayed past its first round.
            Assert.That(MatchSeries.NextSeed(12345u), Is.EqualTo(MatchSeries.NextSeed(12345u)));
            Assert.That(MatchSeries.NextSeed(1u), Is.EqualTo(MatchSeries.NextSeed(1u)));
        }

        [Test]
        public void ARoundNeverDealsTheArenaItJustPlayed()
        {
            // A seed mapping to itself would stick a series on one arena for ever, which
            // is the failure a lazy derivation like "add one and hope" would not have.
            for (uint seed = 1; seed <= 20000; seed++)
            {
                Assert.That(MatchSeries.NextSeed(seed), Is.Not.EqualTo(seed), $"seed {seed} maps to itself");
            }
        }

        [Test]
        public void ASeriesDoesNotLoopBackOnItselfWithinAMatch()
        {
            // Long enough to cover any series anybody would sit through. A short cycle
            // would quietly replay arenas in a fixed order.
            var seen = new HashSet<uint>();
            uint seed = 7u;
            seen.Add(seed);

            for (int round = 0; round < 50; round++)
            {
                seed = MatchSeries.NextSeed(seed);
                Assert.That(seen.Add(seed), Is.True, $"round {round} came back to a seed already played");
            }
        }

        [Test]
        public void TwoSeriesThatStartNextToEachOtherDoNotConverge()
        {
            // Adjacent seeds are what a person types. If they produced neighbouring
            // arenas, or met after a round or two, pinning a seed would say much less
            // than it appears to.
            uint a = 7u;
            uint b = 8u;

            for (int round = 0; round < 10; round++)
            {
                a = MatchSeries.NextSeed(a);
                b = MatchSeries.NextSeed(b);
                Assert.That(a, Is.Not.EqualTo(b), $"the two chains met at round {round}");
            }
        }

        [Test]
        public void TheNextRoundIsADifferentBoardAndNotTheSameOneShiftedAlong()
        {
            // Different seeds are not the point. Different arenas are, and the two are
            // not the same claim: deriving a seed by one draw of the same generator gave
            // every round the previous round's stream offset by a single step, so the
            // Classic cover came out one tile along and the tests that only compared
            // seeds all passed.
            //
            // Measured at 314 of 337 eligible tiles matching at an offset of one before
            // the fix, 196 after it, and 191 for two unrelated seeds. The bar is three
            // quarters, well clear of chance and well under what the bug produced.
            const uint Seed = 424242u;
            MatchState first = MatchFactory.Create(ArenaSettings.Classic, MatchSettings.Default, 2, Seed);
            MatchState second = MatchFactory.Create(
                ArenaSettings.Classic, MatchSettings.Default, 2, MatchSeries.NextSeed(Seed));

            var before = new List<bool>();
            var after = new List<bool>();

            for (int y = 0; y < first.Arena.Height; y++)
            {
                for (int x = 0; x < first.Arena.Width; x++)
                {
                    var tile = new GridPos(x, y);
                    if (first.Arena[tile] == TileKind.HardBlock || second.Arena[tile] == TileKind.HardBlock
                        || first.Arena[tile] == TileKind.Void || second.Arena[tile] == TileKind.Void)
                    {
                        continue;
                    }

                    before.Add(first.Arena[tile] == TileKind.SoftBlock);
                    after.Add(second.Arena[tile] == TileKind.SoftBlock);
                }
            }

            int shifted = 0;
            for (int i = 0; i + 1 < before.Count; i++)
            {
                if (after[i] == before[i + 1])
                {
                    shifted++;
                }
            }

            Assert.That(
                shifted,
                Is.LessThan((before.Count - 1) * 3 / 4),
                $"round two is round one shifted by a tile ({shifted} of {before.Count - 1})");
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
