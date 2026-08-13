using System.Collections.Generic;

namespace Blastlands.Core
{
    public sealed class ExplosionResult
    {
        public ExplosionResult(
            IReadOnlyList<GridPos> flameTiles,
            IReadOnlyList<GridPos> destroyedSoftBlocks,
            IReadOnlyList<int> detonatedBombs)
        {
            FlameTiles = flameTiles;
            DestroyedSoftBlocks = destroyedSoftBlocks;
            DetonatedBombs = detonatedBombs;
        }

        public IReadOnlyList<GridPos> FlameTiles { get; }

        public IReadOnlyList<GridPos> DestroyedSoftBlocks { get; }

        public IReadOnlyList<int> DetonatedBombs { get; }
    }
}
