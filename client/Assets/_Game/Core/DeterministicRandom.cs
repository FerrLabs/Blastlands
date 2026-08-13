using System;

namespace Blastlands.Core
{
    public sealed class DeterministicRandom
    {
        private const uint FallbackSeed = 0x9E3779B9u;

        private uint state;

        public DeterministicRandom(uint seed)
        {
            state = seed == 0u ? FallbackSeed : seed;
        }

        public uint NextUInt()
        {
            uint next = state;
            next ^= next << 13;
            next ^= next >> 17;
            next ^= next << 5;
            state = next;
            return next;
        }

        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveMax), exclusiveMax, "Bound must be positive.");
            }

            uint bound = (uint)exclusiveMax;
            uint threshold = (uint.MaxValue - bound + 1u) % bound;

            uint value;
            do
            {
                value = NextUInt();
            }
            while (value < threshold);

            return (int)(value % bound);
        }
    }
}
