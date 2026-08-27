namespace Blastlands.Core.Lobby
{
    // What a lobby call came back with: a value, or a named reason it did not.
    //
    // One type rather than an out-parameter and a bool, because every screen has to
    // handle both halves and the compiler should not let one be read without the other
    // having been checked. Ok carries the value; anything else carries a Failure the
    // screen can ask questions about.
    public readonly struct LobbyResult<T>
    {
        private LobbyResult(bool ok, T value, LobbyFailure failure)
        {
            Ok = ok;
            Value = value;
            Failure = failure;
        }

        public bool Ok { get; }

        // Meaningless unless Ok. Kept as a plain property rather than throwing, because
        // a screen reading it on a failed call is a bug the tests should catch rather
        // than an exception in front of a player mid-match.
        public T Value { get; }

        public LobbyFailure Failure { get; }

        public static LobbyResult<T> Success(T value)
        {
            return new LobbyResult<T>(true, value, LobbyFailure.Unknown);
        }

        public static LobbyResult<T> Failed(LobbyFailure failure)
        {
            return new LobbyResult<T>(false, default(T), failure);
        }

        // True when trying the same call again could plausibly work: the lobby was not
        // reachable, or it answered something this build could not read. A refused call
        // is refused for a reason and retrying it just annoys the server.
        public bool WorthRetrying
        {
            get { return !Ok && (Failure == LobbyFailure.Unreachable || Failure == LobbyFailure.Unreadable); }
        }
    }
}
