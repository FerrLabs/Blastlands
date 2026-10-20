using System;
using System.Collections.Generic;
using System.Globalization;

namespace Blastlands.Core
{
    public readonly struct HostOptions
    {
        public const string PortFlag = "--port";
        public const string LobbyFlag = "--lobby";
        public const string SlotsFlag = "--slots";

        public const string PortVariable = "BLASTLANDS_PORT";
        public const string SlotsVariable = "BLASTLANDS_MATCHES_PER_SERVER";
        public const string LobbyVariable = "BLASTLANDS_LOBBY";
        public const string PollVariable = "BLASTLANDS_POLL_SECONDS";
        public const string DrainFileVariable = "BLASTLANDS_DRAIN_FILE";

        private const string SlotsName = SlotsFlag + " or " + SlotsVariable;

        public const int DefaultPort = 7777;
        public const int DefaultSlots = 1;
        public const int MaxSlots = 16;
        public const int DefaultPollSeconds = 2;

        // A slot's poll is also how it tells the lobby it is free: PortPool only offers a
        // port that asked within INSTANCE_READY_TTL, 15 s. The widest gap is the interval
        // plus the request's own timeout, so a longer interval than this empties the pool
        // without a single error to say why.
        public const int MaxPollSeconds = 4;

        public HostOptions(
            int firstPort, int slots, string lobbyUrl, int pollSeconds, string drainFile, ServerRole role)
        {
            Role = role;
            FirstPort = firstPort;
            Slots = slots;
            LobbyUrl = lobbyUrl;
            PollSeconds = pollSeconds;
            DrainFile = drainFile;
        }

        public ServerRole Role { get; }

        public bool Listed
        {
            get { return Role == ServerRole.Dedicated; }
        }

        public bool OwnsProcess
        {
            get { return Role == ServerRole.Dedicated; }
        }

        public int FirstPort { get; }

        public int Slots { get; }

        public string LobbyUrl { get; }

        public int PollSeconds { get; }

        // Where the supervisor signals that the pod is going away. Null when nothing
        // supervises the process, which then simply never drains.
        public string DrainFile { get; }

        public int PortOf(int slot)
        {
            return FirstPort + slot;
        }

        public HostOptions InGame()
        {
            return new HostOptions(FirstPort, 1, LobbyUrl, PollSeconds, null, ServerRole.InGame);
        }

        // Arguments win over the environment. The environment is how a host is
        // configured once; an argument is how one process out of several on that host is
        // told which ports are its own, so the more specific of the two has to count.
        //
        // `environment` is passed in rather than read, so this stays engine-free and so
        // a test does not have to mutate the process it runs in.
        public static bool TryRead(
            IReadOnlyList<string> arguments,
            Func<string, string> environment,
            out HostOptions options,
            out string error)
        {
            options = default;

            if (!TryNoDanglingFlag(arguments, out error))
            {
                return false;
            }

            if (!TrySlots(Read(arguments, environment, SlotsFlag, SlotsVariable), out int slots, out error))
            {
                return false;
            }

            if (!TryFirstPort(arguments, environment, slots, out int firstPort, out error))
            {
                return false;
            }

            if (!TryLobby(Read(arguments, environment, LobbyFlag, LobbyVariable), out string lobby, out error))
            {
                return false;
            }

            if (!TryPoll(Read(null, environment, null, PollVariable), out int poll, out error))
            {
                return false;
            }

            string drain = Read(null, environment, null, DrainFileVariable);
            drain = string.IsNullOrWhiteSpace(drain) ? null : drain.Trim();

            options = new HostOptions(firstPort, slots, lobby, poll, drain, ServerRole.Dedicated);
            error = null;
            return true;
        }

        private static bool TrySlots(string raw, out int slots, out string error)
        {
            slots = DefaultSlots;
            error = null;

            if (string.IsNullOrWhiteSpace(raw))
            {
                return true;
            }

            if (!TryNumber(raw, SlotsName, out slots, out error))
            {
                return false;
            }

            if (slots < 1 || slots > MaxSlots)
            {
                error = $"{SlotsName} must be between 1 and {MaxSlots}, not {slots}.";
                return false;
            }

            return true;
        }

        private static bool TryFirstPort(
            IReadOnlyList<string> arguments, Func<string, string> environment, int slots, out int port, out string error)
        {
            port = DefaultPort;
            string raw = Read(arguments, environment, PortFlag, PortVariable);

            if (!string.IsNullOrWhiteSpace(raw) && !TryNumber(raw, PortFlag, out port, out error))
            {
                return false;
            }

            // The whole range is allowed rather than only the unprivileged part. The
            // lobby hands out ports from its own range, and refusing one it allocates
            // would be this process arguing with the thing that placed it.
            int last = port + slots - 1;
            if (port < 1 || port > 65535 || last > 65535)
            {
                error = slots == 1
                    ? $"{PortFlag} must be a port between 1 and 65535, not {port}."
                    : $"the ports {port} to {last} must lie between 1 and 65535.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryPoll(string raw, out int seconds, out string error)
        {
            seconds = DefaultPollSeconds;
            error = null;

            if (string.IsNullOrWhiteSpace(raw))
            {
                return true;
            }

            if (!TryNumber(raw, PollVariable, out seconds, out error))
            {
                return false;
            }

            if (seconds < 1 || seconds > MaxPollSeconds)
            {
                error = $"{PollVariable} must be between 1 and {MaxPollSeconds}, not {seconds}.";
                return false;
            }

            return true;
        }

        private static bool TryLobby(string raw, out string lobby, out string error)
        {
            lobby = string.IsNullOrWhiteSpace(raw) ? ClientOptions.DefaultLobby : raw.Trim();

            if (!lobby.StartsWith("http://", StringComparison.Ordinal)
                && !lobby.StartsWith("https://", StringComparison.Ordinal))
            {
                error = $"{LobbyFlag} must be an http or https URL, not \"{lobby}\".";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryNoDanglingFlag(IReadOnlyList<string> arguments, out string error)
        {
            error = null;

            if (arguments == null || arguments.Count == 0)
            {
                return true;
            }

            string last = arguments[arguments.Count - 1];
            if (last == PortFlag || last == LobbyFlag || last == SlotsFlag)
            {
                error = $"{last} needs a value.";
                return false;
            }

            return true;
        }

        private static bool TryNumber(string raw, string name, out int value, out string error)
        {
            // Invariant on purpose. A host with a different locale must not read a
            // number differently from the one that wrote it.
            if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                error = $"{name} must be a whole number, not \"{raw}\".";
                return false;
            }

            error = null;
            return true;
        }

        private static string Read(
            IReadOnlyList<string> arguments, Func<string, string> environment, string flag, string variable)
        {
            if (arguments != null && flag != null)
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
