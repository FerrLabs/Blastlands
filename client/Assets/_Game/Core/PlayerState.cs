namespace Blastlands.Core
{
    public sealed class PlayerState
    {
        public PlayerState(int id, SubPos position, MatchSettings settings)
        {
            Id = id;
            Position = position;
            Alive = true;
            BombCapacity = settings.StartingBombs;
            FireRange = settings.StartingFireRange;
            SpeedSteps = 0;
            NextBombKind = BombKind.Standard;
            Facing = Direction.Down;
        }

        public int Id { get; }

        public SubPos Position { get; set; }

        public bool Alive { get; set; }

        public int BombCapacity { get; set; }

        public int FireRange { get; set; }

        public int SpeedSteps { get; set; }

        public int BombsPlaced { get; set; }

        public BombKind NextBombKind { get; set; }

        public Direction Facing { get; set; }

        public GridPos Tile
        {
            get { return Position.Tile; }
        }

        public bool CanDropBomb
        {
            get { return Alive && BombsPlaced < BombCapacity; }
        }
    }
}
