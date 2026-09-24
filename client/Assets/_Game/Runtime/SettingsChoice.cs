using Blastlands.Core;
using UnityEngine;

namespace Blastlands.Runtime
{
    public static class SettingsChoice
    {
        private const string VolumeKey = "blastlands.volume";
        private const string ShakeKey = "blastlands.shake";
        private const string HudKey = "blastlands.hud";
        private const string KeysKey = "blastlands.keys";

        public static int Volume
        {
            get { return ClientSettings.StepVolume(PlayerPrefs.GetInt(VolumeKey, ClientSettings.DefaultVolume), 0); }
        }

        public static bool Shake
        {
            get { return PlayerPrefs.GetInt(ShakeKey, 1) != 0; }
        }

        public static HudSize Hud
        {
            get { return ClientSettings.StepHud((HudSize)PlayerPrefs.GetInt(HudKey, (int)HudSize.Normal), 0); }
        }

        public static string Keys
        {
            get { return PlayerPrefs.GetString(KeysKey, string.Empty); }
        }

        public static void ChooseVolume(int steps)
        {
            PlayerPrefs.SetInt(VolumeKey, ClientSettings.StepVolume(steps, 0));
            PlayerPrefs.Save();
            Apply();
        }

        public static void ChooseShake(bool on)
        {
            PlayerPrefs.SetInt(ShakeKey, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void ChooseHud(HudSize size)
        {
            PlayerPrefs.SetInt(HudKey, (int)size);
            PlayerPrefs.Save();
        }

        public static void ChooseKeys(string json)
        {
            PlayerPrefs.SetString(KeysKey, json ?? string.Empty);
            PlayerPrefs.Save();
        }

        public static void Apply()
        {
            AudioListener.volume = ClientSettings.VolumeLevel(Volume);
        }
    }
}
