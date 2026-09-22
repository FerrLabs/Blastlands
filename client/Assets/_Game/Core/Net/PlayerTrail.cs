using System;
using System.Collections.Generic;

namespace Blastlands.Core.Net
{
    public sealed class PlayerTrail
    {
        public const int MaxGlidePerTick = SubPos.UnitsPerTile;

        private readonly int playerCount;
        private readonly int capacity;
        private readonly int[] ticks;
        private readonly SubPos[] positions;
        private int oldest;
        private int count;

        public PlayerTrail(int playerCount, int capacity)
        {
            if (playerCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerCount));
            }

            if (capacity < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            this.playerCount = playerCount;
            this.capacity = capacity;
            ticks = new int[capacity];
            positions = new SubPos[capacity * playerCount];
        }

        public int Count
        {
            get { return count; }
        }

        public void Record(int tick, IReadOnlyList<PlayerState> players)
        {
            if (players == null || players.Count != playerCount)
            {
                return;
            }

            if (count > 0 && tick <= TickAt(count - 1))
            {
                return;
            }

            int slot;
            if (count < capacity)
            {
                slot = Slot(count);
                count++;
            }
            else
            {
                slot = oldest;
                oldest = (oldest + 1) % capacity;
            }

            ticks[slot] = tick;
            for (int i = 0; i < playerCount; i++)
            {
                positions[(slot * playerCount) + i] = players[i].Position;
            }
        }

        public bool TrySample(int player, long time, out SubPos position)
        {
            position = default;
            if (count == 0 || player < 0 || player >= playerCount)
            {
                return false;
            }

            if (time <= Units(TickAt(0)))
            {
                position = PositionAt(0, player);
                return true;
            }

            int last = count - 1;
            if (time >= Units(TickAt(last)))
            {
                position = PositionAt(last, player);
                return true;
            }

            int after = 1;
            while (Units(TickAt(after)) < time)
            {
                after++;
            }

            position = Between(after - 1, after, player, time);
            return true;
        }

        private SubPos Between(int before, int after, int player, long time)
        {
            SubPos from = PositionAt(before, player);
            SubPos to = PositionAt(after, player);
            long start = Units(TickAt(before));
            long span = Units(TickAt(after)) - start;
            int gap = TickAt(after) - TickAt(before);

            if (Math.Abs(to.X - from.X) > MaxGlidePerTick * gap || Math.Abs(to.Y - from.Y) > MaxGlidePerTick * gap)
            {
                return from;
            }

            long done = time - start;
            return new SubPos(
                from.X + (int)((to.X - from.X) * done / span),
                from.Y + (int)((to.Y - from.Y) * done / span));
        }

        private static long Units(int tick)
        {
            return (long)tick * InterpolationClock.UnitsPerTick;
        }

        private int Slot(int index)
        {
            return (oldest + index) % capacity;
        }

        private int TickAt(int index)
        {
            return ticks[Slot(index)];
        }

        private SubPos PositionAt(int index, int player)
        {
            return positions[(Slot(index) * playerCount) + player];
        }
    }
}
