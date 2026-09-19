using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Blastlands.Core.Update
{
    public static class Digest
    {
        private const int Sha256HexLength = 64;

        public static bool IsSha256(string hex)
        {
            if (hex == null || hex.Length != Sha256HexLength)
            {
                return false;
            }

            for (int i = 0; i < hex.Length; i++)
            {
                char c = hex[i];
                bool digit = c >= '0' && c <= '9';
                bool letter = c >= 'a' && c <= 'f';
                if (!digit && !letter)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool Matches(Stream content, string expectedSha256)
        {
            if (!IsSha256(expectedSha256))
            {
                return false;
            }

            using (SHA256 sha = SHA256.Create())
            {
                return ToHex(sha.ComputeHash(content)) == expectedSha256;
            }
        }

        private static string ToHex(byte[] bytes)
        {
            var hex = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                hex.Append(b.ToString("x2"));
            }

            return hex.ToString();
        }
    }
}
