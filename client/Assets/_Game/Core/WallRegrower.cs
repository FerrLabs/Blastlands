namespace Blastlands.Core
{
    // Puts destroyed soft blocks back. Once the blocks are gone the board is static and
    // two careful players circle each other forever; walls that grow back keep routes
    // opening and closing instead of ending the round with a guillotine.
    public static class WallRegrower
    {
        private static readonly Direction[] PushOrder =
        {
            Direction.Right,
            Direction.Down,
            Direction.Left,
            Direction.Up
        };

        public static void Tick(MatchState state)
        {
            for (int i = state.RegrowingWalls.Count - 1; i >= 0; i--)
            {
                WallRegrowth wall = state.RegrowingWalls[i];
                wall.TicksRemaining--;

                if (wall.TicksRemaining > 0)
                {
                    continue;
                }

                if (TryClose(state, wall.Tile, wall.Kind))
                {
                    state.RemoveRegrowthAt(i);
                }
                else
                {
                    // Something is in the way that must not be walled over. Come back
                    // shortly rather than dropping the regrowth, or a tile held open
                    // once stays open for the rest of the round.
                    wall.TicksRemaining = state.Settings.WallRetryTicks;
                }
            }
        }

        private static bool TryClose(MatchState state, GridPos tile, TileKind kind)
        {
            if (state.Arena[tile] != TileKind.Floor
                || state.HasBombAt(tile)
                || state.HasFlameAt(tile))
            {
                return false;
            }

            // A bush growing back around someone is not a problem: standing in one is
            // what it is for. Only something solid has to move them out of the way, and
            // only something solid takes back what is lying there.
            if (Tiles.BlocksMovement(kind))
            {
                if (!ClearPlayers(state, tile))
                {
                    return false;
                }

                // Postponing on a pickup instead would let a bomb nobody wants hold a
                // corridor open all round, and being shoved off your loot is a fair
                // price for dawdling.
                int pickup = state.LooseBombIndexAt(tile);
                if (pickup >= 0)
                {
                    state.RemoveLooseBombAt(pickup);
                }

                int powerUp = state.PowerUpIndexAt(tile);
                if (powerUp >= 0)
                {
                    state.RemovePowerUpAt(powerUp);
                }
            }

            state.Arena[tile] = kind;
            return true;
        }

        // A wall cannot kill: bombs are the only thing that does. It shoves instead, and
        // gives up for now when there is nowhere to shove to.
        private static bool ClearPlayers(MatchState state, GridPos tile)
        {
            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                if (!player.Alive || player.Tile != tile)
                {
                    continue;
                }

                GridPos destination;
                if (!TryFindRoom(state, tile, out destination))
                {
                    return false;
                }

                player.Position = SubPos.AtTileCentre(destination);
            }

            return true;
        }

        private static bool TryFindRoom(MatchState state, GridPos from, out GridPos destination)
        {
            for (int i = 0; i < PushOrder.Length; i++)
            {
                GridPos delta = Directions.Delta(PushOrder[i]);
                GridPos candidate = from.Offset(delta.X, delta.Y);

                if (state.Arena.Contains(candidate)
                    && state.Arena[candidate] == TileKind.Floor
                    && !state.HasBombAt(candidate)
                    && !state.HasFlameAt(candidate))
                {
                    destination = candidate;
                    return true;
                }
            }

            destination = from;
            return false;
        }
    }
}
