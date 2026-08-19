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
            // Ninety seconds, then a ring every four. The island runs about nine rings
            // deep, so the board is gone within forty seconds of the first ring and no
            // match outlives two minutes.
            //
            // The cadence is set on draws rather than on length. A draw here is both
            // survivors losing their tile to the same ring, so closing faster produces
            // more of them. Measured over twelve four-bot matches:
            //
            //    3 s   2 draws   median  96 s   slowest 108 s
            //    4 s   1 draw    median  98 s   slowest 110 s   <- here
            //    6 s   1 draw    median 102 s   slowest 114 s
            //
            // Four seconds buys back half the draws for two seconds of round. Six buys
            // nothing further and costs four more.
            get { return new SuddenDeathSettings(2700, 120); }
        }

        public static SuddenDeathSettings Off
        {
            get { return new SuddenDeathSettings(0, 0); }
        }
    }
}
