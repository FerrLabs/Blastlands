using System.Text;
using Blastlands.Core.Lobby;
using Blastlands.Core.Net;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class ServerRegistrationTests
    {
        private const string Match = "0b7f3f5a-1c2d-4e5f-8a9b-0c1d2e3f4a5b";
        private const string Secret = "blastlands-test-ticket-key-0123456789";
        private const string LobbyTicket =
            "v1.0b7f3f5a-1c2d-4e5f-8a9b-0c1d2e3f4a5b.427279616e.1790000000."
            + "00112233445566778899aabbccddeeff."
            + "da14ccf2b112adda120a715ca50537cba93eae34638d2fa6d62782b01784c7a7";

        [Test]
        public void TheTicketSecretIsUsedAsItsUtf8TextNotDecodedFromHex()
        {
            string hexSecret = new string('a', 64);

            Assert.That(ServerRegistration.TryCreate("id", "key", hexSecret, out ServerRegistration registration, out string error), Is.True, error);

            Assert.That(registration.TicketKey, Is.EqualTo(Encoding.UTF8.GetBytes(hexSecret)));
            Assert.That(registration.TicketKey.Length, Is.EqualTo(64));
        }

        [Test]
        public void ATicketTheLobbySignedWithTheSecretIsAdmittedByTheVerifierBuiltFromIt()
        {
            Assert.That(ServerRegistration.TryCreate("id", "key", Secret, out ServerRegistration registration, out string error), Is.True, error);

            var verifier = new GameTicketVerifier(registration.TicketKey, Match);

            Assert.That(verifier.Admit(LobbyTicket, 1789999900, out _, out _, out _), Is.EqualTo(TicketVerdict.Admitted));
        }

        [Test]
        public void ADocumentMissingAnyFieldIsRefused()
        {
            string good = new string('b', 64);

            Assert.That(ServerRegistration.TryCreate(null, "key", good, out _, out _), Is.False);
            Assert.That(ServerRegistration.TryCreate("id", "", good, out _, out _), Is.False);
            Assert.That(ServerRegistration.TryCreate("id", "key", null, out _, out _), Is.False);
        }

        [Test]
        public void ASecretShorterThanTheVerifierAcceptsIsRefusedHereRatherThanWhenAMatchStarts()
        {
            string tooShort = new string('c', GameTicketVerifier.MinKeyBytes - 1);

            Assert.That(ServerRegistration.TryCreate("id", "key", tooShort, out _, out string error), Is.False);
            Assert.That(error, Does.Contain(GameTicketVerifier.MinKeyBytes.ToString()));
            Assert.That(
                ServerRegistration.TryCreate("id", "key", new string('c', GameTicketVerifier.MinKeyBytes), out _, out _),
                Is.True);
        }

        [Test]
        public void AKeyThatCouldNotBeSentInAHeaderIsRefused()
        {
            string good = new string('d', 64);

            foreach (string key in new[] { "a b", "a\nb", "a\r\nX-Evil: 1", "tab\there", "del\u007f" })
            {
                Assert.That(ServerRegistration.TryCreate("id", key, good, out _, out _), Is.False, key);
            }
        }
    }
}
