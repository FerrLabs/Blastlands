using System.Collections.Generic;

namespace Blastlands.Core
{
    // Carves the outline of a floating island out of the arena's rectangle.
    //
    // Three steps, and the order matters. An ellipse gives the island its mass and keeps
    // it clear of the bounds. Erosion nibbles the coast so it stops reading as a drawn
    // shape. Then the whole thing is flooded from the middle and anything the flood did
    // not reach is dropped, because erosion is perfectly capable of biting through a
    // neck and leaving a second island nobody can walk to.
    //
    // The flood is what makes the shape safe to build on rather than something to check
    // afterwards and reroll: a seed that strands a player is a match nobody can win, and
    // rerolling until the dice behave is how a rare bad seed survives to production.
    //
    // Integer arithmetic throughout, like the rest of the simulation. Coordinates are
    // doubled so the centre of an even-sided arena lands on a whole number.
    public static class IslandShape
    {
        private static readonly GridPos[] Neighbours =
        {
            new GridPos(1, 0),
            new GridPos(-1, 0),
            new GridPos(0, 1),
            new GridPos(0, -1)
        };

        public static bool[] Carve(int width, int height, IslandSettings settings, DeterministicRandom random)
        {
            bool[] land = Ellipse(width, height, settings.RadiusPercent);
            Erode(land, width, height, settings.ErosionPasses, settings.ErosionPercent, random);
            KeepOnlyTheMainland(land, width, height);
            return land;
        }

        public static bool IsLand(bool[] land, int width, int height, GridPos tile)
        {
            return tile.X >= 0 && tile.X < width && tile.Y >= 0 && tile.Y < height && land[(tile.Y * width) + tile.X];
        }

        private static bool[] Ellipse(int width, int height, int radiusPercent)
        {
            var land = new bool[width * height];

            long spanX = width - 1;
            long spanY = height - 1;
            long radiusX = spanX * radiusPercent;
            long radiusY = spanY * radiusPercent;
            long limit = radiusX * radiusX * radiusY * radiusY;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    long dx = ((2L * x) - spanX) * 100L;
                    long dy = ((2L * y) - spanY) * 100L;

                    long value = (dx * dx * radiusY * radiusY) + (dy * dy * radiusX * radiusX);
                    land[(y * width) + x] = value <= limit;
                }
            }

            return land;
        }

        // Each pass reads a snapshot rather than the live map, so a tile eroded this pass
        // does not expose its neighbour to erosion until the next one. Without that the
        // sweep order becomes visible in the result: coasts retreat much further on the
        // side the loop happens to start from.
        private static void Erode(
            bool[] land, int width, int height, int passes, int percent, DeterministicRandom random)
        {
            for (int pass = 0; pass < passes; pass++)
            {
                var before = (bool[])land.Clone();

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var tile = new GridPos(x, y);
                        if (!IsLand(before, width, height, tile) || !TouchesWater(before, width, height, tile))
                        {
                            continue;
                        }

                        if (random.NextInt(100) < percent)
                        {
                            land[(y * width) + x] = false;
                        }
                    }
                }
            }
        }

        private static bool TouchesWater(bool[] land, int width, int height, GridPos tile)
        {
            for (int i = 0; i < Neighbours.Length; i++)
            {
                if (!IsLand(land, width, height, tile.Offset(Neighbours[i].X, Neighbours[i].Y)))
                {
                    return true;
                }
            }

            return false;
        }

        // Flooded from the land tile nearest the centre rather than from an arbitrary
        // one, so the piece that survives is the body of the island and not whichever
        // fragment happened to come first in the sweep.
        private static void KeepOnlyTheMainland(bool[] land, int width, int height)
        {
            GridPos start = NearestLandToCentre(land, width, height);
            if (start.X < 0)
            {
                return;
            }

            var reached = new bool[width * height];
            var queue = new Queue<GridPos>();
            reached[(start.Y * width) + start.X] = true;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                GridPos current = queue.Dequeue();

                for (int i = 0; i < Neighbours.Length; i++)
                {
                    GridPos next = current.Offset(Neighbours[i].X, Neighbours[i].Y);
                    if (!IsLand(land, width, height, next))
                    {
                        continue;
                    }

                    int index = (next.Y * width) + next.X;
                    if (reached[index])
                    {
                        continue;
                    }

                    reached[index] = true;
                    queue.Enqueue(next);
                }
            }

            for (int i = 0; i < land.Length; i++)
            {
                land[i] = land[i] && reached[i];
            }
        }

        private static GridPos NearestLandToCentre(bool[] land, int width, int height)
        {
            var best = new GridPos(-1, -1);
            long bestDistance = long.MaxValue;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!land[(y * width) + x])
                    {
                        continue;
                    }

                    long dx = (2L * x) - (width - 1);
                    long dy = (2L * y) - (height - 1);
                    long distance = (dx * dx) + (dy * dy);

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = new GridPos(x, y);
                    }
                }
            }

            return best;
        }
    }
}
