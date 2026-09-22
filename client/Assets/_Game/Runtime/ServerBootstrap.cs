#if UNITY_SERVER
using System;
using Blastlands.Core;
using Blastlands.Core.Net;
using UnityEngine;

namespace Blastlands.Runtime
{
    // The entry point of a dedicated server build. There is no scene to wire it into and
    // no inspector to fill in, so it starts itself: the build is launched by a
    // supervisor with arguments, and everything it needs comes from those.
    //
    // Guarded on UNITY_SERVER rather than on a hand-added SERVER define. Unity sets this
    // one itself for the Dedicated Server subtarget, which is the same subtarget
    // build.yml already asks for, so there is no project setting anybody has to remember
    // to keep in step with the workflow.
    public static class ServerBootstrap
    {
        // The exit code a supervisor reads. Anything non-zero has to mean the instance
        // failed, or a crash loop looks like a clean shutdown and the match never gets
        // released.
        public const int Ok = 0;
        public const int BadConfiguration = 1;
        public const int FailedToStart = 2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Begin()
        {
            if (!ServerOptions.TryRead(
                    Environment.GetCommandLineArgs(),
                    Environment.GetEnvironmentVariable,
                    out ServerOptions options,
                    out string problem))
            {
                Fail(BadConfiguration, problem);
                return;
            }

            if (!GameTicketVerifier.TryReadKey(Environment.GetEnvironmentVariable, out byte[] ticketKey, out problem))
            {
                Fail(BadConfiguration, problem);
                return;
            }

            // Logged before anything can go wrong with them, because these four are the
            // first thing anybody reads off a failed instance.
            Debug.Log(
                $"Blastlands server: match {options.MatchId}, port {options.ListenPort}, "
                + $"{options.ExpectedPlayers} players, lobby {options.LobbyUrl}");

            MatchState state;
            try
            {
                state = MatchFactory.Create(
                    ArenaSettings.Default, MatchSettings.Default, options.ExpectedPlayers, Seed());
            }
            catch (ArgumentOutOfRangeException bad)
            {
                // The arena is what knows how many it seats, so this is where a player
                // count too high for the board is caught rather than when it was read.
                Fail(FailedToStart, bad.Message);
                return;
            }

            var host = new GameObject("Blastlands Server");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<ServerLoop>().Run(state, options, new GameTicketVerifier(ticketKey, options.MatchId));
        }

        // Fresh per instance. The lobby hands out one match per instance, so there is
        // nothing to reproduce across them, and a fixed seed would give every match on
        // the host the same arena.
        private static uint Seed()
        {
            return (uint)UnityEngine.Random.Range(1, int.MaxValue);
        }

        internal static void Fail(int code, string reason)
        {
            Debug.LogError("Blastlands server: " + reason);
            Application.Quit(code);
        }
    }
}
#endif
