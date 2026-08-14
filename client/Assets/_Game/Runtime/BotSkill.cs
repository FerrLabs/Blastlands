using Blastlands.Core;

namespace Blastlands.Runtime
{
    public enum BotSkill
    {
        Easy = 0,
        Normal = 1,
        Hard = 2
    }

    public static class BotSkills
    {
        public static BotSettings SettingsFor(BotSkill skill)
        {
            switch (skill)
            {
                case BotSkill.Easy:
                    return BotSettings.Easy;
                case BotSkill.Hard:
                    return BotSettings.Hard;
                default:
                    return BotSettings.Normal;
            }
        }
    }
}
