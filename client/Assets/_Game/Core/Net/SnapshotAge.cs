using System;

namespace Blastlands.Core.Net
{
    public static class SnapshotAge
    {
        public static int FuseAfter(ActiveBomb bomb, int ticks)
        {
            return Math.Max(0, bomb.FuseRemaining - Math.Max(0, ticks));
        }

        public static bool BurnsAfter(ActiveFlame flame, int ticks)
        {
            return flame.TicksRemaining - Math.Max(0, ticks) > 0;
        }
    }
}
