using System;
using System.Collections.Generic;
using System.Globalization;

namespace Blastlands.Core
{
    // What an instance is told when it starts: which port to listen on, which match it
    // is, how many players to wait for, and where to report back.
    //
    // Every one of them is required. There is no useful default for any: a port guessed
    // wrong collides with another instance on the same host, a wrong match id releases
    // somebody else's match, and an instance that cannot reach the lobby holds its port
    // until a restart. Refusing to start with a message an operator can act on beats
    // starting into any of that.
    public readonly struct ServerOptions
    {
        public const string PortFlag = "--port";
        public const string MatchFlag = "--match";
        public const string PlayersFlag = "--players";
        public const string LobbyFlag = "--lobby";
        public const string TokenFlag = "--token";

        public const string PortVariable = "BLASTLANDS_PORT";
        public const string MatchVariable = "BLASTLANDS_MATCH";
        public const string PlayersVariable = "BLASTLANDS_PLAYERS";
        public const string LobbyVariable = "BLASTLANDS_LOBBY";

        // The same name the lobby reads it under, because it is the same secret. Two
        // names for one value is how they end up different on one host.
        public const string TokenVariable = "BLASTLANDS_INSTANCE_TOKEN";

        public ServerOptions(
            int listenPort, string matchId, int expectedPlayers, string lobbyUrl, string instanceToken)
        {
            ListenPort = listenPort;
            MatchId = matchId;
            ExpectedPlayers = expectedPlayers;
            LobbyUrl = lobbyUrl;
            InstanceToken = instanceToken;
        }

        public int ListenPort { get; }

        public string MatchId { get; }

        public int ExpectedPlayers { get; }

        public string LobbyUrl { get; }

        // What the lobby's internal endpoints check. An instance without it can heartbeat
        // and release into a 401 forever, which means its port is gone until somebody
        // restarts the lobby.
        public string InstanceToken { get; }

        // Arguments win over the environment. The environment is how a host is
        // configured once; the arguments are how one instance out of several on that
        // host is told what it is, so the more specific of the two has to be the one
        // that counts.
        //
        // `environment` is passed in rather than read, so this stays engine-free and so
        // a test does not have to mutate the process it runs in.
        public static bool TryRead(
            IReadOnlyList<string> arguments,
            Func<string, string> environment,
            out ServerOptions options,
            out string error)
        {
            options = default;

            string rawPort = Read(arguments, environment, PortFlag, PortVariable);
            string rawMatch = Read(arguments, environment, MatchFlag, MatchVariable);
            string rawPlayers = Read(arguments, environment, PlayersFlag, PlayersVariable);
            string rawLobby = Read(arguments, environment, LobbyFlag, LobbyVariable);
            string rawToken = Read(arguments, environment, TokenFlag, TokenVariable);

            if (!TryPort(rawPort, out int port, out error))
            {
                return false;
            }

            if (!TryMatchId(rawMatch, out string matchId, out error))
            {
                return false;
            }

            if (!TryPlayers(rawPlayers, out int players, out error))
            {
                return false;
            }

            if (!TryLobby(rawLobby, out string lobby, out error))
            {
                return false;
            }

            if (!TryToken(rawToken, out string token, out error))
            {
                return false;
            }

            options = new ServerOptions(port, matchId, players, lobby, token);
            error = null;
            return true;
        }

        private static bool TryPort(string raw, out int port, out string error)
        {
            port = 0;

            if (!TryNumber(raw, PortFlag, PortVariable, out port, out error))
            {
                return false;
            }

            // The whole range is allowed rather than only the unprivileged part. The
            // lobby hands out the port from its own pool, and refusing what it just
            // allocated would be this process arguing with the thing that placed it.
            if (port < 1 || port > 65535)
            {
                error = $"{PortFlag} must be a port between 1 and 65535, not {port}.";
                return false;
            }

            return true;
        }

        private static bool TryPlayers(string raw, out int players, out string error)
        {
            if (!TryNumber(raw, PlayersFlag, PlayersVariable, out players, out error))
            {
                return false;
            }

            // Only the floor is checked here. How many players an arena can actually
            // seat is however many spawns it generated, which MatchFactory knows and
            // this does not, so restating a ceiling here would be a second answer to
            // drift away from the first.
            if (players < 1)
            {
                error = $"{PlayersFlag} must be at least 1, not {players}.";
                return false;
            }

            return true;
        }

        private static bool TryMatchId(string raw, out string matchId, out string error)
        {
            matchId = raw == null ? null : raw.Trim();

            if (string.IsNullOrEmpty(matchId))
            {
                error = Missing(MatchFlag, MatchVariable);
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryLobby(string raw, out string lobby, out string error)
        {
            lobby = raw == null ? null : raw.Trim();

            if (string.IsNullOrEmpty(lobby))
            {
                error = Missing(LobbyFlag, LobbyVariable);
                return false;
            }

            if (!lobby.StartsWith("http://", StringComparison.Ordinal)
                && !lobby.StartsWith("https://", StringComparison.Ordinal))
            {
                error = $"{LobbyFlag} must be an http or https URL, not \"{lobby}\".";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryToken(string raw, out string token, out string error)
        {
            token = raw == null ? null : raw.Trim();

            if (string.IsNullOrEmpty(token))
            {
                error = Missing(TokenFlag, TokenVariable);
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryNumber(string raw, string flag, string variable, out int value, out string error)
        {
            value = 0;

            if (string.IsNullOrEmpty(raw))
            {
                error = Missing(flag, variable);
                return false;
            }

            // Invariant on purpose. A host with a different locale must not read a
            // number differently from the one that wrote it.
            if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                error = $"{flag} must be a whole number, not \"{raw}\".";
                return false;
            }

            error = null;
            return true;
        }

        private static string Missing(string flag, string variable)
        {
            return $"{flag} is required, either as an argument or as {variable}.";
        }

        private static string Read(
            IReadOnlyList<string> arguments, Func<string, string> environment, string flag, string variable)
        {
            if (arguments != null)
            {
                for (int i = 0; i < arguments.Count - 1; i++)
                {
                    if (string.Equals(arguments[i], flag, StringComparison.Ordinal))
                    {
                        return arguments[i + 1];
                    }
                }
            }

            return environment == null ? null : environment(variable);
        }
    }
}
