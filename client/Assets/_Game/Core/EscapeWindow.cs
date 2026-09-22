using System;
using System.Collections.Generic;

namespace Blastlands.Core
{
    public sealed class EscapeWindow
    {
        private static readonly GridPos[] Neighbours =
        {
            new GridPos(1, 0),
            new GridPos(0, 1),
            new GridPos(-1, 0),
            new GridPos(0, -1)
        };

        private readonly Dictionary<GridPos, int> latestEntry;

        private EscapeWindow(Dictionary<GridPos, int> latestEntry)
        {
            this.latestEntry = latestEntry;
        }

        public static EscapeWindow From(
            MatchState state, BlastMap blast, int ticksPerTile, Func<GridPos, bool> walkable)
        {
            var latest = new Dictionary<GridPos, int>();
            var queue = new Queue<GridPos>();

            for (int y = 0; y < state.Arena.Height; y++)
            {
                for (int x = 0; x < state.Arena.Width; x++)
                {
                    var tile = new GridPos(x, y);
                    if (blast.TicksUntilFire(tile) == BlastMap.Never && walkable(tile))
                    {
                        latest[tile] = int.MaxValue;
                        queue.Enqueue(tile);
                    }
                }
            }

            while (queue.Count > 0)
            {
                GridPos tile = queue.Dequeue();
                int onward = latest[tile];

                for (int i = 0; i < Neighbours.Length; i++)
                {
                    GridPos next = tile.Offset(Neighbours[i].X, Neighbours[i].Y);
                    int fire = blast.TicksUntilFire(next);
                    if (fire == BlastMap.Never || !walkable(next))
                    {
                        continue;
                    }

                    int entry = fire - ticksPerTile - 1;
                    if (onward != int.MaxValue)
                    {
                        entry = Math.Min(entry, onward - ticksPerTile);
                    }

                    int known;
                    if (entry >= 0 && (!latest.TryGetValue(next, out known) || entry > known))
                    {
                        latest[next] = entry;
                        queue.Enqueue(next);
                    }
                }
            }

            return new EscapeWindow(latest);
        }

        public bool TryLatestEntry(GridPos tile, out int latest)
        {
            return latestEntry.TryGetValue(tile, out latest);
        }

        public bool Allows(GridPos tile, int arrival)
        {
            int latest;
            return TryLatestEntry(tile, out latest) && arrival <= latest;
        }
    }
}
