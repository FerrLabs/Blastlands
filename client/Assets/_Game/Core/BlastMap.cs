using System.Collections.Generic;

namespace Blastlands.Core
{
    // For every tile, how many ticks until it catches fire. This is the whole basis
    // of bot survival: everything else is pathing around it.
    //
    // Chains are handled for free by resolving each bomb through ExplosionResolver:
    // if a bomb with ten ticks left reaches one with fifty, the second bomb's tiles
    // burn in ten, and taking the minimum records exactly that.
    public sealed class BlastMap
    {
        public const int Never = int.MaxValue;

        private readonly Dictionary<GridPos, int> ticksUntilFire;

        private BlastMap(Dictionary<GridPos, int> ticksUntilFire)
        {
            this.ticksUntilFire = ticksUntilFire;
        }

        public static BlastMap From(MatchState state)
        {
            return From(state, state.Bombs);
        }

        public static BlastMap From(MatchState state, IReadOnlyList<ActiveBomb> bombs)
        {
            var times = new Dictionary<GridPos, int>();

            // Already burning: nothing is more urgent than a tile on fire now.
            for (int i = 0; i < state.Flames.Count; i++)
            {
                times[state.Flames[i].Tile] = 0;
            }

            if (bombs.Count > 0)
            {
                var definitions = new List<Bomb>(bombs.Count);
                for (int i = 0; i < bombs.Count; i++)
                {
                    definitions.Add(bombs[i].Bomb);
                }

                var trigger = new int[1];
                for (int i = 0; i < bombs.Count; i++)
                {
                    trigger[0] = i;
                    ExplosionResult result = ExplosionResolver.Resolve(state.Arena, definitions, trigger);
                    int fuse = bombs[i].FuseRemaining;

                    for (int f = 0; f < result.FlameTiles.Count; f++)
                    {
                        GridPos tile = result.FlameTiles[f];
                        int existing;
                        if (!times.TryGetValue(tile, out existing) || fuse < existing)
                        {
                            times[tile] = fuse;
                        }
                    }
                }
            }

            return new BlastMap(times);
        }

        public int TicksUntilFire(GridPos tile)
        {
            int ticks;
            return ticksUntilFire.TryGetValue(tile, out ticks) ? ticks : Never;
        }

        // Safe to stand on for the next `horizon` ticks. A bot with a short horizon is
        // not cheating less, it is simply looking less far ahead.
        public bool IsSafeFor(GridPos tile, int horizon)
        {
            return TicksUntilFire(tile) > horizon;
        }

        // Safe to still be standing there having spent `arrival` ticks getting to it.
        public bool SurvivesArrival(GridPos tile, int arrival, int margin)
        {
            int fire = TicksUntilFire(tile);
            return fire == Never || arrival + margin < fire;
        }
    }
}
