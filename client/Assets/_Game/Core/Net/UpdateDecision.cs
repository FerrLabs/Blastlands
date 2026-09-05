namespace Blastlands.Core.Net
{
    // What a build is allowed to do, given what the lobby says is current.
    public enum UpdateVerdict : byte
    {
        // Nothing to say. The build is current.
        UpToDate = 0,

        // Newer exists. Worth offering, not worth insisting on.
        UpdateAvailable = 1,

        // Too old to play. Not a nag: an outdated client desyncs the simulation quietly
        // rather than failing loudly, so letting it in corrupts everybody else's match.
        MustUpdate = 2,

        // Nothing was heard from the lobby, or what came back made no sense. Play.
        Unknown = 3
    }

    // The rule #45 describes, on its own so it can be tested without a socket.
    //
    // The bias is deliberate and asymmetric. Being wrong about "you may play" costs a
    // desynced match for everybody in it; being wrong about "you may not" costs one
    // person an evening. So a build below the minimum is refused on the lobby's word,
    // and a lobby that says nothing at all is not taken as permission to refuse.
    public static class UpdateDecision
    {
        public static UpdateVerdict For(string installed, string latest, string minimum)
        {
            if (!GameVersion.TryParse(installed, out GameVersion have))
            {
                // A build that cannot say what it is. Refusing here would brick every
                // client the day a version string changed shape, and the server gate is
                // the thing that actually keeps a bad build out of a match.
                return UpdateVerdict.Unknown;
            }

            bool knowsLatest = GameVersion.TryParse(latest, out GameVersion newest);
            bool knowsMinimum = GameVersion.TryParse(minimum, out GameVersion oldest);

            if (!knowsLatest && !knowsMinimum)
            {
                return UpdateVerdict.Unknown;
            }

            // Checked before the offer, so a build that is both below the minimum and
            // below the latest is told the truth that matters.
            if (knowsMinimum && have < oldest)
            {
                return UpdateVerdict.MustUpdate;
            }

            if (knowsLatest && have < newest)
            {
                return UpdateVerdict.UpdateAvailable;
            }

            return UpdateVerdict.UpToDate;
        }

        // A build newer than the lobby knows about is not an error worth blocking on: it
        // is what every developer running from the editor looks like, and what a player
        // looks like for the minutes between a release going out and the lobby being
        // told about it.
        public static bool MayPlay(UpdateVerdict verdict)
        {
            return verdict != UpdateVerdict.MustUpdate;
        }
    }
}
