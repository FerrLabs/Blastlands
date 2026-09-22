using System;

namespace Blastlands.Core.Net
{
    public sealed class InputRateGate
    {
        public const int DefaultBurst = InputBuffer.DefaultCapacity;
        public const int DefaultRefillPerTick = 2;
        public const int DefaultPatienceTicks = 30;

        private readonly int burst;
        private readonly int refillPerTick;
        private readonly int patienceTicks;
        private readonly int[] tokens;
        private readonly bool[] overflowed;
        private readonly int[] ticksOver;

        public InputRateGate(int seats)
            : this(seats, DefaultBurst, DefaultRefillPerTick, DefaultPatienceTicks)
        {
        }

        public InputRateGate(int seats, int burst, int refillPerTick, int patienceTicks)
        {
            this.burst = burst < 1 ? 1 : burst;
            this.refillPerTick = refillPerTick < 1 ? 1 : refillPerTick;
            this.patienceTicks = patienceTicks < 1 ? 1 : patienceTicks;
            tokens = new int[seats];
            overflowed = new bool[seats];
            ticksOver = new int[seats];

            for (int seat = 0; seat < seats; seat++)
            {
                Reset(seat);
            }
        }

        public bool Admit(int seat)
        {
            if (tokens[seat] > 0)
            {
                tokens[seat]--;
                return true;
            }

            overflowed[seat] = true;
            return false;
        }

        public bool EndTicks(int seat, int ticks)
        {
            if (ticks < 1)
            {
                return false;
            }

            ticksOver[seat] = overflowed[seat] ? Math.Min(ticksOver[seat] + ticks, patienceTicks) : 0;
            overflowed[seat] = false;

            long refilled = tokens[seat] + ((long)refillPerTick * ticks);
            tokens[seat] = refilled > burst ? burst : (int)refilled;

            return ticksOver[seat] >= patienceTicks;
        }

        public void Reset(int seat)
        {
            tokens[seat] = burst;
            overflowed[seat] = false;
            ticksOver[seat] = 0;
        }
    }
}
