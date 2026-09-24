namespace Blastlands.Core
{
    public enum HudSize : byte
    {
        Small = 0,
        Normal = 1,
        Large = 2
    }

    public static class ClientSettings
    {
        public const int VolumeSteps = 10;
        public const int DefaultVolume = 8;

        public static int StepVolume(int from, int delta)
        {
            return Clamp(from + delta, 0, VolumeSteps);
        }

        public static float VolumeLevel(int steps)
        {
            return Clamp(steps, 0, VolumeSteps) / (float)VolumeSteps;
        }

        public static HudSize StepHud(HudSize from, int delta)
        {
            return (HudSize)Clamp((int)from + delta, (int)HudSize.Small, (int)HudSize.Large);
        }

        public static float HudFactor(HudSize size)
        {
            switch (size)
            {
                case HudSize.Small:
                    return 0.8f;
                case HudSize.Large:
                    return 1.2f;
                default:
                    return 1f;
            }
        }

        private static int Clamp(int value, int lowest, int highest)
        {
            return value < lowest ? lowest : (value > highest ? highest : value);
        }
    }
}
