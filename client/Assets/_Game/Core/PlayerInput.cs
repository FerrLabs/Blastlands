namespace Blastlands.Core
{
    // What one player wants to do during one tick. A human socket and a bot produce
    // this identically, which is what stops the simulation from telling them apart.
    //
    // Movement is a vector rather than one of four directions, because free positions
    // without diagonals catch on every corner: there is nothing to slide with.
    // Components run to StickReader.Range and are clamped there.
    public readonly struct PlayerInput
    {
        public PlayerInput(Direction move, bool dropBomb)
            : this(move, dropBomb, false)
        {
        }

        public PlayerInput(Direction move, bool dropBomb, bool dash)
            : this(
                Directions.Delta(move).X * StickReader.Range,
                Directions.Delta(move).Y * StickReader.Range,
                dropBomb,
                dash)
        {
        }

        public PlayerInput(int moveX, int moveY, bool dropBomb, bool dash)
            : this(moveX, moveY, dropBomb, dash, false)
        {
        }

        public PlayerInput(int moveX, int moveY, bool dropBomb, bool dash, bool push)
            : this(moveX, moveY, dropBomb, dash, push, false)
        {
        }

        public PlayerInput(int moveX, int moveY, bool dropBomb, bool dash, bool push, bool ability)
        {
            MoveX = Clamp(moveX);
            MoveY = Clamp(moveY);
            DropBomb = dropBomb;
            Dash = dash;
            Push = push;
            Ability = ability;
        }

        public int MoveX { get; }

        public int MoveY { get; }

        public bool DropBomb { get; }

        public bool Dash { get; }

        public bool Push { get; }

        public bool Ability { get; }

        public bool IsMoving
        {
            get { return MoveX != 0 || MoveY != 0; }
        }

        // The dominant direction, for the things that still think in four ways: which
        // way a character faces, and where a standing dash goes.
        public Direction Move
        {
            get { return StickReader.ToDirection(MoveX, MoveY, 1); }
        }

        public static PlayerInput None
        {
            get { return new PlayerInput(0, 0, false, false); }
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

        public static PlayerInput Pushing(Direction facing)
        {
            GridPos delta = Directions.Delta(facing);
            return new PlayerInput(
                delta.X * StickReader.Range, delta.Y * StickReader.Range, false, false, true);
        }

        public static PlayerInput UsingAbility()
        {
            return new PlayerInput(0, 0, false, false, false, true);
        }

        private static int Clamp(int value)
        {
            if (value > StickReader.Range)
            {
                return StickReader.Range;
            }

            return value < -StickReader.Range ? -StickReader.Range : value;
        }
    }
}
