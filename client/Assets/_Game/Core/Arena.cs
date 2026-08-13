using System;

namespace Blastlands.Core
{
    public sealed class Arena
    {
        private readonly TileKind[] tiles;

        public Arena(int width, int height)
        {
            if (width < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "Arena width must be at least 3.");
            }

            if (height < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "Arena height must be at least 3.");
            }

            Width = width;
            Height = height;
            tiles = new TileKind[width * height];
        }

        public int Width { get; }

        public int Height { get; }

        public TileKind this[GridPos pos]
        {
            get
            {
                RequireInside(pos);
                return tiles[(pos.Y * Width) + pos.X];
            }
            set
            {
                RequireInside(pos);
                tiles[(pos.Y * Width) + pos.X] = value;
            }
        }

        public bool Contains(GridPos pos)
        {
            return pos.X >= 0 && pos.X < Width && pos.Y >= 0 && pos.Y < Height;
        }

        private void RequireInside(GridPos pos)
        {
            if (!Contains(pos))
            {
                throw new ArgumentOutOfRangeException(nameof(pos), pos, "Position is outside the arena.");
            }
        }
    }
}
