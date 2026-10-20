using System;
using System.Globalization;

namespace Blastlands.Core.Lobby
{
    public enum RegistrationOutcome : byte
    {
        Registered = 0,
        Retry = 1,
        ClientTooOld = 2,
        Rejected = 3
    }

    public static class RegistrationPolicy
    {
        public const int MaxRetrySeconds = 30;

        public static RegistrationOutcome Classify(long status)
        {
            switch (status)
            {
                case 201:
                    return RegistrationOutcome.Registered;
                case 426:
                    return RegistrationOutcome.ClientTooOld;
                case 400:
                    return RegistrationOutcome.Rejected;
                default:
                    return RegistrationOutcome.Retry;
            }
        }

        public static bool LobbyForgotUs(long status)
        {
            return status == 401;
        }

        public static string Body(int firstPort, int slots)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{{\"port\":{0},\"slots\":{1},\"listed\":true}}",
                firstPort,
                slots);
        }

        public static int RetrySeconds(int attempt)
        {
            if (attempt < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(attempt));
            }

            return attempt >= 5 ? MaxRetrySeconds : Math.Min(MaxRetrySeconds, 1 << attempt);
        }
    }
}
