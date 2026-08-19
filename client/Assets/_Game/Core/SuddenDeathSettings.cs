namespace Blastlands.Core
{
    // Grouped rather than added to MatchSettings one field at a time, for the reason
    // PushSettings gives: that constructor is already twenty-four positional ints of
    // the same type.
    public readonly struct SuddenDeathSettings
    {
        public SuddenDeathSettings(int startTicks, int ringTicks)
        {
            StartTicks = startTicks;
            RingTicks = ringTicks;
        }

        // How long the round runs untouched before the coast starts closing.
        public int StartTicks { get; }

        // Ticks between one ring closing and the next. Zero or less turns sudden death
        // off entirely, which is what the tests that care about something else use.
        public int RingTicks { get; }

        public bool Enabled
        {
            get { return RingTicks > 0; }
        }

        public static SuddenDeathSettings Default
        {
            // Ninety seconds, then a ring every five. The island runs about nine rings
            // deep, so the board is gone within a minute of the first ring and no match
            // outlives two minutes.
            //
            // The cadence is set on draws rather than on length. A draw here is both
            // survivors losing their ground to the same ring, so the question is whether
            // closing slower separates them. Measured over twenty-four four-bot matches,
            // all of which end at every setting:
            //
            //    4 s   3 draws   median 94 s   slowest 110 s
            //    5 s   2 draws   median 95 s   slowest 109 s   <- here
            //    6 s   4 draws   median 96 s   slowest 114 s
            //
            // Five is the best of the three on both counts, but the honest reading is
            // that cadence barely matters: draws sit near a tenth whatever it is set to,
            // and two against four over twenty-four matches is not a cliff. An earlier
            // pass over twelve seeds put six seconds at zero draws and four at one, the
            // opposite order, which is how much noise there is at that sample size.
            get { return new SuddenDeathSettings(2700, 150); }
        }

        public static SuddenDeathSettings Off
        {
            get { return new SuddenDeathSettings(0, 0); }
        }
    }
}
