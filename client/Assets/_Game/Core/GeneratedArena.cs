using System.Collections.Generic;

namespace Blastlands.Core
{
    // An arena and the spawns that belong to it.
    //
    // They arrive together because on an island they cannot be worked out apart. The
    // rectangle let a caller compute spawn corners from the width and height alone and
    // trust the generator to leave them walkable; an irregular coastline decides where
    // the corners are, so asking the settings is asking the wrong thing.
    public sealed class GeneratedArena
    {
        public GeneratedArena(Arena arena, IReadOnlyList<GridPos> spawns)
        {
            Arena = arena;
            Spawns = spawns;
        }

        public Arena Arena { get; }

        public IReadOnlyList<GridPos> Spawns { get; }
    }
}
