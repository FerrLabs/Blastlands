using System.Globalization;

namespace Blastlands.Core
{
    // One match as an instance runs it: the port it is served on, which match it is, how
    // many players to wait for, and where to report back.
    //
    // Built from the lobby's assignment rather than read at startup. A process hosts
    // several of these over its life, one per match per slot, so what it is told when it
    // starts is only the part that never changes (`HostOptions`), and the rest arrives
    // with each match.
    //
    // Everything the assignment names is checked, and nothing is guessed. A wrong match
    // id releases somebody else's match, and a match that cannot seat its players is
    // better refused with a reason than started into a crash.
    public readonly struct ServerOptions
    {
        public const string MatchField = "match_id";
        public const string PlayersField = "players";
        public const string HumansField = "humans";
        public const string ModeField = "mode";
        public const string BotsField = "bot_skill";

        public ServerOptions(
            int listenPort,
            string matchId,
            int expectedPlayers,
            int expectedHumans,
            string lobbyUrl,
            string instanceToken,
            GameMode mode,
            BotSkill botSkill)
        {
            Mode = mode;
            BotSkill = botSkill;
            ListenPort = listenPort;
            MatchId = matchId;
            ExpectedPlayers = expectedPlayers;
            ExpectedHumans = expectedHumans;
            LobbyUrl = lobbyUrl;
            InstanceToken = instanceToken;
        }

        public int ListenPort { get; }

        public string MatchId { get; }

        public int ExpectedPlayers { get; }

        public int ExpectedHumans { get; }

        public string LobbyUrl { get; }

        // What the lobby's internal endpoints check. An instance without it can heartbeat
        // and release into a 401 forever, which means its port is gone until somebody
        // restarts the lobby.
        public string InstanceToken { get; }

        // One of the two fields with a default. A lobby from before modes existed never
        // names one, and every match it could have handed out was an Arena one.
        public GameMode Mode { get; }

        public BotSkill BotSkill { get; }

        // The port, the lobby and the token belong to the host and were checked when it
        // started, so they are taken as they are. The rest is the assignment's text.
        public static bool TryCreate(
            int port,
            string lobbyUrl,
            string instanceToken,
            string rawMatch,
            string rawPlayers,
            string rawHumans,
            string rawMode,
            string rawBots,
            out ServerOptions options,
            out string error)
        {
            options = default;

            if (!TryMatchId(rawMatch, out string matchId, out error))
            {
                return false;
            }

            if (!TryPlayers(rawPlayers, out int players, out error))
            {
                return false;
            }

            if (!TryHumans(rawHumans, players, out int humans, out error))
            {
                return false;
            }

            if (!TryMode(rawMode, out GameMode mode, out error))
            {
                return false;
            }

            if (!TryBots(rawBots, out BotSkill bots, out error))
            {
                return false;
            }

            options = new ServerOptions(port, matchId, players, humans, lobbyUrl, instanceToken, mode, bots);
            error = null;
            return true;
        }

        private static bool TryMode(string raw, out GameMode mode, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(raw))
            {
                mode = GameMode.Arena;
                return true;
            }

            if (GameModeTokens.TryRead(raw.Trim(), out mode))
            {
                return true;
            }

            error = $"{ModeField} must be arena, classic, classic_blinded or survival, not \"{raw.Trim()}\".";
            return false;
        }

        private static bool TryBots(string raw, out BotSkill skill, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(raw))
            {
                skill = BotSkill.Normal;
                return true;
            }

            if (BotSkills.TryRead(raw.Trim(), out skill))
            {
                return true;
            }

            error = $"{BotsField} must be easy, normal or hard, not \"{raw.Trim()}\".";
            return false;
        }

        private static bool TryPlayers(string raw, out int players, out string error)
        {
            if (!TryNumber(raw, PlayersField, out players, out error))
            {
                return false;
            }

            // Only the floor is checked here. How many players an arena can actually
            // seat is however many spawns it generated, which MatchFactory knows and
            // this does not, so restating a ceiling here would be a second answer to
            // drift away from the first.
            if (players < 1)
            {
                error = $"{PlayersField} must be at least 1, not {players}.";
                return false;
            }

            return true;
        }

        private static bool TryHumans(string raw, int players, out int humans, out string error)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                humans = players;
                error = null;
                return true;
            }

            if (!TryNumber(raw, HumansField, out humans, out error))
            {
                return false;
            }

            if (humans < 1 || humans > players)
            {
                error = $"{HumansField} must be between 1 and {PlayersField} ({players}), not {humans}.";
                return false;
            }

            return true;
        }

        private static bool TryMatchId(string raw, out string matchId, out string error)
        {
            matchId = raw == null ? null : raw.Trim();

            if (string.IsNullOrEmpty(matchId))
            {
                error = Missing(MatchField);
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryNumber(string raw, string field, out int value, out string error)
        {
            value = 0;

            if (string.IsNullOrWhiteSpace(raw))
            {
                error = Missing(field);
                return false;
            }

            // Invariant on purpose. A host with a different locale must not read a
            // number differently from the one that wrote it.
            if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                error = $"{field} must be a whole number, not \"{raw}\".";
                return false;
            }

            error = null;
            return true;
        }

        private static string Missing(string field)
        {
            return $"the assignment has no {field}.";
        }
    }
}
