using System.Text;

namespace Blastlands.Core.Lobby
{
    public static class SlotProbe
    {
        public const string Prefix = "blastlands-probe:";
        public const int NonceLength = 32;

        public static bool TryRead(byte[] datagram, int length, out string nonce)
        {
            nonce = null;
            if (datagram == null || length != Prefix.Length + NonceLength || length > datagram.Length)
            {
                return false;
            }

            for (int i = 0; i < Prefix.Length; i++)
            {
                if (datagram[i] != (byte)Prefix[i])
                {
                    return false;
                }
            }

            for (int i = Prefix.Length; i < length; i++)
            {
                byte b = datagram[i];
                if (!((b >= (byte)'0' && b <= (byte)'9') || (b >= (byte)'a' && b <= (byte)'f')))
                {
                    return false;
                }
            }

            nonce = Encoding.ASCII.GetString(datagram, Prefix.Length, NonceLength);
            return true;
        }

        public static string Query(string nonce)
        {
            return string.IsNullOrEmpty(nonce) ? string.Empty : "?probe=" + nonce;
        }
    }
}
