namespace Blastlands.Core
{
    // Tuning for the shove, grouped rather than added to MatchSettings one field at a
    // time. That constructor is already twenty-three positional ints and every one of
    // them is the same type, so a transposition compiles and quietly changes the game.
    public readonly struct PushSettings
    {
        public PushSettings(int reach, int speed, int ticks, int stunTicks, int cooldownTicks)
        {
            Reach = reach;
            Speed = speed;
            Ticks = ticks;
            StunTicks = stunTicks;
            CooldownTicks = cooldownTicks;
        }

        // How far in front a shove finds someone, in sub-tile units. It has to exceed a
        // tile: two players standing on adjacent tile centres are 256 apart, and a shove
        // that cannot reach them is one that only works when you are already inside
        // somebody.
        public int Reach { get; }

        // How fast and how long the target travels. Together these are the distance a
        // shove buys, which is the whole tactical value of it.
        public int Speed { get; }

        public int Ticks { get; }

        // What hitting a wall costs the target. A shove into open ground is only a
        // reposition; a shove into a wall is the punish.
        public int StunTicks { get; }

        public int CooldownTicks { get; }

        public static PushSettings Default
        {
            get { return new PushSettings(320, 62, 10, 24, 20); }
        }
    }
}
