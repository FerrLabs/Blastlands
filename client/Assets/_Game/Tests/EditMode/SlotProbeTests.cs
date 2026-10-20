using System.Text;
using Blastlands.Core.Lobby;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class SlotProbeTests
    {
        private const string Nonce = "00112233445566778899aabbccddeeff";

        private static byte[] Bytes(string text)
        {
            return Encoding.ASCII.GetBytes(text);
        }

        [Test]
        public void ReadsTheNonceTheLobbySends()
        {
            byte[] datagram = Bytes("blastlands-probe:" + Nonce);

            Assert.That(SlotProbe.TryRead(datagram, datagram.Length, out string nonce), Is.True);
            Assert.That(nonce, Is.EqualTo(Nonce));
        }

        [Test]
        public void OnlyTheReceivedBytesOfALargerBufferCount()
        {
            byte[] buffer = new byte[256];
            byte[] datagram = Bytes("blastlands-probe:" + Nonce);
            datagram.CopyTo(buffer, 0);

            Assert.That(SlotProbe.TryRead(buffer, datagram.Length, out string nonce), Is.True);
            Assert.That(nonce, Is.EqualTo(Nonce));
        }

        [Test]
        public void AnythingElseOnThePortIsIgnored()
        {
            foreach (string stray in new[]
                     {
                         "",
                         Nonce,
                         "blastlands-probe:",
                         "blastlands-probe:" + Nonce + "00",
                         "blastlands-probe:" + Nonce.ToUpperInvariant(),
                         "blastlands-probe:0011223344556677?probe=aabbccddeeff",
                         "other-probe:00112233445566778899aabbccddeeff00000"
                     })
            {
                byte[] datagram = Bytes(stray);
                Assert.That(SlotProbe.TryRead(datagram, datagram.Length, out string nonce), Is.False, stray);
                Assert.That(nonce, Is.Null, stray);
            }
        }

        [Test]
        public void ALengthPastTheBufferIsRefused()
        {
            byte[] datagram = Bytes("blastlands-probe:" + Nonce);

            Assert.That(SlotProbe.TryRead(datagram, datagram.Length + 1, out _), Is.False);
            Assert.That(SlotProbe.TryRead(null, 0, out _), Is.False);
        }

        [Test]
        public void ThePollCarriesTheNonceOnlyOnceOneArrived()
        {
            Assert.That(SlotProbe.Query(null), Is.Empty);
            Assert.That(SlotProbe.Query(Nonce), Is.EqualTo("?probe=" + Nonce));
        }
    }
}
