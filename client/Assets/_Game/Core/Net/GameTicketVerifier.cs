using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Blastlands.Core.Net
{
    public sealed class GameTicketVerifier
    {
        public const int MinKeyBytes = 32;
        public const string SecretVariable = "BLASTLANDS_TICKET_SECRET";

        private const string Format = "v1";
        private const string FormatWithCharacter = "v2";
        private const int FieldCount = 6;
        private const int SignatureBytes = 32;

        private static readonly Dictionary<string, CharacterKind> CharacterTokens =
            new Dictionary<string, CharacterKind>(StringComparer.Ordinal)
            {
                { "demolisher", CharacterKind.Demolisher },
                { "runner", CharacterKind.Runner },
                { "grenadier", CharacterKind.Grenadier },
                { "sapper", CharacterKind.Sapper }
            };

        private readonly byte[] key;
        private readonly string matchId;
        private readonly Dictionary<string, long> spent = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly List<string> lapsed = new List<string>();

        public GameTicketVerifier(byte[] key, string matchId)
        {
            if (key == null || key.Length < MinKeyBytes)
            {
                throw new ArgumentException($"a ticket key needs at least {MinKeyBytes} bytes", nameof(key));
            }

            this.key = (byte[])key.Clone();
            this.matchId = matchId ?? throw new ArgumentNullException(nameof(matchId));
        }

        public static bool TryReadKey(Func<string, string> environment, out byte[] key, out string error)
        {
            string raw = environment == null ? null : environment(SecretVariable);
            key = string.IsNullOrEmpty(raw) ? null : Encoding.UTF8.GetBytes(raw.Trim());

            if (key == null || key.Length == 0)
            {
                error = $"{SecretVariable} is required.";
                return false;
            }

            if (key.Length < MinKeyBytes)
            {
                error = $"{SecretVariable} must be at least {MinKeyBytes} bytes.";
                key = null;
                return false;
            }

            error = null;
            return true;
        }

        public TicketVerdict Admit(
            string ticket, long nowUnixSeconds, out string player, out string nonce, out CharacterKind character)
        {
            player = null;
            nonce = null;
            character = CharacterKind.None;

            if (string.IsNullOrEmpty(ticket))
            {
                return TicketVerdict.Malformed;
            }

            string[] fields = ticket.Split('.');
            int extra = ExtraFields(fields[0]);
            if (extra < 0 || fields.Length != FieldCount + extra)
            {
                return TicketVerdict.Malformed;
            }

            if (!TryHex(fields[5 + extra], out byte[] presented) || presented.Length != SignatureBytes)
            {
                return TicketVerdict.Malformed;
            }

            string signed = ticket.Substring(0, ticket.LastIndexOf('.'));
            if (!SameBytes(Sign(signed), presented))
            {
                return TicketVerdict.Forged;
            }

            if (!string.Equals(fields[1], matchId, StringComparison.OrdinalIgnoreCase))
            {
                return TicketVerdict.OtherMatch;
            }

            CharacterKind chosen = CharacterKind.None;
            if (!long.TryParse(fields[3 + extra], NumberStyles.None, CultureInfo.InvariantCulture, out long expires)
                || !TryHex(fields[2], out byte[] name)
                || (extra == 1 && !CharacterTokens.TryGetValue(fields[3], out chosen)))
            {
                return TicketVerdict.Malformed;
            }

            if (nowUnixSeconds > expires)
            {
                return TicketVerdict.Expired;
            }

            ForgetLapsed(nowUnixSeconds);

            if (spent.ContainsKey(fields[4 + extra]))
            {
                return TicketVerdict.AlreadyUsed;
            }

            nonce = fields[4 + extra];
            spent.Add(nonce, expires);
            player = Encoding.UTF8.GetString(name);
            character = chosen;
            return TicketVerdict.Admitted;
        }

        public void Release(string nonce)
        {
            if (nonce != null)
            {
                spent.Remove(nonce);
            }
        }

        private static int ExtraFields(string format)
        {
            if (string.Equals(format, Format, StringComparison.Ordinal))
            {
                return 0;
            }

            return string.Equals(format, FormatWithCharacter, StringComparison.Ordinal) ? 1 : -1;
        }

        private byte[] Sign(string payload)
        {
            using (var hmac = new HMACSHA256(key))
            {
                return hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            }
        }

        private void ForgetLapsed(long nowUnixSeconds)
        {
            lapsed.Clear();
            foreach (KeyValuePair<string, long> entry in spent)
            {
                if (entry.Value < nowUnixSeconds)
                {
                    lapsed.Add(entry.Key);
                }
            }

            foreach (string nonce in lapsed)
            {
                spent.Remove(nonce);
            }
        }

        private static bool SameBytes(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            int difference = 0;
            for (int i = 0; i < left.Length; i++)
            {
                difference |= left[i] ^ right[i];
            }

            return difference == 0;
        }

        private static bool TryHex(string text, out byte[] bytes)
        {
            bytes = null;
            if (text.Length % 2 != 0)
            {
                return false;
            }

            var result = new byte[text.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                int high = HexValue(text[i * 2]);
                int low = HexValue(text[(i * 2) + 1]);
                if (high < 0 || low < 0)
                {
                    return false;
                }

                result[i] = (byte)((high << 4) | low);
            }

            bytes = result;
            return true;
        }

        private static int HexValue(char digit)
        {
            if (digit >= '0' && digit <= '9')
            {
                return digit - '0';
            }

            if (digit >= 'a' && digit <= 'f')
            {
                return digit - 'a' + 10;
            }

            return -1;
        }
    }
}
