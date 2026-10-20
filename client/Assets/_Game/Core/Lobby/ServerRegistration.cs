using System.Text;
using Blastlands.Core.Net;

namespace Blastlands.Core.Lobby
{
    public sealed class ServerRegistration
    {
        private ServerRegistration(string id, string key, byte[] ticketKey)
        {
            Id = id;
            Key = key;
            TicketKey = ticketKey;
        }

        public string Id { get; }

        public string Key { get; }

        public byte[] TicketKey { get; }

        public static bool TryCreate(
            string id, string key, string ticketSecret, out ServerRegistration registration, out string error)
        {
            registration = null;

            if (string.IsNullOrWhiteSpace(id))
            {
                error = "the lobby's registration named no server id";
                return false;
            }

            if (string.IsNullOrEmpty(key) || HasWhitespaceOrControl(key))
            {
                error = "the lobby's registration carried no usable key";
                return false;
            }

            byte[] ticketKey = string.IsNullOrEmpty(ticketSecret) ? null : Encoding.UTF8.GetBytes(ticketSecret);
            if (ticketKey == null || ticketKey.Length < GameTicketVerifier.MinKeyBytes)
            {
                error = $"the lobby's ticket secret must be at least {GameTicketVerifier.MinKeyBytes} bytes";
                return false;
            }

            registration = new ServerRegistration(id, key, ticketKey);
            error = null;
            return true;
        }

        private static bool HasWhitespaceOrControl(string text)
        {
            foreach (char letter in text)
            {
                if (letter <= ' ' || letter == 127)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
