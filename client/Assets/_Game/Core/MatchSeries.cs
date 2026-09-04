using System;

namespace Blastlands.Core
{
    // Who has won how many rounds, and whether that settles it.
    //
    // Separate from MatchState because a round is thrown away and rebuilt between
    // rounds, arena and all, while the score is the one thing that has to survive that.
    // Keeping it on the state would mean copying it across every rebuild, which is the
    // kind of bookkeeping that quietly loses a point.
    public sealed class MatchSeries
    {
        private readonly int[] wins;

        public MatchSeries(int playerCount, int roundsToWin)
        {
            if (playerCount < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(playerCount), playerCount, "a series needs at least one player");
            }

            if (roundsToWin < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(roundsToWin), roundsToWin, "a series has to be winnable");
            }

            wins = new int[playerCount];
            RoundsToWin = roundsToWin;
        }

        public int PlayerCount
        {
            get { return wins.Length; }
        }

        public int RoundsToWin { get; }

        public int RoundsPlayed { get; private set; }

        // -1 until somebody reaches the target, and it stays whoever got there first.
        public int Champion { get; private set; } = NoChampion;

        public const int NoChampion = -1;

        public bool Decided
        {
            get { return Champion != NoChampion; }
        }

        // The arena for the round after this one, derived from the one that just played
        // rather than rolled fresh. A series is then described by a single number: pin
        // the first round's seed and every round after it follows, which is what makes
        // a generator flaw or a bug report reproducible past round one.
        //
        // Lives here because the series is the thing that spans rounds, and one draw
        // from the project's own generator is a better answer than a hash written for
        // this. Zero is the one value that would break the chain, since the driver reads
        // it as "roll a random one", and xorshift cannot produce it: zero is a fixed
        // point of the shift, so a state that is not zero never becomes zero, and
        // DeterministicRandom substitutes a constant when handed zero to begin with.
        //
        // The xor is not decoration and must not be simplified away. Without it the
        // derived seed is the first number the previous round drew, so every round in a
        // series reads one xorshift stream a single draw further along: round N runs on
        // f(S), f2(S), f3(S) and round N+1 on f2(S), f3(S), the same numbers offset by
        // one. On the Classic board, where the lattice is fixed and the cover spends one
        // draw per eligible tile in a fixed order, that puts round N+1's cover one tile
        // along from round N's. Measured at 314 of 337 eligible tiles matching at an
        // offset of one, against 191 for two unrelated seeds. The xor lands the derived
        // seed a long way round the cycle instead of one step, and takes the match back
        // to 196, which is chance.
        //
        // Advancing by more draws would not have fixed it: a round consumes an unbounded
        // number of them, so any fixed offset can still fall inside the next round's
        // window.
        public static uint NextSeed(uint seed)
        {
            return new DeterministicRandom(seed ^ StreamShift).NextUInt();
        }

        // The golden-ratio constant, chosen for having its bits spread out rather than
        // for any property of its value. Only seed == StreamShift xors to zero, and
        // DeterministicRandom substitutes its own fallback for zero, so the derived seed
        // is still never zero.
        private const uint StreamShift = 0x9E3779B9u;

        public int Wins(int player)
        {
            return player >= 0 && player < wins.Length ? wins[player] : 0;
        }

        // A draw is recorded rather than ignored. Nobody scores, but the round happened,
        // and RoundsPlayed is what says so: with mutual destruction as common as it is
        // here, a series that skipped draws would report its fourth round as its second.
        //
        // It does not end anything. A series is over when somebody reaches the target and
        // at no other time, so a board where every single round is drawn deals forever
        // whether draws are counted or not. If that ever needs a stop, it wants a round
        // cap rather than a different rule for draws.
        public void Record(RoundOutcome outcome, int winnerId)
        {
            if (outcome == RoundOutcome.Running)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(outcome), outcome, "a round still being played has no result to record");
            }

            if (Decided)
            {
                return;
            }

            RoundsPlayed++;

            if (outcome != RoundOutcome.Winner || winnerId < 0 || winnerId >= wins.Length)
            {
                return;
            }

            wins[winnerId]++;

            if (wins[winnerId] >= RoundsToWin)
            {
                Champion = winnerId;
            }
        }

        // Who is ahead while it is still being played. NoChampion when it is level at the
        // top, because two players on two wins each is not a lead.
        public int Leader
        {
            get
            {
                int best = 0;
                int leader = NoChampion;
                bool tied = false;

                for (int i = 0; i < wins.Length; i++)
                {
                    if (wins[i] > best)
                    {
                        best = wins[i];
                        leader = i;
                        tied = false;
                    }
                    else if (wins[i] == best && best > 0)
                    {
                        tied = true;
                    }
                }

                return tied ? NoChampion : leader;
            }
        }
    }
}
