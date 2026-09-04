namespace Blastlands.Runtime
{
    // When a bomb is close enough to going off to be worth a sound.
    //
    // Split out of MatchView because the interesting part is the refusal, not the
    // comparison: a bomb whose whole fuse is shorter than the warning would announce
    // itself the instant it lands, one tick after the drop it already played, and the
    // pair reads as a stutter rather than as a warning.
    public static class BombFuse
    {
        // A second of notice. Long enough to step out of a corridor, short enough that
        // the board is not constantly ticking at you.
        public static int WarningTicks(int ticksPerSecond)
        {
            return ticksPerSecond;
        }

        public static bool IsWarning(int fuseRemaining, int fuseTicks, int ticksPerSecond)
        {
            int lead = WarningTicks(ticksPerSecond);

            if (lead <= 0 || fuseTicks <= lead)
            {
                return false;
            }

            return fuseRemaining > 0 && fuseRemaining <= lead;
        }
    }
}
