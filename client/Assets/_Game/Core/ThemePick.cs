namespace Blastlands.Core
{
    public static class ThemePick
    {
        public static int For(string matchId, int themes)
        {
            if (themes <= 1 || string.IsNullOrEmpty(matchId))
            {
                return 0;
            }

            uint hash = 2166136261;
            foreach (char c in matchId)
            {
                hash = (hash ^ c) * 16777619;
            }

            return (int)(hash % (uint)themes);
        }
    }
}
