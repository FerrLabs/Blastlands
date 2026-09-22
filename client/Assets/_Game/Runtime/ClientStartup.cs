using System;
using Blastlands.Core;
using UnityEngine;

namespace Blastlands.Runtime
{
    public static class ClientStartup
    {
#if !UNITY_SERVER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CheckForUpdates()
        {
            var host = new GameObject("Client updates");
            UnityEngine.Object.DontDestroyOnLoad(host);

            LobbyClient lobby = host.AddComponent<LobbyClient>();
            lobby.Use(ClientOptions.Lobby(Environment.GetCommandLineArgs()));

            ClientUpdater updater = host.AddComponent<ClientUpdater>();

            VersionGate gate = host.AddComponent<VersionGate>();
            gate.Use(lobby, updater);

            host.AddComponent<UpdateBanner>().Use(gate, updater);
        }
#endif
    }
}
