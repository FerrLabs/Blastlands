using System.Collections.Generic;
using System.Text;
using Blastlands.Core.Net;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class GameTicketVerifierTests
    {
        private const string Match = "0b7f3f5a-1c2d-4e5f-8a9b-0c1d2e3f4a5b";
        private const long IssuedBefore = 1789999900;
        private const long Expiry = 1790000000;

        private const string LobbyVector =
            "v1.0b7f3f5a-1c2d-4e5f-8a9b-0c1d2e3f4a5b.427279616e.1790000000."
            + "00112233445566778899aabbccddeeff."
            + "da14ccf2b112adda120a715ca50537cba93eae34638d2fa6d62782b01784c7a7";

        private const string ForOtherMatch = "v1.11111111-2222-3333-4444-555555555555.427279616e.1790000000.00112233445566778899aabbccddeeff.5477a992464bd190be7dcb555f355a64d3c103f7c55f725ebc60ea49db3c7684";
        private const string ForSam = "v1.0b7f3f5a-1c2d-4e5f-8a9b-0c1d2e3f4a5b.53616d.1790000000.ffeeddccbbaa99887766554433221100.1f69280baadad64886fd4d679b9c58fb5b71a8736b085bebb0a0c68c2d997ca3";
        private const string SignedWithAnotherKey = "v1.0b7f3f5a-1c2d-4e5f-8a9b-0c1d2e3f4a5b.427279616e.1790000000.00112233445566778899aabbccddeeff.9b7bb9ac60a003794fc73c9217f859aad5a2f87cc9071cafd161de056f28ef2a";

        private static GameTicketVerifier Verifier()
        {
            return new GameTicketVerifier(Encoding.UTF8.GetBytes("blastlands-test-ticket-key-0123456789"), Match);
        }

        [Test]
        public void TheTicketTheLobbySignsIsAdmitted()
        {
            Assert.That(Verifier().Admit(LobbyVector, IssuedBefore, out string player, out _), Is.EqualTo(TicketVerdict.Admitted));
            Assert.That(player, Is.EqualTo("Bryan"));
        }

        [Test]
        public void ATicketIsGoodForOneConnectionOnly()
        {
            GameTicketVerifier verifier = Verifier();

            verifier.Admit(LobbyVector, IssuedBefore, out _, out _);

            Assert.That(verifier.Admit(LobbyVector, IssuedBefore + 1, out _, out _), Is.EqualTo(TicketVerdict.AlreadyUsed));
        }

        [Test]
        public void TwoPlayersOfTheSameMatchAreBothAdmitted()
        {
            GameTicketVerifier verifier = Verifier();

            Assert.That(verifier.Admit(LobbyVector, IssuedBefore, out _, out _), Is.EqualTo(TicketVerdict.Admitted));
            Assert.That(verifier.Admit(ForSam, IssuedBefore, out string player, out _), Is.EqualTo(TicketVerdict.Admitted));
            Assert.That(player, Is.EqualTo("Sam"));
        }

        [Test]
        public void ATicketReleasedByItsHolderAdmitsThemAgain()
        {
            GameTicketVerifier verifier = Verifier();

            verifier.Admit(LobbyVector, IssuedBefore, out _, out string nonce);
            verifier.Release(nonce);

            Assert.That(verifier.Admit(LobbyVector, IssuedBefore + 5, out _, out _), Is.EqualTo(TicketVerdict.Admitted));
            Assert.That(verifier.Admit(LobbyVector, IssuedBefore + 6, out _, out _), Is.EqualTo(TicketVerdict.AlreadyUsed));
        }

        [Test]
        public void AReleasedTicketStillExpires()
        {
            GameTicketVerifier verifier = Verifier();

            verifier.Admit(LobbyVector, IssuedBefore, out _, out string nonce);
            verifier.Release(nonce);

            Assert.That(verifier.Admit(LobbyVector, Expiry + 1, out _, out _), Is.EqualTo(TicketVerdict.Expired));
        }

        [Test]
        public void OnlyAnAdmittedTicketHandsBackANonce()
        {
            Assert.That(Verifier().Admit(ForOtherMatch, IssuedBefore, out _, out string refused), Is.EqualTo(TicketVerdict.OtherMatch));
            Assert.That(refused, Is.Null);
        }

        [Test]
        public void ATicketPastItsExpiryIsRefused()
        {
            Assert.That(Verifier().Admit(LobbyVector, Expiry + 1, out _, out _), Is.EqualTo(TicketVerdict.Expired));
        }

        [Test]
        public void ATicketOnItsLastSecondIsStillGood()
        {
            Assert.That(Verifier().Admit(LobbyVector, Expiry, out _, out _), Is.EqualTo(TicketVerdict.Admitted));
        }

        [Test]
        public void AValidTicketForAnotherMatchIsRefused()
        {
            Assert.That(Verifier().Admit(ForOtherMatch, IssuedBefore, out _, out _), Is.EqualTo(TicketVerdict.OtherMatch));
        }

        [Test]
        public void ATicketSignedWithAnotherKeyIsForged()
        {
            Assert.That(Verifier().Admit(SignedWithAnotherKey, IssuedBefore, out _, out _), Is.EqualTo(TicketVerdict.Forged));
        }

        [Test]
        public void ChangingAnySignedFieldBreaksTheSignature()
        {
            string[] fields = LobbyVector.Split('.');
            var tampered = new List<string>
            {
                string.Join(".", fields[0], "11111111-2222-3333-4444-555555555555", fields[2], fields[3], fields[4], fields[5]),
                string.Join(".", fields[0], fields[1], "53616d", fields[3], fields[4], fields[5]),
                string.Join(".", fields[0], fields[1], fields[2], "1890000000", fields[4], fields[5]),
                string.Join(".", fields[0], fields[1], fields[2], fields[3], "ffeeddccbbaa99887766554433221100", fields[5])
            };

            foreach (string ticket in tampered)
            {
                Assert.That(Verifier().Admit(ticket, IssuedBefore, out _, out _), Is.EqualTo(TicketVerdict.Forged), ticket);
            }
        }

        [Test]
        public void ARefusedTicketDoesNotSpendItsNonce()
        {
            GameTicketVerifier verifier = Verifier();

            verifier.Admit(LobbyVector, Expiry + 1, out _, out _);

            Assert.That(verifier.Admit(LobbyVector, IssuedBefore, out _, out _), Is.EqualTo(TicketVerdict.Admitted));
        }

        [Test]
        public void GarbageIsMalformedRatherThanAnError()
        {
            foreach (string ticket in new[] { null, "", "v1", "v2" + LobbyVector.Substring(2), LobbyVector + ".extra", LobbyVector.Substring(0, LobbyVector.Length - 1), LobbyVector.Replace("da14", "DA14"), LobbyVector.Replace("da14", "zz14") })
            {
                Assert.That(Verifier().Admit(ticket, IssuedBefore, out _, out _), Is.EqualTo(TicketVerdict.Malformed), ticket ?? "null");
            }
        }

        [Test]
        public void TheSecretIsRequiredAndLongEnough()
        {
            Assert.That(GameTicketVerifier.TryReadKey(_ => null, out _, out string missing), Is.False);
            Assert.That(missing, Does.Contain(GameTicketVerifier.SecretVariable));

            Assert.That(GameTicketVerifier.TryReadKey(_ => new string('k', GameTicketVerifier.MinKeyBytes - 1), out _, out _), Is.False);
            Assert.That(GameTicketVerifier.TryReadKey(_ => new string('k', GameTicketVerifier.MinKeyBytes), out byte[] key, out _), Is.True);
            Assert.That(key.Length, Is.EqualTo(GameTicketVerifier.MinKeyBytes));
        }
    }
}
