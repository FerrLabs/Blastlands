using Blastlands.Core;
using UnityEngine;

namespace Blastlands.Runtime
{
    public static class CharacterChoice
    {
        private const string Key = "blastlands.character";

        public static CharacterKind Current
        {
            get
            {
                CharacterTokens.TryRead(PlayerPrefs.GetString(Key, string.Empty), out CharacterKind picked);
                return picked;
            }
        }

        public static void Choose(CharacterKind character)
        {
            PlayerPrefs.SetString(Key, CharacterTokens.Write(character) ?? string.Empty);
            PlayerPrefs.Save();
        }
    }
}
