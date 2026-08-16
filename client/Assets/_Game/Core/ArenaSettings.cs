using System;

namespace Blastlands.Core
{
    public readonly struct ArenaSettings
    {
        public ArenaSettings(int width, int height, int softBlockPercent)
            : this(width, height, softBlockPercent, 35, IslandSettings.Default)
        {
        }

        public ArenaSettings(int width, int height, int softBlockPercent, int bushPercent)
            : this(width, height, softBlockPercent, bushPercent, IslandSettings.Default)
        {
        }

        public ArenaSettings(
            int width, int height, int softBlockPercent, int bushPercent, IslandSettings island)
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
            BushPercent = bushPercent < 0 ? 0 : (bushPercent > 100 ? 100 : bushPercent);
            Island = island;
        }

        public int Width { get; }

        public int Height { get; }

        public int SoftBlockPercent { get; }

        // Share of the breakable tiles that come up as bushes rather than walls. Cover
        // you stand in against cover you stand behind: too few and hiding is not an
        // option anybody plans around, too many and nothing blocks a line any more.
        public int BushPercent { get; }

        // The outline carved out of the rectangle. The dimensions are still the bounds
        // the island is cut from, not the island itself.
        public IslandSettings Island { get; }

        public static ArenaSettings Default
        {
            // 25x21, up from the 15x13 a bomberman inherits by convention. A camera that
            // follows a player is pointless on a board narrower than its own view, and at
            // 16:9 the old arena was exactly that. Everything tuned before this — bomb
            // supply, wall regrowth, every bot survival figure — was fitted at the old
            // size and had to be measured again rather than assumed to carry over.
            get { return new ArenaSettings(25, 21, 75); }
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
