namespace Blastlands.Core.Lobby
{
    public enum LocalHostStep : byte
    {
        Wait = 0,
        Create = 1,
        GiveUp = 2
    }

    public static class LocalHostDecision
    {
        public const string Loopback = "127.0.0.1";
        public const float PatienceSeconds = 30f;
        public const float FreshPollSeconds = 10f;

        public static LocalHostStep For(
            bool registered, bool registrationFailed, bool slotBusy, float secondsSinceSlotPolled, float waitedSeconds)
        {
            if (registrationFailed || slotBusy)
            {
                return LocalHostStep.GiveUp;
            }

            if (registered && secondsSinceSlotPolled <= FreshPollSeconds)
            {
                return LocalHostStep.Create;
            }

            return waitedSeconds < PatienceSeconds ? LocalHostStep.Wait : LocalHostStep.GiveUp;
        }
    }
}
