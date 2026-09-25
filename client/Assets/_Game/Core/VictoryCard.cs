using System.Text;

namespace Blastlands.Core
{
    public enum VictoryTone : byte
    {
        Won,
        Lost,
        Drawn
    }

    // What the end of a match says, worked out here rather than in the screen that
    // draws it, so the wording for every ending is tested without a scene.
    public readonly struct VictoryCard
    {
        public const int NoWinner = -1;

        private VictoryCard(VictoryTone tone, string heading, string detail, int winner, CharacterKind character)
        {
            Tone = tone;
            Heading = heading;
            Detail = detail;
            Winner = winner;
            Character = character;
        }

        public VictoryTone Tone { get; }

        public string Heading { get; }

        public string Detail { get; }

        public int Winner { get; }

        public CharacterKind Character { get; }

        public bool HasWinner
        {
            get { return Winner != NoWinner; }
        }

        // An online match is one round, seen from one seat.
        public static VictoryCard ForRound(MatchState state, int localSeat)
        {
            if (state.Settings.Survival.Enabled)
            {
                return ForRun(state);
            }

            PlayerState winner = WinnerOf(state);
            if (winner == null)
            {
                return new VictoryCard(VictoryTone.Drawn, "DRAW", "Nobody was left standing.", NoWinner, CharacterKind.None);
            }

            if (winner.Id == localSeat)
            {
                string how = state.WonByHoldingOut ? "The ring took everyone, and you held out nearest the middle." : "You are the last one standing.";
                return new VictoryCard(VictoryTone.Won, "VICTORY", how, winner.Id, winner.Character);
            }

            string won = state.WonByHoldingOut ? " wins, holding out nearest the middle." : " wins.";
            return new VictoryCard(VictoryTone.Lost, "DEFEAT", Label(winner) + won, winner.Id, winner.Character);
        }

        // A local series, seen from the couch: whoever took it, and how it went.
        public static VictoryCard ForSeries(MatchSeries series, MatchState state)
        {
            if (!series.Decided || series.Champion >= state.Players.Count)
            {
                return new VictoryCard(VictoryTone.Drawn, "DRAW", "Nobody took the series.", NoWinner, CharacterKind.None);
            }

            PlayerState champion = state.Players[series.Champion];
            string detail = Label(champion) + " takes the series, " + Score(series) + ".";
            VictoryTone tone = champion.IsBot ? VictoryTone.Lost : VictoryTone.Won;
            string heading = champion.IsBot ? "DEFEAT" : "VICTORY";

            return new VictoryCard(tone, heading, detail, champion.Id, champion.Character);
        }

        public static VictoryCard ForRun(MatchState state)
        {
            int waves = state.Settings.Survival.Waves;
            string slain = state.ZombiesSlain == 1 ? "1 zombie down" : state.ZombiesSlain + " zombies down";

            if (state.Outcome == RoundOutcome.Survived)
            {
                return new VictoryCard(
                    VictoryTone.Won, "SURVIVED", "All " + waves + " waves held off, " + slain + ".", NoWinner, CharacterKind.None);
            }

            return new VictoryCard(
                VictoryTone.Lost, "OVERRUN", "The horde got through on wave " + state.Wave + " of " + waves + ", " + slain + ".",
                NoWinner, CharacterKind.None);
        }

        public static string Label(PlayerState player)
        {
            return (player.IsBot ? "BOT " : "PLAYER ") + (player.Id + 1);
        }

        private static PlayerState WinnerOf(MatchState state)
        {
            if (state.Outcome != RoundOutcome.Winner)
            {
                return null;
            }

            foreach (PlayerState player in state.Players)
            {
                if (player.Id == state.WinnerId)
                {
                    return player;
                }
            }

            return null;
        }

        private static string Score(MatchSeries series)
        {
            var text = new StringBuilder();
            for (int i = 0; i < series.PlayerCount; i++)
            {
                if (i > 0)
                {
                    text.Append(" to ");
                }

                text.Append(series.Wins(i));
            }

            return text.ToString();
        }
    }
}
