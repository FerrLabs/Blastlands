using System.Collections.Generic;

namespace Blastlands.Core
{
    // How many tiles each position sits from the edge of the island. The coast is 1, the
    // tiles behind it 2, and so on inward.
    //
    // Measured by flooding from the drop rather than by ring index off the bounds,
    // because the island is an eroded blob rather than a rectangle: a tile can be near
    // the middle of the grid and still be on the shore of a bay. Anything off the island
    // is 0, and so is anything outside the grid, which is why a border tile comes out
    // at 1 even when nothing next to it is Void.
    public static class CoastDistance
    {
        private static readonly GridPos[] Neighbours =
        {
            new GridPos(1, 0),
            new GridPos(-1, 0),
            new GridPos(0, 1),
            new GridPos(0, -1)
        };

        public static int[] Measure(Arena arena)
        {
            int width = arena.Width;
            int height = arena.Height;
            var distance = new int[width * height];
            var frontier = new Queue<GridPos>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var tile = new GridPos(x, y);
                    if (arena[tile] == TileKind.Void)
                    {
                        continue;
                    }

                    if (x == 0 || y == 0 || x == width - 1 || y == height - 1 || TouchesVoid(arena, tile))
                    {
                        distance[(y * width) + x] = 1;
                        frontier.Enqueue(tile);
                    }
                }
            }

            while (frontier.Count > 0)
            {
                GridPos tile = frontier.Dequeue();
                int next = distance[(tile.Y * width) + tile.X] + 1;

                for (int i = 0; i < Neighbours.Length; i++)
                {
                    GridPos neighbour = tile.Offset(Neighbours[i].X, Neighbours[i].Y);
                    if (!arena.Contains(neighbour) || arena[neighbour] == TileKind.Void)
                    {
                        continue;
                    }

                    int index = (neighbour.Y * width) + neighbour.X;
                    if (distance[index] != 0)
                    {
                        continue;
                    }

                    distance[index] = next;
                    frontier.Enqueue(neighbour);
                }
            }

            return distance;
        }

        private static bool TouchesVoid(Arena arena, GridPos tile)
        {
            for (int i = 0; i < Neighbours.Length; i++)
            {
                GridPos neighbour = tile.Offset(Neighbours[i].X, Neighbours[i].Y);
                if (arena.Contains(neighbour) && arena[neighbour] == TileKind.Void)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
