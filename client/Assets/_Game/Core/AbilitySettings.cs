namespace Blastlands.Core
{
    public readonly struct AbilitySettings
    {
        public AbilitySettings(int triggerCooldownTicks, int vanishTicks, int vanishCooldownTicks)
        {
            TriggerCooldownTicks = triggerCooldownTicks;
            VanishTicks = vanishTicks;
            VanishCooldownTicks = vanishCooldownTicks;
        }

        public int TriggerCooldownTicks { get; }

        public int VanishTicks { get; }

        public int VanishCooldownTicks { get; }

        public int CooldownFor(CharacterKind character)
        {
            switch (character)
            {
                case CharacterKind.Demolisher:
                    return TriggerCooldownTicks;
                case CharacterKind.Runner:
                    return VanishCooldownTicks;
                default:
                    return 0;
            }
        }

        public static AbilitySettings Default
        {
            get { return new AbilitySettings(180, 60, 300); }
        }
    }
}
