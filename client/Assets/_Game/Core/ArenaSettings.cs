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
            : this(width, height, softBlockPercent, bushPercent, island, GameMode.Arena)
        {
        }

        public ArenaSettings(
            int width, int height, int softBlockPercent, int bushPercent, IslandSettings island, GameMode mode)
        {
            RequireLargeEnough(width, nameof(width));
            RequireLargeEnough(height, nameof(height));

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
            Mode = mode;
        }

        public int Width { get; }

        public int Height { get; }

        // Share of the open ground that comes up as cover. It is a target the clumps aim
        // for rather than a roll per tile, so the same number now buys fewer, larger
        // pieces of cover instead of an even sprinkle.
        public int SoftBlockPercent { get; }

        // Share of the breakable tiles that come up as bushes rather than walls. Cover
        // you stand in against cover you stand behind: too few and hiding is not an
        // option anybody plans around, too many and nothing blocks a line any more.
        public int BushPercent { get; }

        // The outline carved out of the rectangle. The dimensions are still the bounds
        // the island is cut from, not the island itself.
        public IslandSettings Island { get; }

        // Which board to lay out. Classic is the full rectangle with a border and a
        // pillar lattice; Arena is the eroded island. See #145.
        public GameMode Mode { get; }

        public static ArenaSettings Default
        {
            // 25x21, up from the 15x13 a bomberman inherits by convention. A camera that
            // follows a player is pointless on a board narrower than its own view, and at
            // 16:9 the old arena was exactly that. Everything tuned before this — bomb
            // supply, wall regrowth, every bot survival figure — was fitted at the old
            // size and had to be measured again rather than assumed to carry over.
            //
            // Cover fell from 75 to 35 when the pillar lattice went and the scatter became
            // clumps. Seventy-five was a maze; the number is chosen on how often anybody
            // actually dies. Twelve four-bot matches, sudden death off, deaths of 48:
            //
            //   20%   14      too open, nobody meets anybody
            //   25%   23
            //   30%   27
            //   35%   29   <- here
            //   40%   24
            //   55%   21      back towards corridors
            //
            // The optimum is a broad plateau rather than a spike, so 30 and 35 were run
            // again over twenty-four seeds to separate them: 43 deaths of 96 against 51.
            // The gap held and widened, so it is not the noise it could have been.
            get { return new ArenaSettings(25, 21, 35); }
        }

        // The board a bomberman inherits: odd so the lattice lands inside the border, and
        // packed with soft blocks, which is what makes the opening minute about digging
        // rather than about finding somebody.
        //
        // Odd is a real constraint here, unlike in Arena where nothing needs it any more:
        // on an even width the lattice would collide with the border ring.
        public static ArenaSettings Classic
        {
            get { return new ArenaSettings(25, 21, 75, 0, IslandSettings.Default, GameMode.Classic); }
        }

        // Any size at all. Arena has neither a lattice nor a border to line up with,
        // and Classic is a preset rather than something a caller dimensions itself, so
        // the odd-only rule that board does need is stated where the preset is built.
        private static void RequireLargeEnough(int value, string name)
        {
            if (value < 5)
            {
                throw new ArgumentOutOfRangeException(name, value, "Arena dimension must be at least 5.");
            }
        }
    }
}
