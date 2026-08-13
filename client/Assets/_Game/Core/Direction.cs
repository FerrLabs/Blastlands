namespace Blastlands.Core
{
    public enum Direction : byte
    {
        None = 0,
        Right = 1,
        Left = 2,
        Down = 3,
        Up = 4
    }

    public static class Directions
    {
        public static readonly Direction[] Cardinals =
        {
            Direction.Right,
            Direction.Left,
            Direction.Down,
            Direction.Up
        };

        public static GridPos Delta(Direction direction)
        {
            switch (direction)
            {
                case Direction.Right:
                    return new GridPos(1, 0);
                case Direction.Left:
                    return new GridPos(-1, 0);
                case Direction.Down:
                    return new GridPos(0, 1);
                case Direction.Up:
                    return new GridPos(0, -1);
                default:
                    return new GridPos(0, 0);
            }
        }

        public static bool IsHorizontal(Direction direction)
        {
            return direction == Direction.Right || direction == Direction.Left;
        }
    }
}
