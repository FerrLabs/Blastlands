using Blastlands.Core;
using Blastlands.Core.Lobby;

namespace Blastlands.Runtime
{
    // What the lobby screens leave behind for the match scene: where to dial and what
    // to present at the door. A static rather than a scene object, because the scene
    // that knows it is being unloaded at the moment the one that needs it loads, and
    // carrying a DontDestroyOnLoad object across for two strings and a port is more
    // machinery than the thing it carries.
    //
    // Taken rather than read, so a second match cannot start on a stale invite: a
    // ticket names one match and one player, and the game server refuses a reused one.
    public static class MatchHandoff
    {
        private static MatchInvite pending;
        private static GameMode? practice;

        // Who the player was when they left the lobby, so coming back from a match
        // drops them on the match list rather than asking their name again.
        public static string Player { get; private set; } = string.Empty;

        public static bool Waiting
        {
            get { return pending.CanConnect; }
        }

        public static void Leave(MatchInvite invite, string player)
        {
            pending = invite;
            Player = player ?? string.Empty;
        }

        public static void Practise(GameMode mode, string player)
        {
            pending = default;
            practice = mode;
            Player = player ?? string.Empty;
        }

        public static bool TryTakePractice(out GameMode mode)
        {
            mode = practice ?? GameMode.Arena;
            bool asked = practice.HasValue;
            practice = null;
            return asked;
        }

        public static MatchInvite Take()
        {
            MatchInvite invite = pending;
            pending = default;
            return invite;
        }

        public static void Forget()
        {
            pending = default;
        }
    }
}
