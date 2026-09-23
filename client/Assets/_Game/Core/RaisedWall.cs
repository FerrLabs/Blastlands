namespace Blastlands.Core
{
    public sealed class RaisedWall
    {
        public RaisedWall(GridPos tile, int ticksRemaining)
        {
            Tile = tile;
            TicksRemaining = ticksRemaining;
        }

        public GridPos Tile { get; }

        public int TicksRemaining { get; set; }
    }
}
