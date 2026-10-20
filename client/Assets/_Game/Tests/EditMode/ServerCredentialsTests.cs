using Blastlands.Core.Lobby;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class ServerCredentialsTests
    {
        private static ServerRegistration Registration(string key)
        {
            Assert.That(ServerRegistration.TryCreate("id", key, new string('s', 64), out ServerRegistration registration, out string error), Is.True, error);
            return registration;
        }

        [Test]
        public void NothingIsReadyBeforeTheFirstRegistration()
        {
            var credentials = new ServerCredentials();

            Assert.That(credentials.Ready, Is.False);
            Assert.That(credentials.Current, Is.Null);
        }

        [Test]
        public void SlotsThatAllGetA401TogetherStartOneRenewal()
        {
            var credentials = new ServerCredentials();
            credentials.Install(Registration("old"));
            int seen = credentials.Generation;

            int renewals = 0;
            for (int slot = 0; slot < 16; slot++)
            {
                if (credentials.TryBeginRenewal(seen))
                {
                    renewals++;
                }
            }

            Assert.That(renewals, Is.EqualTo(1));
            Assert.That(credentials.Ready, Is.False, "nobody should call with the rejected key while renewing");
        }

        [Test]
        public void A401FromBeforeTheRenewalLandedDoesNotStartAnother()
        {
            var credentials = new ServerCredentials();
            credentials.Install(Registration("old"));
            int slowRequestSaw = credentials.Generation;

            Assert.That(credentials.TryBeginRenewal(credentials.Generation), Is.True);
            credentials.Install(Registration("new"));

            Assert.That(credentials.TryBeginRenewal(slowRequestSaw), Is.False);
            Assert.That(credentials.Current.Key, Is.EqualTo("new"));
            Assert.That(credentials.Ready, Is.True);
        }

        [Test]
        public void ARejectionOfTheNewKeyCanRenewAgain()
        {
            var credentials = new ServerCredentials();
            credentials.Install(Registration("one"));
            credentials.TryBeginRenewal(credentials.Generation);
            credentials.Install(Registration("two"));

            Assert.That(credentials.TryBeginRenewal(credentials.Generation), Is.True);
        }
    }
}
