using Blastlands.Core.Lobby;
using UnityEngine;

namespace Blastlands.Runtime
{
    public static class NameChoice
    {
        private const string Key = "blastlands.name";

        public static string Saved
        {
            get
            {
                string saved = PlayerPrefs.GetString(Key, string.Empty);
                return DisplayName.IsAcceptable(saved) ? DisplayName.Clean(saved) : string.Empty;
            }
        }

        public static void Remember(string name)
        {
            PlayerPrefs.SetString(Key, name ?? string.Empty);
            PlayerPrefs.Save();
        }
    }
}
