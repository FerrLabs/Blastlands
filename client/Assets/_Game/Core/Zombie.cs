namespace Blastlands.Core
{
    public sealed class Zombie
    {
        public Zombie(int id, SubPos position)
        {
            Id = id;
            Position = position;
        }

        public int Id { get; }

        public SubPos Position { get; set; }

        public Direction Facing { get; set; }

        public int ChewTicks { get; set; }

        public GridPos Tile
        {
            get { return Position.Tile; }
        }
    }
}
