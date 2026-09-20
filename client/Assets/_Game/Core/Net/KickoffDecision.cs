namespace Blastlands.Core.Net
{
    // What an instance should do while it waits for its seats to fill.
    public enum Kickoff : byte
    {
        // Not everybody is in and there is still time. Tick nothing.
        Wait = 0,

        // Play. Either every seat is taken, or the wait ran out with enough of them.
        Play = 1,

        // Nobody worth playing against arrived. Release the match and exit, because a
        // port held by an empty arena is capacity gone until somebody notices.
        GiveUp = 2
    }

    // The rule #17 describes, on its own so it can be tested without a socket.
    //
    // The cost of being wrong is not symmetric. Starting too early plays a match with
    // seats nobody is in, which the players can see and leave; waiting forever holds one
    // of four ports for the life of the pod, which nobody can see at all. So the wait has
    // a ceiling, and the ceiling gives up rather than starting something pointless.
    public static class KickoffDecision
    {
        // The lobby refuses to start a match below two players, so two is what this
        // codebase already means by a match worth playing. A seat that never connects
        // after that should not cost the players who did turn up their match.
        public const int FewestToPlay = 2;

        public static Kickoff For(int seats, int connected, float waitedSeconds, float patienceSeconds)
        {
            if (connected >= seats)
            {
                return Kickoff.Play;
            }

            if (waitedSeconds < patienceSeconds)
            {
                return Kickoff.Wait;
            }

            return connected >= FewestToPlay ? Kickoff.Play : Kickoff.GiveUp;
        }
    }
}
