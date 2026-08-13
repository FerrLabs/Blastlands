using System;

namespace Blastlands.Core
{
    // Fixed-point position in sub-tile units. Movement has to be smoother than one
    // tile per tick, but floats would break determinism, so a tile is divided into
    // UnitsPerTile integer steps.
    public readonly struct SubPos : IEquatable<SubPos>
    {
        public const int UnitsPerTile = 256;
        public const int TileCentre = UnitsPerTile / 2;

        public SubPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }

        public GridPos Tile
        {
            get { return new GridPos(FloorDiv(X, UnitsPerTile), FloorDiv(Y, UnitsPerTile)); }
        }

        public static SubPos AtTileCentre(GridPos tile)
        {
            return new SubPos((tile.X * UnitsPerTile) + TileCentre, (tile.Y * UnitsPerTile) + TileCentre);
        }

        public static int CentreOf(int tileCoordinate)
        {
            return (tileCoordinate * UnitsPerTile) + TileCentre;
        }

        public SubPos WithX(int x)
        {
            return new SubPos(x, Y);
        }

        public SubPos WithY(int y)
        {
            return new SubPos(X, y);
        }

        public bool Equals(SubPos other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is SubPos other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }

        public override string ToString()
        {
            return "(" + X + ", " + Y + ")";
        }

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            return value % divisor != 0 && ((value < 0) != (divisor < 0)) ? quotient - 1 : quotient;
        }
    }
}
