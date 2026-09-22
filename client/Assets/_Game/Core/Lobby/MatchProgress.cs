namespace Blastlands.Core.Lobby
{
    // A match as the lobby sees it right now, for everyone waiting on somebody else to
    // press start.
    public readonly struct MatchProgress
    {
        private const string RunningState = "in_progress";

        public MatchProgress(MatchListing listing, bool running)
        {
            Listing = listing;
            Running = running;
        }

        public MatchListing Listing { get; }

        public bool Running { get; }

        // An unknown state reads as not running. A client that guessed the other way
        // would dial a game server that has not been told to start yet.
        public static bool Reads(string state)
        {
            return state == RunningState;
        }
    }
}
