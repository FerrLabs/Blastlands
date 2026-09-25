#if UNITY_SERVER
using System;
using Blastlands.Core;
using Blastlands.Core.Net;
using UnityEngine;

namespace Blastlands.Runtime
{
    // The entry point of a dedicated server build. There is no scene to wire it into and
    // no inspector to fill in, so it starts itself: the build is launched by a
    // supervisor, and everything it needs comes from the environment and the arguments.
    //
    // Guarded on UNITY_SERVER rather than on a hand-added SERVER define. Unity sets this
    // one itself for the Dedicated Server subtarget, which is the same subtarget
    // build.yml already asks for, so there is no project setting anybody has to remember
    // to keep in step with the workflow.
    public static class ServerBootstrap
    {
        // The exit code a supervisor reads. Anything non-zero has to mean the process
        // failed, or a crash loop looks like a clean shutdown.
        public const int Ok = 0;
        public const int BadConfiguration = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Begin()
        {
            if (!HostOptions.TryRead(
                    Environment.GetCommandLineArgs(),
                    Environment.GetEnvironmentVariable,
                    out HostOptions options,
                    out string problem))
            {
                Fail(problem);
                return;
            }

            if (!GameTicketVerifier.TryReadKey(Environment.GetEnvironmentVariable, out byte[] ticketKey, out problem))
            {
                Fail(problem);
                return;
            }

            var host = new GameObject("Blastlands Server");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<ServerHost>().Run(options, ticketKey);
        }

        private static void Fail(string reason)
        {
            Debug.LogError("Blastlands server: " + reason);
            Application.Quit(BadConfiguration);
        }
    }
}
#endif
