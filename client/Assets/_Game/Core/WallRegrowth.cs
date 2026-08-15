namespace Blastlands.Core
{
    // A soft block on its way back. The tile is walkable for the whole countdown, which
    // is the point: the arena keeps changing shape and routes close behind you.
    public sealed class WallRegrowth
    {
        public WallRegrowth(GridPos tile, int ticksRemaining)
        {
            Tile = tile;
            TicksRemaining = ticksRemaining;
        }

        public GridPos Tile { get; }

        public int TicksRemaining { get; set; }
    }
}
