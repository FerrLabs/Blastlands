namespace Blastlands.Core.Lobby
{
    // Every way the lobby can say no, by name.
    //
    // Named rather than left as the wire string because the screens have to react
    // differently: a full match is a listing that went stale and the player should just
    // pick another, an outdated client is a dead end until they update, and a name the
    // lobby refused is something they can fix on the spot. A screen holding a string
    // ends up comparing it in three places and getting one of them wrong.
    public enum LobbyFailure : byte
    {
        // Nothing came back at all: no lobby at that address, or the network dropped.
        Unreachable = 0,

        // The lobby answered something this build cannot read. Kept separate from
        // Unreachable because one is worth retrying and the other is not.
        Unreadable = 1,

        InvalidName = 2,
        InvalidPlayerCount = 3,
        MatchNotFound = 4,
        MatchFull = 5,
        MatchAlreadyStarted = 6,
        NoCapacity = 7,
        ClientTooOld = 8,
        InvalidVersion = 9,
        Unauthorized = 10,

        // Too fast. The lobby throttles creates and joins per address, and a screen
        // should say "wait a moment" rather than "something went wrong".
        RateLimited = 12,

        // Not too fast, but holding too many matches at once. Different advice: close
        // one rather than wait.
        TooManyMatches = 13,

        // Start was refused because the lobby has not filled enough seats. The one
        // refusal the host fixes by waiting rather than by re-reading the listing.
        NotEnoughPlayers = 14,

        // The lobby refused with a code this build has never heard of, which happens
        // when the server is ahead of the client.
        Unknown = 11
    }

    public static class LobbyFailures
    {
        public static LobbyFailure FromCode(string code)
        {
            switch (code)
            {
                case "invalid_name":
                    return LobbyFailure.InvalidName;
                case "invalid_player_count":
                    return LobbyFailure.InvalidPlayerCount;
                case "match_not_found":
                    return LobbyFailure.MatchNotFound;
                case "match_full":
                    return LobbyFailure.MatchFull;
                case "match_already_started":
                    return LobbyFailure.MatchAlreadyStarted;
                case "no_capacity":
                    return LobbyFailure.NoCapacity;
                case "client_too_old":
                    return LobbyFailure.ClientTooOld;
                case "invalid_version":
                    return LobbyFailure.InvalidVersion;
                case "unauthorized":
                    return LobbyFailure.Unauthorized;
                case "rate_limited":
                    return LobbyFailure.RateLimited;
                case "too_many_matches":
                    return LobbyFailure.TooManyMatches;
                case "not_enough_players":
                    return LobbyFailure.NotEnoughPlayers;
                default:
                    return LobbyFailure.Unknown;
            }
        }

        // Whether the sensible answer is to refresh the list and let the player choose
        // again. A match filling up or starting between the listing and the join is the
        // normal case rather than an error worth a dialogue.
        public static bool MeansTheListingWentStale(LobbyFailure failure)
        {
            return failure == LobbyFailure.MatchFull
                || failure == LobbyFailure.MatchAlreadyStarted
                || failure == LobbyFailure.MatchNotFound;
        }

        // Whether the player can fix it themselves without leaving the screen.
        public static bool IsTheirsToFix(LobbyFailure failure)
        {
            return failure == LobbyFailure.InvalidName
                || failure == LobbyFailure.InvalidPlayerCount
                || failure == LobbyFailure.TooManyMatches;
        }
    }
}
