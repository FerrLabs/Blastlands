namespace Blastlands.Core
{
    public readonly struct AbilitySettings
    {
        public AbilitySettings(int triggerCooldownTicks)
        {
            TriggerCooldownTicks = triggerCooldownTicks;
        }

        public int TriggerCooldownTicks { get; }

        public int CooldownFor(CharacterKind character)
        {
            return character == CharacterKind.Demolisher ? TriggerCooldownTicks : 0;
        }

        public static AbilitySettings Default
        {
            get { return new AbilitySettings(180); }
        }
    }
}
