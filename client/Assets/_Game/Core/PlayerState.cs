namespace Blastlands.Core
{
    public sealed class PlayerState
    {
        public PlayerState(int id, SubPos position, MatchSettings settings)
        {
            Id = id;
            Position = position;
            Alive = true;
            BombsHeld = settings.StartingHeldBombs;
            CarryCapacity = settings.StartingCarryCapacity;
            FireRange = settings.StartingFireRange;
            SpeedSteps = 0;
            NextBombKind = BombKind.Standard;
            Facing = Direction.Down;
        }

        public int Id { get; }

        public SubPos Position { get; set; }

        public bool Alive { get; set; }

        // Spent on placement and never returned. Running out is the normal state of
        // affairs, not an edge case: it is what sends a player back into the open.
        public int BombsHeld { get; set; }

        public int CarryCapacity { get; set; }

        public int FireRange { get; set; }

        public int SpeedSteps { get; set; }

        public BombKind NextBombKind { get; set; }

        public Direction Facing { get; set; }

        // A dash is committed: once it starts it runs its length in the direction it
        // started in, whatever the player does next. That commitment is the risk that
        // pays for the speed — it is possible to dash into a blast.
        public Direction DashDirection { get; set; }

        public int DashTicksRemaining { get; set; }

        public int DashCooldownRemaining { get; set; }

        public bool Dashing
        {
            get { return DashTicksRemaining > 0; }
        }

        public bool CanDash
        {
            get { return Alive && DashTicksRemaining <= 0 && DashCooldownRemaining <= 0; }
        }

        public GridPos Tile
        {
            get { return Position.Tile; }
        }

        public bool CanDropBomb
        {
            get { return Alive && BombsHeld > 0; }
        }

        public bool CanCarryMore
        {
            get { return BombsHeld < CarryCapacity; }
        }
    }
}
