using System.Collections.Generic;

namespace Blastlands.Core
{
    // Keeps bombs lying around for players to find. An arena that runs dry ends as a
    // chase between people with nothing to throw, so the target is a floor rather than
    // a budget: the spawner only ever tops up.
    public static class BombSpawner
    {
        private static readonly GridPos[] Cardinals =
        {
            new GridPos(1, 0),
            new GridPos(0, 1),
            new GridPos(-1, 0),
            new GridPos(0, -1)
        };

        public static void Seed(MatchState state)
        {
            int target = TargetFor(state);
            for (int i = 0; i < target; i++)
            {
                TrySpawn(state);
            }
        }

        // Derived from the size of the arena rather than fixed, so growing the board does
        // not quietly starve it. Four bombs is generous on 15x13 and thin on 25x21.
        public static int TargetFor(MatchState state)
        {
            int perBomb = state.Settings.TilesPerLooseBomb;
            if (perBomb <= 0)
            {
                return 0;
            }

            return (state.Arena.Width * state.Arena.Height) / perBomb;
        }

        public static void Tick(MatchState state)
        {
            if (state.Settings.BombRespawnTicks <= 0
                || state.Tick % state.Settings.BombRespawnTicks != 0
                || state.LooseBombs.Count >= TargetFor(state))
            {
                return;
            }

            TrySpawn(state);
        }

        private static bool TrySpawn(MatchState state)
        {
            var candidates = new List<GridPos>();
            CollectReachable(state, candidates);

            if (candidates.Count == 0)
            {
                return false;
            }

            state.AddLooseBomb(candidates[state.Random.NextInt(candidates.Count)]);
            return true;
        }

        // Floor alone is not enough to be a spawn: a free tile can be sealed inside a
        // ring of soft blocks, and a bomb dropped there is invisible to the arena. The
        // count then says the arena is stocked while nobody can reach a single one,
        // which is the exact failure the target was meant to prevent.
        //
        // Walking out from the living players answers "can anyone get to it" directly.
        private static void CollectReachable(MatchState state, List<GridPos> into)
        {
            var seen = new HashSet<GridPos>();
            var queue = new Queue<GridPos>();

            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                if (player.Alive && seen.Add(player.Tile))
                {
                    queue.Enqueue(player.Tile);
                }
            }

            while (queue.Count > 0)
            {
                GridPos current = queue.Dequeue();

                if (CanHoldBomb(state, current))
                {
                    into.Add(current);
                }

                for (int i = 0; i < Cardinals.Length; i++)
                {
                    GridPos next = current.Offset(Cardinals[i].X, Cardinals[i].Y);

                    if (state.Arena.Contains(next)
                        && state.Arena[next] == TileKind.Floor
                        && seen.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }
        }

        // Never under a player: a bomb that lands on someone is picked up for free, and
        // never on fire or on a live bomb, which would destroy it before it is seen.
        private static bool CanHoldBomb(MatchState state, GridPos tile)
        {
            if (state.Arena[tile] != TileKind.Floor
                || state.HasBombAt(tile)
                || state.HasFlameAt(tile)
                || state.LooseBombIndexAt(tile) >= 0
                || state.PowerUpIndexAt(tile) >= 0)
            {
                return false;
            }

            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                if (player.Alive && player.Tile == tile)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
