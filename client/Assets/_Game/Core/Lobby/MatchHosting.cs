namespace Blastlands.Core.Lobby
{
    // What creating a match hands back. Two tickets, because they prove different
    // things: the invite's gets this client through the game server's door, and the
    // host ticket proves to the lobby that this is the client that created the match
    // and may therefore start it. Match ids are public, so the lobby takes nobody's
    // word for that.
    public readonly struct MatchHosting
    {
        public MatchHosting(MatchListing listing, MatchInvite invite, string hostTicket)
        {
            Listing = listing;
            Invite = invite;
            HostTicket = hostTicket;
        }

        public MatchListing Listing { get; }

        public MatchInvite Invite { get; }

        public string HostTicket { get; }

        public bool CanStart
        {
            get { return Invite.CanConnect && !string.IsNullOrEmpty(HostTicket); }
        }
    }
}
