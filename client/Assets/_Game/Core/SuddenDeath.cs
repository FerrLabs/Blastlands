using System;
using System.Collections.Generic;

namespace Blastlands.Core
{
    // Closes the island from the coast inward, one ring at a time, until there is no
    // board left. Without it a round between two competent survivors does not end: both
    // read the same blast map, neither can corner the other, and wall regrowth holds the
    // arena at an equilibrium instead of tightening it. See #105.
    //
    // Unlike a regrowing wall, a closing ring kills whoever is standing on it. That is a
    // deliberate break with the rule WallRegrower states, and it is the whole mechanism:
    // a wall that shoves instead has to find somewhere to shove to, and the moment it
    // cannot it postpones, which is exactly the stalemate this is here to end.
    //
    // It needs no telegraph of its own. The ring that closes next is the one against the
    // edge of what already closed, so the warning is the shape of the board.
    public static class SuddenDeath
    {
        public static IReadOnlyList<PlayerState> Tick(MatchState state)
        {
            SuddenDeathSettings settings = state.Settings.SuddenDeath;
            if (!settings.Enabled || state.Tick < settings.StartTicks)
            {
                return Array.Empty<PlayerState>();
            }

            int due = ((state.Tick - settings.StartTicks) / settings.RingTicks) + 1;
            if (state.SuddenDeathRings >= due)
            {
                return Array.Empty<PlayerState>();
            }

            var taken = new List<PlayerState>();
            while (state.SuddenDeathRings < due)
            {
                Close(state, state.SuddenDeathRings + 1, taken);
                state.SuddenDeathRings++;
            }

            return taken;
        }

        public static PlayerState HeldOutLongest(MatchState state, IReadOnlyList<PlayerState> taken)
        {
            long centreX = (long)state.Arena.Width * SubPos.UnitsPerTile / 2;
            long centreY = (long)state.Arena.Height * SubPos.UnitsPerTile / 2;
            PlayerState nearest = null;
            long best = long.MaxValue;
            bool tied = false;

            foreach (PlayerState player in taken)
            {
                long dx = player.Position.X - centreX;
                long dy = player.Position.Y - centreY;
                long distance = (dx * dx) + (dy * dy);
                if (distance < best)
                {
                    best = distance;
                    nearest = player;
                    tied = false;
                }
                else if (distance == best)
                {
                    tied = true;
                }
            }

            return tied ? null : nearest;
        }

        private static void Close(MatchState state, int ring, List<PlayerState> taken)
        {
            Arena arena = state.Arena;

            for (int y = 0; y < arena.Height; y++)
            {
                for (int x = 0; x < arena.Width; x++)
                {
                    var tile = new GridPos(x, y);
                    if (arena[tile] == TileKind.Void || state.DistanceToCoast(tile) != ring)
                    {
                        continue;
                    }

                    Clear(state, tile, taken);
                    arena[tile] = TileKind.HardBlock;
                }
            }
        }

        // Everything the tile was carrying goes with it. A bomb left live under a wall
        // would go off inside solid rock, and a pickup sealed in is a power-up the round
        // silently loses.
        //
        // Players are taken on the body rather than on the tile under their centre.
        // Movement is free, so a body 0.7 of a tile across can be two thirds inside the
        // ring that closes and still be centred next door; on the centre alone that
        // player survives and then walks around with their body in the rock, which
        // PlayerBody explicitly allows by ignoring the tiles it already overlaps.
        private static void Clear(MatchState state, GridPos tile, List<PlayerState> taken)
        {
            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                if (player.Alive && PlayerBody.Covers(player.Position, state.Settings.PlayerRadius, tile))
                {
                    player.Alive = false;
                    taken.Add(player);
                }
            }

            for (int i = state.Bombs.Count - 1; i >= 0; i--)
            {
                if (state.Bombs[i].Position == tile)
                {
                    state.RemoveBombAt(i);
                }
            }

            for (int i = state.Flames.Count - 1; i >= 0; i--)
            {
                if (state.Flames[i].Tile == tile)
                {
                    state.RemoveFlameAt(i);
                }
            }

            for (int i = state.RegrowingWalls.Count - 1; i >= 0; i--)
            {
                if (state.RegrowingWalls[i].Tile == tile)
                {
                    state.RemoveRegrowthAt(i);
                }
            }

            int raised = state.RaisedWallIndexAt(tile);
            if (raised >= 0)
            {
                state.RemoveRaisedWallAt(raised);
            }

            int loose = state.LooseBombIndexAt(tile);
            if (loose >= 0)
            {
                state.RemoveLooseBombAt(loose);
            }

            int powerUp = state.PowerUpIndexAt(tile);
            if (powerUp >= 0)
            {
                state.RemovePowerUpAt(powerUp);
            }
        }
    }
}
