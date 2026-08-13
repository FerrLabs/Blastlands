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
            int startingBombs,
            int startingFireRange)
        {
            TicksPerSecond = ticksPerSecond;
            FuseTicks = fuseTicks;
            FlameTicks = flameTicks;
            BaseSpeed = baseSpeed;
            SpeedStep = speedStep;
            MaxSpeedSteps = maxSpeedSteps;
            StartingBombs = startingBombs;
            StartingFireRange = startingFireRange;
        }

        public int TicksPerSecond { get; }

        public int FuseTicks { get; }

        public int FlameTicks { get; }

        public int BaseSpeed { get; }

        public int SpeedStep { get; }

        public int MaxSpeedSteps { get; }

        public int StartingBombs { get; }

        public int StartingFireRange { get; }

        public static MatchSettings Default
        {
            get { return new MatchSettings(30, 75, 15, 26, 6, 4, 1, 2); }
        }

        public int SpeedFor(int speedSteps)
        {
            int steps = speedSteps < 0 ? 0 : (speedSteps > MaxSpeedSteps ? MaxSpeedSteps : speedSteps);
            return BaseSpeed + (steps * SpeedStep);
        }
    }
}
