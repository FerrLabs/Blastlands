using System;
using System.Collections.Generic;
using System.Globalization;

namespace Blastlands.Core
{
    // What a server process is told when it starts, and keeps for its whole life: which
    // ports it serves, where the lobby is, and how to prove to it who is asking.
    //
    // A process runs `Slots` matches side by side, one per port, from `FirstPort` up. The
    // lobby hands matches out by port and cannot tell whether two ports belong to one
    // process or to two, so how many a process carries is a deployment choice made here
    // and nowhere else.
    //
    // The ports come either from BLASTLANDS_PORT, the first of the run, or from a base
    // and the ordinal at the end of the pod's name. The ordinal is scaled by the slot
    // count, so pod 2 with four slots serves base + 8 to base + 11, and one slot per pod
    // lays the ports out exactly as they were before a process could carry more.
    public readonly struct HostOptions
    {
        public const string PortFlag = "--port";
        public const string LobbyFlag = "--lobby";
        public const string TokenFlag = "--token";

        public const string PortVariable = "BLASTLANDS_PORT";
        public const string PortBaseVariable = "BLASTLANDS_PORT_BASE";
        public const string PodNameVariable = "BLASTLANDS_POD_NAME";
        public const string SlotsVariable = "BLASTLANDS_MATCHES_PER_SERVER";
        public const string LobbyVariable = "BLASTLANDS_LOBBY";
        public const string PollVariable = "BLASTLANDS_POLL_SECONDS";
        public const string DrainFileVariable = "BLASTLANDS_DRAIN_FILE";

        // The same name the lobby reads it under, because it is the same secret. Two
        // names for one value is how they end up different on one host.
        public const string TokenVariable = "BLASTLANDS_INSTANCE_TOKEN";

        public const int DefaultSlots = 1;
        public const int MaxSlots = 32;
        public const int DefaultPollSeconds = 2;

        // A slot's poll is also how it tells the lobby it is free: PortPool only offers a
        // port that asked within INSTANCE_READY_TTL, 15 s. The widest gap is the interval
        // plus the request's own timeout, so a longer interval than this empties the pool
        // without a single error to say why.
        public const int MaxPollSeconds = 4;

        public HostOptions(
            int firstPort, int slots, string lobbyUrl, string instanceToken, int pollSeconds, string drainFile)
        {
            FirstPort = firstPort;
            Slots = slots;
            LobbyUrl = lobbyUrl;
            InstanceToken = instanceToken;
            PollSeconds = pollSeconds;
            DrainFile = drainFile;
        }

        public int FirstPort { get; }

        public int Slots { get; }

        public string LobbyUrl { get; }

        public string InstanceToken { get; }

        public int PollSeconds { get; }

        // Where the supervisor signals that the pod is going away. Null when nothing
        // supervises the process, which then simply never drains.
        public string DrainFile { get; }

        public int PortOf(int slot)
        {
            return FirstPort + slot;
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

            if (!TrySlots(Read(null, environment, null, SlotsVariable), out int slots, out error))
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

            if (!TryToken(Read(arguments, environment, TokenFlag, TokenVariable), out string token, out error))
            {
                return false;
            }

            if (!TryPoll(Read(null, environment, null, PollVariable), out int poll, out error))
            {
                return false;
            }

            string drain = Read(null, environment, null, DrainFileVariable);
            drain = string.IsNullOrWhiteSpace(drain) ? null : drain.Trim();

            options = new HostOptions(firstPort, slots, lobby, token, poll, drain);
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

            if (!TryNumber(raw, SlotsVariable, out slots, out error))
            {
                return false;
            }

            if (slots < 1 || slots > MaxSlots)
            {
                error = $"{SlotsVariable} must be between 1 and {MaxSlots}, not {slots}.";
                return false;
            }

            return true;
        }

        private static bool TryFirstPort(
            IReadOnlyList<string> arguments, Func<string, string> environment, int slots, out int port, out string error)
        {
            port = 0;
            string raw = Read(arguments, environment, PortFlag, PortVariable);

            if (!string.IsNullOrWhiteSpace(raw))
            {
                if (!TryNumber(raw, PortFlag, out port, out error))
                {
                    return false;
                }
            }
            else if (!TryPortFromPod(environment, slots, out port, out error))
            {
                return false;
            }

            // The whole range is allowed rather than only the unprivileged part. The
            // lobby hands out ports from its own range, and refusing one it allocates
            // would be this process arguing with the thing that placed it.
            int last = port + slots - 1;
            if (port < 1 || last > 65535)
            {
                error = slots == 1
                    ? $"{PortFlag} must be a port between 1 and 65535, not {port}."
                    : $"the ports {port} to {last} must lie between 1 and 65535.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryPortFromPod(Func<string, string> environment, int slots, out int port, out string error)
        {
            port = 0;
            string rawBase = Read(null, environment, null, PortBaseVariable);
            string pod = Read(null, environment, null, PodNameVariable);

            if (string.IsNullOrWhiteSpace(rawBase) || string.IsNullOrWhiteSpace(pod))
            {
                error = $"{PortFlag} is required, either as an argument, as {PortVariable}, "
                    + $"or as {PortBaseVariable} with {PodNameVariable}.";
                return false;
            }

            if (!TryNumber(rawBase, PortBaseVariable, out int portBase, out error))
            {
                return false;
            }

            pod = pod.Trim();
            string suffix = pod.Substring(pod.LastIndexOf('-') + 1);
            if (!int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out int ordinal))
            {
                error = $"{PodNameVariable}={pod} does not end in an ordinal.";
                return false;
            }

            port = portBase + (ordinal * slots);
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

        private static string Missing(string flag, string variable)
        {
            return $"{flag} is required, either as an argument or as {variable}.";
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
