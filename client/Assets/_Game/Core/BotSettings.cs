namespace Blastlands.Core
{
    // Difficulty is a handicap on the bot, never privileged information. A hard bot
    // sees exactly what an easy one does, tile for tile; it reacts sooner, looks
    // further, and holds on to what it saw for longer.
    public readonly struct BotSettings
    {
        public BotSettings(int reactionTicks, int lookaheadTicks, int safetyMarginTicks, int memoryTicks)
        {
            ReactionTicks = reactionTicks < 0 ? 0 : reactionTicks;
            LookaheadTicks = lookaheadTicks;
            SafetyMarginTicks = safetyMarginTicks;
            MemoryTicks = memoryTicks;
        }

        // Ticks between decisions. The bot keeps walking the way it was during them,
        // which is what makes a slow bot look hesitant rather than frozen.
        public int ReactionTicks { get; }

        // How far ahead a tile counts as dangerous. Short means walking into a blast
        // that was already obvious.
        public int LookaheadTicks { get; }

        // Slack required when fleeing, so a bot does not arrive exactly as the fire does.
        public int SafetyMarginTicks { get; }

        // How long an opponent stays where the bot last saw them. This is the difficulty
        // knob that vision created: an easy bot loses track of you the moment you break
        // line of sight, a hard one keeps hunting the place you were.
        public int MemoryTicks { get; }

        // The lookahead here is deliberately longer than it looks like it should be, and
        // that is the whole of #186. Raising it makes this bot worse, not better: it
        // reads more tiles as dangerous and flees more often, and with nine ticks between
        // decisions and two ticks of slack it commits to those flights on information
        // that has already gone stale. Normal, deciding every four ticks with twice the
        // slack, survives the same reading.
        //
        // Measured over 60 seeds of solo survival: 22 ticks scored 42 of 60 against
        // Normal's 43, which is the whole complaint. Everything from 28 to 40 lands
        // between 34 and 36, so 35 is the middle of a plateau rather than a lucky point.
        public static BotSettings Easy
        {
            get { return new BotSettings(9, 35, 2, 30); }
        }

        public static BotSettings Normal
        {
            get { return new BotSettings(4, 45, 4, 90); }
        }

        public static BotSettings Hard
        {
            get { return new BotSettings(0, 90, 6, 180); }
        }
    }
}
