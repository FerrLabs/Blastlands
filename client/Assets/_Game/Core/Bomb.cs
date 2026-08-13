using System;

namespace Blastlands.Core
{
    public readonly struct Bomb
    {
        public Bomb(GridPos position, int ownerId, int fireRange)
        {
            if (fireRange < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(fireRange), fireRange, "Fire range must be at least 1.");
            }

            Position = position;
            OwnerId = ownerId;
            FireRange = fireRange;
        }

        public GridPos Position { get; }

        public int OwnerId { get; }

        public int FireRange { get; }
    }
}
