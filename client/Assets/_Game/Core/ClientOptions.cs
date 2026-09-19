using System;
using System.Collections.Generic;

namespace Blastlands.Core
{
    public static class ClientOptions
    {
        public const string DefaultLobby = "https://api.blastlands.ferrlabs.com";

        public static string Lobby(IReadOnlyList<string> arguments)
        {
            for (int i = 0; i < arguments.Count - 1; i++)
            {
                if (arguments[i] == ServerOptions.LobbyFlag)
                {
                    string value = arguments[i + 1].Trim();
                    if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                        || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        return value.TrimEnd('/');
                    }
                }
            }

            return DefaultLobby;
        }
    }
}
