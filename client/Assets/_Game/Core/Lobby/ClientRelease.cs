namespace Blastlands.Core.Lobby
{
    // What the lobby publishes about the build a player should be running.
    //
    // The two versions decide whether this client may play, which UpdateDecision works
    // out. The url and the hash are what an update would fetch and check, and they are
    // carried together on purpose: an update that downloads without checking is a remote
    // code execution vector, so the thing that provides one always provides the other.
    public readonly struct ClientRelease
    {
        public ClientRelease(string latest, string minimum, string downloadUrl, string sha256)
        {
            Latest = latest;
            Minimum = minimum;
            DownloadUrl = downloadUrl;
            Sha256 = sha256;
        }

        public string Latest { get; }

        public string Minimum { get; }

        public string DownloadUrl { get; }

        public string Sha256 { get; }

        // Ordinal and case-insensitive, both deliberately. The default StartsWith is
        // culture-sensitive, which is the wrong comparison for a security check because
        // characters with no collation weight are ignored under some cultures, so a
        // prefix can match text that is not literally https. And schemes are
        // case-insensitive per RFC 3986, so HTTPS:// is a real URL rather than a
        // rejected one.
        //
        // A release nobody can fetch or check is one nobody can act on. The verdict is
        // still worth showing, so this is asked separately rather than folded into
        // parsing: a client can be told it is out of date by a lobby that cannot yet say
        // where the new build lives.
        public bool CanBeFetched
        {
            get
            {
                return !string.IsNullOrEmpty(DownloadUrl)
                    && Blastlands.Core.Update.Digest.IsSha256(Sha256)
                    && DownloadUrl.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
