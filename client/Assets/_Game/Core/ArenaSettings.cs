using System;

namespace Blastlands.Core
{
    public readonly struct ArenaSettings
    {
        public ArenaSettings(int width, int height, int softBlockPercent)
        {
            RequireOddAndLargeEnough(width, nameof(width));
            RequireOddAndLargeEnough(height, nameof(height));

            if (softBlockPercent < 0 || softBlockPercent > 100)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(softBlockPercent), softBlockPercent, "Soft block percent must be between 0 and 100.");
            }

            Width = width;
            Height = height;
            SoftBlockPercent = softBlockPercent;
        }

        public int Width { get; }

        public int Height { get; }

        public int SoftBlockPercent { get; }

        public static ArenaSettings Default
        {
            get { return new ArenaSettings(15, 13, 75); }
        }

        private static void RequireOddAndLargeEnough(int value, string name)
        {
            if (value < 5)
            {
                throw new ArgumentOutOfRangeException(name, value, "Arena dimension must be at least 5.");
            }

            if (value % 2 == 0)
            {
                throw new ArgumentOutOfRangeException(
                    name, value, "Arena dimension must be odd so the pillar lattice lands inside the border.");
            }
        }
    }
}
