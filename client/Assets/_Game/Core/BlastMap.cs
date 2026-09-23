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

            IgniteLooseBombs(state, bombs, times);
            return new BlastMap(times);
        }

        private static void IgniteLooseBombs(MatchState state, IReadOnlyList<ActiveBomb> bombs, Dictionary<GridPos, int> times)
        {
            var definitions = new List<Bomb>(bombs.Count + state.LooseBombs.Count);
            for (int i = 0; i < bombs.Count; i++)
            {
                definitions.Add(bombs[i].Bomb);
            }

            // When it was lit, not merely that it was. A loose bomb reached later in one
            // pass can be reached sooner in the next, and the flames it throws move with
            // it: marking the tile once leaves those flames recorded at the old time,
            // which reads to a bot as seconds of clear ground it does not have.
            var lit = new Dictionary<GridPos, int>();
            var placed = new Dictionary<GridPos, int>();
            var trigger = new int[1];
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < state.LooseBombs.Count; i++)
                {
                    GridPos tile = state.LooseBombs[i];
                    int reached;
                    if (!times.TryGetValue(tile, out reached) || HasBombAt(bombs, tile))
                    {
                        continue;
                    }

                    int already;
                    if (lit.TryGetValue(tile, out already) && already <= reached)
                    {
                        continue;
                    }

                    lit[tile] = reached;
                    changed = true;

                    // One definition per tile however often it is re-lit, or the same
                    // bomb chains with itself and the list grows on every pass.
                    int at;
                    if (!placed.TryGetValue(tile, out at))
                    {
                        at = definitions.Count;
                        placed[tile] = at;
                        definitions.Add(new Bomb(tile, -1, state.Settings.StartingFireRange, BombKind.Standard));
                    }

                    trigger[0] = at;

                    int fuse = reached + state.Settings.LooseBombFuseTicks;
                    ExplosionResult result = ExplosionResolver.Resolve(state.Arena, definitions, trigger);
                    for (int f = 0; f < result.FlameTiles.Count; f++)
                    {
                        GridPos flame = result.FlameTiles[f];
                        int existing;
                        if (!times.TryGetValue(flame, out existing) || fuse < existing)
                        {
                            times[flame] = fuse;
                        }
                    }
                }
            }
        }

        private static bool HasBombAt(IReadOnlyList<ActiveBomb> bombs, GridPos tile)
        {
            for (int i = 0; i < bombs.Count; i++)
            {
                if (bombs[i].Bomb.Position == tile)
                {
                    return true;
                }
            }

            return false;
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
