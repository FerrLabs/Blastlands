namespace Blastlands.Core
{
    // Tuning, in ticks and sub-tile units. The simulation runs at TicksPerSecond,
    // so a fuse of 75 ticks is two and a half seconds.
    public readonly struct MatchSettings
    {
        public MatchSettings(
            int ticksPerSecond,
            int fuseTicks,
            int flameTicks,
            int baseSpeed,
            int speedStep,
            int maxSpeedSteps,
            int startingHeldBombs,
            int startingCarryCapacity,
            int startingFireRange,
            int maxCarryCapacity,
            int maxFireRange,
            int powerUpDropPercent,
            int looseBombTarget,
            int bombRespawnTicks)
        {
            TicksPerSecond = ticksPerSecond;
            FuseTicks = fuseTicks;
            FlameTicks = flameTicks;
            BaseSpeed = baseSpeed;
            SpeedStep = speedStep;
            MaxSpeedSteps = maxSpeedSteps;
            StartingHeldBombs = startingHeldBombs;
            StartingCarryCapacity = startingCarryCapacity;
            StartingFireRange = startingFireRange;
            MaxCarryCapacity = maxCarryCapacity;
            MaxFireRange = maxFireRange;
            PowerUpDropPercent = powerUpDropPercent;
            LooseBombTarget = looseBombTarget;
            BombRespawnTicks = bombRespawnTicks;
        }

        public int TicksPerSecond { get; }

        public int FuseTicks { get; }

        public int FlameTicks { get; }

        public int BaseSpeed { get; }

        public int SpeedStep { get; }

        public int MaxSpeedSteps { get; }

        // A bomb is spent when it is placed, and is not handed back when it goes off.
        // What a player carries is the whole of what they have to work with.
        public int StartingHeldBombs { get; }

        public int StartingCarryCapacity { get; }

        public int StartingFireRange { get; }

        // Caps exist so a long round does not end with someone holding a screen-wide
        // blast, or a pocketful of bombs nobody can play around.
        public int MaxCarryCapacity { get; }

        public int MaxFireRange { get; }

        public int PowerUpDropPercent { get; }

        // How many bombs the arena tries to keep lying around, and how often it looks.
        // An arena that runs dry ends as a chase with no weapons, which is a worse
        // failure than one that is slightly too generous.
        public int LooseBombTarget { get; }

        public int BombRespawnTicks { get; }

        public static MatchSettings Default
        {
            // Carry capacity starts at one. It is also how many bombs can be live at
            // once, and two live bombs is precisely what lets a player wall themselves
            // into their own blast — the failure #59 is about. BombUp raises it, so the
            // player who wants that power has to go and earn it.
            // BombRespawnTicks has to stay above FuseTicks, and the margin is not a
            // matter of taste. Below it a player finds their next bomb before the last
            // one has gone off, which is the only way to have two live at once — and
            // two live bombs is how you wall yourself into your own blast.
            //
            // Measured, solo Hard bot over 40 seeds, and blocks cleared over eight
            // four-bot matches:
            //
            //   3 / 120   36 survive   26.6 cleared
            //   4 /  90   35 survive   34.1 cleared   <- here
            //   5 /  60   11 survive   43.4 cleared
            //   6 /  45    7 survive   51.5 cleared
            //
            // It is a cliff at the fuse, not a gradient. Raising the supply buys more
            // action for a collapse in survival, so the last safe setting wins.
            get { return new MatchSettings(30, 75, 15, 26, 6, 4, 1, 1, 2, 6, 8, 35, 4, 90); }
        }

        public int SpeedFor(int speedSteps)
        {
            int steps = speedSteps < 0 ? 0 : (speedSteps > MaxSpeedSteps ? MaxSpeedSteps : speedSteps);
            return BaseSpeed + (steps * SpeedStep);
        }
    }
}
