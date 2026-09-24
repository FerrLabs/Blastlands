using System;
using System.Collections.Generic;

namespace Blastlands.Core
{
    public enum BotSkill
    {
        Easy = 0,
        Normal = 1,
        Hard = 2
    }

    public static class BotSkills
    {
        private static readonly Dictionary<string, BotSkill> ByToken =
            new Dictionary<string, BotSkill>(StringComparer.Ordinal)
            {
                { "easy", BotSkill.Easy },
                { "normal", BotSkill.Normal },
                { "hard", BotSkill.Hard }
            };

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

        public static bool TryRead(string token, out BotSkill skill)
        {
            skill = BotSkill.Normal;
            return token != null && ByToken.TryGetValue(token, out skill);
        }

        public static string Write(BotSkill skill)
        {
            foreach (KeyValuePair<string, BotSkill> entry in ByToken)
            {
                if (entry.Value == skill)
                {
                    return entry.Key;
                }
            }

            return null;
        }

        public static BotSkill Step(BotSkill from, int delta)
        {
            const int count = 3;
            int next = ((int)from + delta) % count;
            return (BotSkill)(next < 0 ? next + count : next);
        }
    }
}
