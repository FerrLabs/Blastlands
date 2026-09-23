namespace Blastlands.Core
{
    public sealed class PlayerState
    {
        public PlayerState(int id, SubPos position, MatchSettings settings)
            : this(id, position, settings, CharacterKind.None)
        {
        }

        public PlayerState(int id, SubPos position, MatchSettings settings, CharacterKind character)
        {
            Id = id;
            Character = character;
            Position = position;
            Alive = true;
            BombsHeld = settings.StartingHeldBombs;
            CarryCapacity = settings.StartingCarryCapacity;
            FireRange = settings.StartingFireRange;
            SpeedSteps = 0;
            NextBombKind = BombKind.Standard;
            Facing = Direction.Down;
            CharacterKits.Apply(this, character, settings);
        }

        public int Id { get; }

        public CharacterKind Character { get; }

        public SubPos Position { get; set; }

        public bool Alive { get; set; }

        public bool IsBot { get; set; }

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

        // Being shoved is not being hurt. A shove moves you and, if you hit something,
        // takes your controls away for a moment — bombs remain the only thing that kills.
        public Direction ShoveDirection { get; set; }

        public int ShoveTicksRemaining { get; set; }

        public int StunTicksRemaining { get; set; }

        public int PushCooldownRemaining { get; set; }

        public bool Shoved
        {
            get { return ShoveTicksRemaining > 0; }
        }

        public bool Stunned
        {
            get { return StunTicksRemaining > 0; }
        }

        public bool CanPush
        {
            get { return Alive && !Stunned && PushCooldownRemaining <= 0; }
        }

        // Ticks left of being given away by having acted. Cover hides someone who is
        // hiding; it does not hide someone dashing out of it or shoving whoever walked
        // past.
        public int RevealTicksRemaining { get; set; }

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
