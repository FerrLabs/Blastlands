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
                GameModeTokens.TryRead(PlayerPrefs.GetString(Key, string.Empty), out GameMode picked);
                return picked;
            }
        }

        public static void Choose(GameMode mode)
        {
            PlayerPrefs.SetString(Key, GameModeTokens.Write(mode) ?? string.Empty);
            PlayerPrefs.Save();
        }
    }
}
