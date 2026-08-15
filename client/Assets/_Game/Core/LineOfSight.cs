namespace Blastlands.Core
{
    // Whether one tile can see another across the arena. Integer Bresenham, so the
    // answer is the same on every machine.
    //
    // This is what makes cover mean something. A blast without it is a distance check,
    // and a distance check turns a placement game into a reflex game: there is no point
    // stepping behind a corner if the corner does not stop anything.
    public static class LineOfSight
    {
        public static bool Between(Arena arena, GridPos from, GridPos to)
        {
            return Between(arena, from, to, true);
        }

        // `softBlocksCover` is what separates a Pierce bomb from a Standard one: it sees
        // past the crates but not through the structure. Letting it ignore hard blocks
        // as well would mean no cover exists at all against it, which is not a bomb any
        // more, it is a rule that the arena does not apply to one player.
        public static bool Between(Arena arena, GridPos from, GridPos to, bool softBlocksCover)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;

            int stepX = dx > 0 ? 1 : (dx < 0 ? -1 : 0);
            int stepY = dy > 0 ? 1 : (dy < 0 ? -1 : 0);

            dx = dx < 0 ? -dx : dx;
            dy = dy < 0 ? -dy : dy;

            int x = from.X;
            int y = from.Y;

            if (dx >= dy)
            {
                int error = (2 * dy) - dx;
                for (int i = 0; i < dx - 1; i++)
                {
                    if (error > 0)
                    {
                        y += stepY;
                        error -= 2 * dx;
                    }

                    x += stepX;
                    error += 2 * dy;

                    if (Blocks(arena, new GridPos(x, y), softBlocksCover))
                    {
                        return false;
                    }
                }

                return true;
            }

            int errorY = (2 * dx) - dy;
            for (int i = 0; i < dy - 1; i++)
            {
                if (errorY > 0)
                {
                    x += stepX;
                    errorY -= 2 * dy;
                }

                y += stepY;
                errorY += 2 * dx;

                if (Blocks(arena, new GridPos(x, y), softBlocksCover))
                {
                    return false;
                }
            }

            return true;
        }

        // Soft blocks stop an ordinary blast as well as hard ones. They are destroyed by
        // being hit themselves, not by what is behind them, so a wall of crates still
        // shelters whatever is on the far side of it.
        private static bool Blocks(Arena arena, GridPos tile, bool softBlocksCover)
        {
            if (!arena.Contains(tile))
            {
                return true;
            }

            TileKind kind = arena[tile];
            return kind == TileKind.HardBlock || (softBlocksCover && kind == TileKind.SoftBlock);
        }
    }
}
