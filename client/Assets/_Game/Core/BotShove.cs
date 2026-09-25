namespace Blastlands.Core
{
    internal static class BotShove
    {
        // Whether shoving somebody right now puts them somewhere that burns.
        //
        // A shove carries the target several tiles in the direction the shover faces, so
        // the question is not where they are but where they end up. Checked against the
        // blast map that already exists rather than a hypothetical one: the danger has to
        // be on the board before the shove, which is what makes bomb-then-shove a plan
        // rather than a coincidence.
        internal static bool Kills(MatchState state, PlayerState player, BlastMap blast, out Direction into)
        {
            into = Direction.None;

            if (!player.CanPush)
            {
                return false;
            }

            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState other = state.Players[i];
                if (other.Id == player.Id || !Vision.CanSee(state, player, other))
                {
                    continue;
                }

                for (int d = 0; d < BotGrid.Order.Length; d++)
                {
                    if (!WithinReach(state, player, other, BotGrid.Order[d]))
                    {
                        continue;
                    }

                    if (BurnsOnArrival(state, blast, other, BotGrid.Order[d]))
                    {
                        into = BotGrid.Order[d];
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool WithinReach(MatchState state, PlayerState player, PlayerState other, Direction facing)
        {
            GridPos delta = Directions.Delta(facing);
            int offX = other.Position.X - player.Position.X;
            int offY = other.Position.Y - player.Position.Y;

            if ((offX * delta.X) + (offY * delta.Y) <= 0)
            {
                return false;
            }

            long reach = state.Settings.Push.Reach;
            return ((long)offX * offX) + ((long)offY * offY) <= reach * reach;
        }

        // Walks the tiles the shove would drag them across. Any one of them burning by
        // the time they are carried into it is a kill.
        //
        // Stops where PlayerBody.Blocks stops, bombs included. A bomb is solid once it is
        // down — the assumption the whole trap rule rests on — so one lying in the path
        // halts the target short of the fire beyond it, and reading past it predicts a
        // kill the simulation will not deliver.
        private static bool BurnsOnArrival(MatchState state, BlastMap blast, PlayerState other, Direction facing)
        {
            GridPos delta = Directions.Delta(facing);
            int distance = (state.Settings.Push.Speed * state.Settings.Push.Ticks) / SubPos.UnitsPerTile;

            for (int step = 1; step <= distance; step++)
            {
                GridPos tile = other.Tile.Offset(delta.X * step, delta.Y * step);
                if (!state.Arena.Contains(tile)
                    || Tiles.BlocksMovement(state.Arena[tile])
                    || state.HasBombAt(tile))
                {
                    return false;
                }

                int fire = blast.TicksUntilFire(tile);
                if (fire != BlastMap.Never && fire <= step * SubPos.UnitsPerTile / state.Settings.Push.Speed)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
