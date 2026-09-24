namespace Blastlands.Core.Lobby
{
    // Everything a client needs to reach the match it was admitted to: where the game
    // server listens, and the signed ticket that gets it through the door. The lobby
    // hands both back from create and from join, and neither is guessable, which is why
    // the match id alone is not enough to connect.
    public readonly struct MatchInvite
    {
        public MatchInvite(string matchId, string host, int port, string ticket, GameMode mode)
        {
            Mode = mode;
            MatchId = matchId;
            Host = host;
            Port = port;
            Ticket = ticket;
        }

        public string MatchId { get; }

        public string Host { get; }

        public int Port { get; }

        public string Ticket { get; }

        // Carried with the invite rather than learnt from the game server, because the
        // client has to build the same rules the server runs before the first snapshot
        // lands: prediction steps the local player with them from the first tick.
        public GameMode Mode { get; }

        public bool CanConnect
        {
            get
            {
                return !string.IsNullOrEmpty(Host)
                    && Port > 0
                    && Port <= ushort.MaxValue
                    && !string.IsNullOrEmpty(Ticket);
            }
        }
    }
}
