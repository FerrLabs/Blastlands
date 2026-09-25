namespace Blastlands.Core
{
    internal static class BotZombies
    {
        internal static int ClosestZombie(MatchState state, GridPos tile)
        {
            int closest = int.MaxValue;
            for (int i = 0; i < state.Zombies.Count; i++)
            {
                GridPos at = state.Zombies[i].Tile;
                int steps = System.Math.Abs(at.X - tile.X) + System.Math.Abs(at.Y - tile.Y);
                if (steps < closest)
                {
                    closest = steps;
                }
            }

            return closest;
        }

        internal static bool ZombieInReach(MatchState state, GridPos tile, int range)
        {
            for (int i = 0; i < state.Zombies.Count; i++)
            {
                if (BotGrid.Reaches(state.Arena, tile, state.Zombies[i].Tile, range, state.Settings.Rules.Blast))
                {
                    return true;
                }
            }

            return false;
        }

        internal static Direction AwayFromZombies(MatchState state, BlastMap blast, GridPos tile)
        {
            Direction best = Direction.None;
            int bestSteps = ClosestZombie(state, tile);

            for (int i = 0; i < BotGrid.Order.Length; i++)
            {
                GridPos delta = Directions.Delta(BotGrid.Order[i]);
                GridPos next = tile.Offset(delta.X, delta.Y);
                if (!state.Arena.Contains(next)
                    || !Tiles.CanBeStoodOn(state.Arena[next])
                    || state.HasBombAt(next)
                    || blast.TicksUntilFire(next) != BlastMap.Never)
                {
                    continue;
                }

                int steps = ClosestZombie(state, next);
                if (steps > bestSteps)
                {
                    best = BotGrid.Order[i];
                    bestSteps = steps;
                }
            }

            return best;
        }

        internal static bool NextToAZombie(MatchState state, GridPos tile)
        {
            for (int i = 0; i < state.Zombies.Count; i++)
            {
                GridPos at = state.Zombies[i].Tile;
                if (System.Math.Abs(at.X - tile.X) <= 1 && System.Math.Abs(at.Y - tile.Y) <= 1)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
