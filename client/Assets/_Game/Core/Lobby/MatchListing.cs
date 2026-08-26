namespace Blastlands.Core.Lobby
{
    // One row of the match list, as the lobby reports it.
    //
    // A snapshot rather than a live object: by the time a player has read it and clicked,
    // the match may be full or already running. The screens treat that as the normal case,
    // which is why nothing here is mutable and why joining can fail on a row that looked
    // fine.
    public readonly struct MatchListing
    {
        public MatchListing(string id, string name, string host, int players, int maxPlayers)
        {
            Id = id;
            Name = name;
            Host = host;
            Players = players;
            MaxPlayers = maxPlayers;
        }

        public string Id { get; }

        public string Name { get; }

        public string Host { get; }

        public int Players { get; }

        public int MaxPlayers { get; }

        public bool IsFull
        {
            get { return Players >= MaxPlayers; }
        }

        public string Occupancy
        {
            get { return Players + "/" + MaxPlayers; }
        }
    }
}
