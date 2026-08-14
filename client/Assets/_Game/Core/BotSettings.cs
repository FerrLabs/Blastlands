namespace Blastlands.Core
{
    // Difficulty is a handicap on the bot, never privileged information. A hard bot
    // sees exactly what an easy one does; it just reacts sooner and looks further.
    public readonly struct BotSettings
    {
        public BotSettings(int reactionTicks, int lookaheadTicks, int safetyMarginTicks)
        {
            ReactionTicks = reactionTicks < 0 ? 0 : reactionTicks;
            LookaheadTicks = lookaheadTicks;
            SafetyMarginTicks = safetyMarginTicks;
        }

        // Ticks between decisions. The bot keeps walking the way it was during them,
        // which is what makes a slow bot look hesitant rather than frozen.
        public int ReactionTicks { get; }

        // How far ahead a tile counts as dangerous. Short means walking into a blast
        // that was already obvious.
        public int LookaheadTicks { get; }

        // Slack required when fleeing, so a bot does not arrive exactly as the fire does.
        public int SafetyMarginTicks { get; }

        public static BotSettings Easy
        {
            get { return new BotSettings(9, 22, 2); }
        }

        public static BotSettings Normal
        {
            get { return new BotSettings(4, 45, 4); }
        }

        public static BotSettings Hard
        {
            get { return new BotSettings(0, 90, 6); }
        }
    }
}
