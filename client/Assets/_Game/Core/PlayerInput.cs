namespace Blastlands.Core
{
    // What one player wants to do during one tick. A human socket and a bot produce
    // this identically, which is what stops the simulation from telling them apart.
    public readonly struct PlayerInput
    {
        public PlayerInput(Direction move, bool dropBomb)
            : this(move, dropBomb, false)
        {
        }

        public PlayerInput(Direction move, bool dropBomb, bool dash)
        {
            Move = move;
            DropBomb = dropBomb;
            Dash = dash;
        }

        public Direction Move { get; }

        public bool DropBomb { get; }

        public bool Dash { get; }

        public static PlayerInput None
        {
            get { return new PlayerInput(Direction.None, false, false); }
        }

        public static PlayerInput Moving(Direction move)
        {
            return new PlayerInput(move, false, false);
        }

        public static PlayerInput Dropping()
        {
            return new PlayerInput(Direction.None, true, false);
        }

        public static PlayerInput Dashing(Direction move)
        {
            return new PlayerInput(move, false, true);
        }
    }
}
