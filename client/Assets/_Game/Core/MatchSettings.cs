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
            int tilesPerLooseBomb,
            int bombRespawnTicks,
            int wallRegrowTicks,
            int wallTelegraphTicks,
            int wallRetryTicks,
            int dashSpeed,
            int dashTicks,
            int dashCooldownTicks,
            int playerRadius,
            int cornerAssist,
            int looseBombFuseTicks,
            PushSettings push,
            VisionSettings vision,
            SuddenDeathSettings suddenDeath,
            RuleSet rules)
        {
            Rules = rules;
            SuddenDeath = suddenDeath;
            Vision = vision;
            Push = push;
            LooseBombFuseTicks = looseBombFuseTicks;
            PlayerRadius = playerRadius;
            CornerAssist = cornerAssist;
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
            TilesPerLooseBomb = tilesPerLooseBomb;
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

        // How much floor there should be per loose bomb, rather than a flat count. The
        // count was fitted against a 15x13 arena and did not survive it growing: the
        // same four bombs spread over two and a half times the ground halved how much
        // got destroyed, because everyone was walking instead of playing. Density is the
        // thing that stays true when the arena changes size.
        //
        // Zero or less turns spawning off.
        public int TilesPerLooseBomb { get; }

        public int BombRespawnTicks { get; }

        // What a bomb lying in fire gets instead of the full fuse. Short enough to read
        // as a chain reaction, long enough to be worth reacting to.
        public int LooseBombFuseTicks { get; }

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

        // Half the width of a player's body, in sub-tile units. Smaller than half a tile
        // so two bodies can pass each other in a corridor.
        public int PlayerRadius { get; }

        // How hard the body is pushed onto an open lane when it walks into the edge of
        // one. Zero makes corridor mouths sticky, which is the most common way free
        // movement feels broken.
        public int CornerAssist { get; }

        public PushSettings Push { get; }

        public VisionSettings Vision { get; }

        // What stops a round that neither survivor can win. See #105.
        public SuddenDeathSettings SuddenDeath { get; }

        // What differs between game modes. See #145.
        public RuleSet Rules { get; }

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
            // A body 0.7 of a tile across, and a corner assist of about a third of the
            // walking speed.
            // MaxFireRange dropped from 8 to 4 when blasts became discs. A cross grows
            // linearly and a disc grows with the square, so the same number means a very
            // different thing. Measured against a 15x13 arena:
            //
            //   range   disc   share of the arena   (old cross)
            //     2      13           7%                 9
            //     3      29          15%                13
            //     4      49          25%                17   <- cap
            //     8     197         101%                33
            //
            // Eight was fine as a cross. As a disc it is one bomb covering the entire
            // map, which is not an upgrade, it is the end of the round.
            get
            {
                return new MatchSettings(
                    // Eighty tiles per loose bomb, measured on the 25x21 arena. The
                    // supply controls lethality through chain reactions rather than
                    // through the bombs themselves: the more that lie around, the more
                    // often a blast lights one and the chain takes whoever set it off.
                    //
                    //   50 tiles/bomb (10 bombs)   19 of 40 survive   56 destroyed
                    //   80 tiles/bomb ( 6 bombs)   22 of 40 survive   53 destroyed  <-
                    //  130 tiles/bomb ( 4 bombs)   25 of 40 survive   39 destroyed
                    //
                    // Past eighty the arena goes quiet for very little safety in return.
                    30, 75, 15, 26, 6, 4, 1, 1, 2, 6, 4, 35, 80, 90, 600, 90, 45, 78, 8, 90, 90, 9, 12,
                    PushSettings.Default,
                    VisionSettings.Default,
                    SuddenDeathSettings.Default,
                    RuleSet.Arena);
            }
        }

        // Restating twenty-two positional ints to change one is where a transposition
        // eventually happens, so the copy lives here rather than at the call site. See
        // the wider problem in the tracking issue for this constructor.
        public MatchSettings WithRules(RuleSet rules)
        {
            return new MatchSettings(
                TicksPerSecond, FuseTicks, FlameTicks, BaseSpeed, SpeedStep, MaxSpeedSteps,
                StartingHeldBombs, StartingCarryCapacity, StartingFireRange,
                MaxCarryCapacity, MaxFireRange, PowerUpDropPercent,
                TilesPerLooseBomb, BombRespawnTicks,
                WallRegrowTicks, WallTelegraphTicks, WallRetryTicks,
                DashSpeed, DashTicks, DashCooldownTicks,
                PlayerRadius, CornerAssist, LooseBombFuseTicks, Push, Vision, SuddenDeath, rules);
        }

        // Classic holds its bombs rather than finding them, so the ground stays clear of
        // loose ones and a player starts with the whole pocketful they will ever carry.
        public static MatchSettings Classic
        {
            get
            {
                return Default
                    .WithRules(RuleSet.Classic)
                    .WithTilesPerLooseBomb(0);
            }
        }

        public static MatchSettings ClassicBlinded
        {
            get { return Classic.WithRules(RuleSet.ClassicBlinded); }
        }

        // The settings a mode runs under, in one place, so a caller picks a mode rather
        // than remembering which preset goes with which board.
        public static MatchSettings For(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Classic:
                    return Classic;
                case GameMode.ClassicBlinded:
                    return ClassicBlinded;
                default:
                    return Default;
            }
        }

        public MatchSettings WithSuddenDeath(SuddenDeathSettings suddenDeath)
        {
            return new MatchSettings(
                TicksPerSecond, FuseTicks, FlameTicks, BaseSpeed, SpeedStep, MaxSpeedSteps,
                StartingHeldBombs, StartingCarryCapacity, StartingFireRange,
                MaxCarryCapacity, MaxFireRange, PowerUpDropPercent,
                TilesPerLooseBomb, BombRespawnTicks,
                WallRegrowTicks, WallTelegraphTicks, WallRetryTicks,
                DashSpeed, DashTicks, DashCooldownTicks,
                PlayerRadius, CornerAssist, LooseBombFuseTicks, Push, Vision, suddenDeath, Rules);
        }

        public MatchSettings WithTilesPerLooseBomb(int tilesPerBomb)
        {
            return new MatchSettings(
                TicksPerSecond, FuseTicks, FlameTicks, BaseSpeed, SpeedStep, MaxSpeedSteps,
                StartingHeldBombs, StartingCarryCapacity, StartingFireRange,
                MaxCarryCapacity, MaxFireRange, PowerUpDropPercent,
                tilesPerBomb, BombRespawnTicks,
                WallRegrowTicks, WallTelegraphTicks, WallRetryTicks,
                DashSpeed, DashTicks, DashCooldownTicks,
                PlayerRadius, CornerAssist, LooseBombFuseTicks, Push, Vision, SuddenDeath, Rules);
        }

        public int SpeedFor(int speedSteps)
        {
            int steps = speedSteps < 0 ? 0 : (speedSteps > MaxSpeedSteps ? MaxSpeedSteps : speedSteps);
            return BaseSpeed + (steps * SpeedStep);
        }
    }
}
