namespace Blastlands.Runtime
{
    // Turns however long the last frame took into a whole number of simulation ticks.
    //
    // The simulation runs at a fixed rate and never on frame time, or the same inputs
    // stop producing the same match, which is the one property the whole design rests
    // on. This lives in Runtime rather than Core because it is the only place a float
    // is allowed to touch the decision, and Core is fixed-point on purpose.
    public sealed class TickPacer
    {
        private readonly float step;
        private readonly int maxCatchUp;
        private float accumulator;

        public TickPacer(int ticksPerSecond, int maxCatchUpTicks)
        {
            step = ticksPerSecond > 0 ? 1f / ticksPerSecond : 1f;
            maxCatchUp = maxCatchUpTicks > 0 ? maxCatchUpTicks : 1;
        }

        public int Advance(float elapsedSeconds)
        {
            if (elapsedSeconds > 0f)
            {
                accumulator += elapsedSeconds;
            }

            int ticks = 0;
            while (accumulator >= step && ticks < maxCatchUp)
            {
                accumulator -= step;
                ticks++;
            }

            // Whatever is left after the cap is thrown away rather than carried. A stall
            // long enough to owe more than the cap is never caught up: keeping the debt
            // means the next frames are all capped too, running flat out and staying
            // behind, which reads as the game speeding up and never recovering.
            if (ticks >= maxCatchUp)
            {
                accumulator = 0f;
            }

            return ticks;
        }
    }
}
