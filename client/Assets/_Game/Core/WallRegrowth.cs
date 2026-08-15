namespace Blastlands.Core
{
    // A soft block on its way back. The tile is walkable for the whole countdown, which
    // is the point: the arena keeps changing shape and routes close behind you.
    public sealed class WallRegrowth
    {
        public WallRegrowth(GridPos tile, TileKind kind, int ticksRemaining)
        {
            Tile = tile;
            Kind = kind;
            TicksRemaining = ticksRemaining;
        }

        public GridPos Tile { get; }

        // What stood here, so it comes back as itself. Without this a bush blown up
        // returns as a wall, and the arena quietly loses every hiding place it had.
        public TileKind Kind { get; }

        public int TicksRemaining { get; set; }
    }
}
