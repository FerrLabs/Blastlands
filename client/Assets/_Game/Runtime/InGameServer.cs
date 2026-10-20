using Blastlands.Core;
using UnityEngine;

namespace Blastlands.Runtime
{
    public static class InGameServer
    {
        private static ServerHost running;

        public static ServerHost Ensure(HostOptions options)
        {
            if (running != null && running.Problem == null)
            {
                return running;
            }

            if (running != null)
            {
                Object.Destroy(running.gameObject);
            }

            var host = new GameObject("Blastlands In-Game Server");
            Object.DontDestroyOnLoad(host);
            running = host.AddComponent<ServerHost>();
            running.Run(options.InGame());
            return running;
        }
    }
}
