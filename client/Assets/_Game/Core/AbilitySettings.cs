namespace Blastlands.Core
{
    public readonly struct AbilitySettings
    {
        public AbilitySettings(
            int triggerCooldownTicks,
            int vanishTicks,
            int vanishCooldownTicks,
            int throwRange,
            int throwCooldownTicks,
            int wallTicks,
            int wallCooldownTicks)
        {
            TriggerCooldownTicks = triggerCooldownTicks;
            VanishTicks = vanishTicks;
            VanishCooldownTicks = vanishCooldownTicks;
            ThrowRange = throwRange;
            ThrowCooldownTicks = throwCooldownTicks;
            WallTicks = wallTicks;
            WallCooldownTicks = wallCooldownTicks;
        }

        public int TriggerCooldownTicks { get; }

        public int VanishTicks { get; }

        public int VanishCooldownTicks { get; }

        public int ThrowRange { get; }

        public int ThrowCooldownTicks { get; }

        public int WallTicks { get; }

        public int WallCooldownTicks { get; }

        public int CooldownFor(CharacterKind character)
        {
            switch (character)
            {
                case CharacterKind.Demolisher:
                    return TriggerCooldownTicks;
                case CharacterKind.Runner:
                    return VanishCooldownTicks;
                case CharacterKind.Grenadier:
                    return ThrowCooldownTicks;
                case CharacterKind.Sapper:
                    return WallCooldownTicks;
                default:
                    return 0;
            }
        }

        public static AbilitySettings Default
        {
            get { return new AbilitySettings(180, 60, 300, 3, 150, 240, 300); }
        }
    }
}
