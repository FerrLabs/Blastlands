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

        public static bool Waiting
        {
            get { return pending.CanConnect; }
        }

        public static void Leave(MatchInvite invite)
        {
            pending = invite;
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
