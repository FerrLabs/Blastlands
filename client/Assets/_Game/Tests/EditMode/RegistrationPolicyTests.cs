using Blastlands.Core.Lobby;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class RegistrationPolicyTests
    {
        [Test]
        public void OnlyA201IsARegistration()
        {
            Assert.That(RegistrationPolicy.Classify(201), Is.EqualTo(RegistrationOutcome.Registered));
            Assert.That(RegistrationPolicy.Classify(200), Is.Not.EqualTo(RegistrationOutcome.Registered));
            Assert.That(RegistrationPolicy.Classify(204), Is.Not.EqualTo(RegistrationOutcome.Registered));
        }

        [Test]
        public void ATooOldBuildAndARefusedRequestStopTheServer()
        {
            Assert.That(RegistrationPolicy.Classify(426), Is.EqualTo(RegistrationOutcome.ClientTooOld));
            Assert.That(RegistrationPolicy.Classify(400), Is.EqualTo(RegistrationOutcome.Rejected));
        }

        [Test]
        public void RateLimitingServerErrorsAndNoAnswerAreRetried()
        {
            foreach (long status in new long[] { 0, 429, 500, 502, 503, 504, 401, 404 })
            {
                Assert.That(RegistrationPolicy.Classify(status), Is.EqualTo(RegistrationOutcome.Retry), status.ToString());
            }
        }

        [Test]
        public void OnlyA401MeansTheLobbyForgotUs()
        {
            Assert.That(RegistrationPolicy.LobbyForgotUs(401), Is.True);

            foreach (long status in new long[] { 0, 200, 204, 403, 404, 429, 500 })
            {
                Assert.That(RegistrationPolicy.LobbyForgotUs(status), Is.False, status.ToString());
            }
        }

        [Test]
        public void TheBodyIsTheJsonTheLobbyExpects()
        {
            Assert.That(RegistrationPolicy.Body(7777, 3), Is.EqualTo("{\"port\":7777,\"slots\":3,\"listed\":true}"));
        }

        [Test]
        public void TheRetryDelayDoublesThenStopsAtTheCeiling()
        {
            Assert.That(RegistrationPolicy.RetrySeconds(0), Is.EqualTo(1));
            Assert.That(RegistrationPolicy.RetrySeconds(1), Is.EqualTo(2));
            Assert.That(RegistrationPolicy.RetrySeconds(2), Is.EqualTo(4));
            Assert.That(RegistrationPolicy.RetrySeconds(4), Is.EqualTo(16));
            Assert.That(RegistrationPolicy.RetrySeconds(5), Is.EqualTo(RegistrationPolicy.MaxRetrySeconds));
            Assert.That(RegistrationPolicy.RetrySeconds(1000), Is.EqualTo(RegistrationPolicy.MaxRetrySeconds));
        }

        [Test]
        public void ANegativeAttemptIsRefusedRatherThanShiftedIntoNonsense()
        {
            TestDelegate negative = () => RegistrationPolicy.RetrySeconds(-1);

            Assert.Throws<System.ArgumentOutOfRangeException>(negative);
        }
    }
}
