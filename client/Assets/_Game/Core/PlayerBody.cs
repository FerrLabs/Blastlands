using System.Collections.Generic;

namespace Blastlands.Core
{
    // Moves a player through the arena in sub-tile units, sliding along whatever it
    // is pressed against instead of stopping dead.
    //
    // The grid used to do this for free: movement was locked to an axis and pulled the
    // player onto the centre of their corridor, so they could never be half-way into a
    // wall. Free positions remove that, and without a replacement the player catches on
    // every pillar. This is the replacement.
    public static class PlayerBody
    {
        public static void Move(MatchState state, PlayerState player, int deltaX, int deltaY)
        {
            // The tiles the player is already inside cannot block them. It is what lets
            // someone walk off the bomb they just placed, and it stops a wall that grew
            // back under them from locking them in place.
            HashSet<GridPos> ignored = Overlapping(state, player.Position, state.Settings.PlayerRadius);

            SubPos position = player.Position;
            position = ResolveX(state, position, deltaX, ignored);
            position = ResolveY(state, position, deltaY, ignored);
            player.Position = position;
        }

        private static SubPos ResolveX(MatchState state, SubPos from, int delta, HashSet<GridPos> ignored)
        {
            if (delta == 0)
            {
                return from;
            }

            int radius = state.Settings.PlayerRadius;
            int x = from.X + delta;
            int edge = delta > 0 ? x + radius : x - radius;
            int column = Floor(edge);

            int firstRow = Floor(from.Y - radius);
            int lastRow = Floor(from.Y + radius);

            for (int row = firstRow; row <= lastRow; row++)
            {
                var tile = new GridPos(column, row);
                if (!Blocks(state, tile, ignored))
                {
                    continue;
                }

                x = delta > 0
                    ? (column * SubPos.UnitsPerTile) - radius - 1
                    : ((column + 1) * SubPos.UnitsPerTile) + radius;
                break;
            }

            return from.WithX(x);
        }

        private static SubPos ResolveY(MatchState state, SubPos from, int delta, HashSet<GridPos> ignored)
        {
            if (delta == 0)
            {
                return from;
            }

            int radius = state.Settings.PlayerRadius;
            int y = from.Y + delta;
            int edge = delta > 0 ? y + radius : y - radius;
            int row = Floor(edge);

            int firstColumn = Floor(from.X - radius);
            int lastColumn = Floor(from.X + radius);

            for (int column = firstColumn; column <= lastColumn; column++)
            {
                var tile = new GridPos(column, row);
                if (!Blocks(state, tile, ignored))
                {
                    continue;
                }

                y = delta > 0
                    ? (row * SubPos.UnitsPerTile) - radius - 1
                    : ((row + 1) * SubPos.UnitsPerTile) + radius;
                break;
            }

            return from.WithY(y);
        }

        // Pushes the player onto the open row or column when a gap is there but they are
        // not lined up with it. Without this, walking into a corridor mouth a few units
        // off centre reads as the wall being sticky, which is the single most common way
        // free movement feels broken.
        public static void AssistCorner(MatchState state, PlayerState player, int deltaX, int deltaY, int assist)
        {
            if (assist <= 0)
            {
                return;
            }

            if (deltaX != 0 && deltaY == 0)
            {
                int nudge = FreeSide(state, player, deltaX, true);
                if (nudge != 0)
                {
                    Move(state, player, 0, nudge > 0 ? Least(assist, nudge) : -Least(assist, -nudge));
                }
            }
            else if (deltaY != 0 && deltaX == 0)
            {
                int nudge = FreeSide(state, player, deltaY, false);
                if (nudge != 0)
                {
                    Move(state, player, nudge > 0 ? Least(assist, nudge) : -Least(assist, -nudge), 0);
                }
            }
        }

        // Returns how far to shift across the direction of travel to find a way through,
        // or zero when the way is already clear or hopelessly blocked.
        private static int FreeSide(MatchState state, PlayerState player, int delta, bool horizontal)
        {
            int radius = state.Settings.PlayerRadius;
            SubPos at = player.Position;
            HashSet<GridPos> ignored = Overlapping(state, at, radius);

            int along = horizontal ? at.X : at.Y;
            int across = horizontal ? at.Y : at.X;

            int lane = Floor(delta > 0 ? along + radius + 1 : along - radius - 1);
            int nearRow = Floor(across - radius);
            int farRow = Floor(across + radius);

            if (nearRow == farRow)
            {
                return 0;
            }

            bool nearBlocked = Blocks(state, Tile(lane, nearRow, horizontal), ignored);
            bool farBlocked = Blocks(state, Tile(lane, farRow, horizontal), ignored);

            if (nearBlocked == farBlocked)
            {
                return 0;
            }

            // Head for the middle of whichever of the two lanes is open.
            int target = nearBlocked
                ? SubPos.CentreOf(farRow)
                : SubPos.CentreOf(nearRow);

            return target - across;
        }

        private static GridPos Tile(int lane, int across, bool horizontal)
        {
            return horizontal ? new GridPos(lane, across) : new GridPos(across, lane);
        }

        // Whether any part of a body this size, centred here, is on the tile. The centre
        // tile stopped being the whole answer when movement came off the grid: a body
        // 0.7 of a tile across can be most of the way onto its neighbour.
        public static bool Covers(SubPos at, int radius, GridPos tile)
        {
            return tile.X >= Floor(at.X - radius)
                && tile.X <= Floor(at.X + radius)
                && tile.Y >= Floor(at.Y - radius)
                && tile.Y <= Floor(at.Y + radius);
        }

        private static HashSet<GridPos> Overlapping(MatchState state, SubPos at, int radius)
        {
            var inside = new HashSet<GridPos>();

            for (int row = Floor(at.Y - radius); row <= Floor(at.Y + radius); row++)
            {
                for (int column = Floor(at.X - radius); column <= Floor(at.X + radius); column++)
                {
                    inside.Add(new GridPos(column, row));
                }
            }

            return inside;
        }

        private static bool Blocks(MatchState state, GridPos tile, HashSet<GridPos> ignored)
        {
            if (!state.Arena.Contains(tile))
            {
                return true;
            }

            if (ignored.Contains(tile))
            {
                return false;
            }

            return Tiles.BlocksMovement(state.Arena[tile]) || state.HasBombAt(tile);
        }

        private static int Floor(int value)
        {
            int quotient = value / SubPos.UnitsPerTile;
            return value % SubPos.UnitsPerTile != 0 && value < 0 ? quotient - 1 : quotient;
        }

        private static int Least(int a, int b)
        {
            return a < b ? a : b;
        }
    }
}
