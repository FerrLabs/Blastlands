using Blastlands.Core;
using UnityEngine;

namespace Blastlands.Runtime
{
    public static class ModeChoice
    {
        private const string Key = "blastlands.mode";

        public static GameMode Current
        {
            get
            {
                return GameModeTokens.TryRead(PlayerPrefs.GetString(Key, string.Empty), out GameMode picked)
                    ? picked
                    : GameModeTokens.All[0];
            }
        }

        public static void Choose(GameMode mode)
        {
            PlayerPrefs.SetString(Key, GameModeTokens.Write(mode) ?? string.Empty);
            PlayerPrefs.Save();
        }
    }
}
