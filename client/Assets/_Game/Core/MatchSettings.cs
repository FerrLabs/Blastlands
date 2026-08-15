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
            int bombRespawnTicks,
            int wallRegrowTicks,
            int wallTelegraphTicks,
            int wallRetryTicks,
            int dashSpeed,
            int dashTicks,
            int dashCooldownTicks)
        {
            DashSpeed = dashSpeed;
            DashTicks = dashTicks;
            DashCooldownTicks = dashCooldownTicks;
            WallRegrowTicks = wallRegrowTicks;
            WallTelegraphTicks = wallTelegraphTicks;
            WallRetryTicks = wallRetryTicks;
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

        // How long a destroyed block stays gone, and how much of that time the floor
        // shows what is coming. The telegraph is not decoration: without it a closing
        // wall is an arbitrary shove, and with it the tile is somewhere not to linger.
        public int WallRegrowTicks { get; }

        public int WallTelegraphTicks { get; }

        // How long to wait before trying again when the tile is occupied by something a
        // wall must not close over.
        public int WallRetryTicks { get; }

        // Sub-units per tick while dashing, how long the burst lasts, and the recharge.
        // A dash is speed and nothing else: it collides with everything a walking player
        // collides with, and burns in a fire the same way.
        public int DashSpeed { get; }

        public int DashTicks { get; }

        public int DashCooldownTicks { get; }

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
            // Walls come back after 20 s, telegraphed for the last 3. Measured, solo
            // Hard bot over 40 seeds, against how much of the arena stays walkable in a
            // four-bot match:
            //
            //    12 s   21 survive   46% open
            //    20 s   30 survive   48% open   <- here
            //    30 s   29 survive   53% open
            //    40 s   28 survive   57% open
            //
            // Faster than 20 s is the only setting that really bites, and it buys almost
            // no extra pressure for it: the arena is barely tighter and a third of the
            // survivors are gone.
            // Dash covers about two and a half tiles in a third of a second, once every
            // three seconds.
            get { return new MatchSettings(30, 75, 15, 26, 6, 4, 1, 1, 2, 6, 8, 35, 4, 90, 600, 90, 45, 78, 8, 90); }
        }

        public int SpeedFor(int speedSteps)
        {
            int steps = speedSteps < 0 ? 0 : (speedSteps > MaxSpeedSteps ? MaxSpeedSteps : speedSteps);
            return BaseSpeed + (steps * SpeedStep);
        }
    }
}
