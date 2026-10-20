using Blastlands.Core.Lobby;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class LocalHostDecisionTests
    {
        private const float Never = float.PositiveInfinity;

        [Test]
        public void CreatesOnceRegisteredAndTheSlotHasJustPolled()
        {
            Assert.That(LocalHostDecision.For(true, false, false, 0.5f, 3f), Is.EqualTo(LocalHostStep.Create));
            Assert.That(
                LocalHostDecision.For(true, false, false, LocalHostDecision.FreshPollSeconds, 3f),
                Is.EqualTo(LocalHostStep.Create));
        }

        [Test]
        public void WaitsForTheFirstPollBecauseTheLobbyOffersNoSlotBeforeIt()
        {
            Assert.That(LocalHostDecision.For(true, false, false, Never, 1f), Is.EqualTo(LocalHostStep.Wait));
        }

        [Test]
        public void APollFromBeforeTheLobbysReadinessWindowDoesNotCount()
        {
            Assert.That(
                LocalHostDecision.For(true, false, false, LocalHostDecision.FreshPollSeconds + 1f, 1f),
                Is.EqualTo(LocalHostStep.Wait));
        }

        [Test]
        public void APollWithoutARegistrationDoesNotCount()
        {
            Assert.That(LocalHostDecision.For(false, false, false, 0.5f, 1f), Is.EqualTo(LocalHostStep.Wait));
        }

        [Test]
        public void GivesUpWhenTheLobbyRefusedTheServer()
        {
            Assert.That(LocalHostDecision.For(false, true, false, Never, 0f), Is.EqualTo(LocalHostStep.GiveUp));
        }

        [Test]
        public void GivesUpWhenTheSlotIsStillPlayingInsteadOfWaitingOnAPollThatWillNotCome()
        {
            Assert.That(LocalHostDecision.For(true, false, true, 0.5f, 0f), Is.EqualTo(LocalHostStep.GiveUp));
        }

        [Test]
        public void GivesUpOncePatienceRunsOut()
        {
            Assert.That(
                LocalHostDecision.For(false, false, false, Never, LocalHostDecision.PatienceSeconds - 0.1f),
                Is.EqualTo(LocalHostStep.Wait));
            Assert.That(
                LocalHostDecision.For(false, false, false, Never, LocalHostDecision.PatienceSeconds),
                Is.EqualTo(LocalHostStep.GiveUp));
        }

        [Test]
        public void ReadinessWinsOverAnExpiredWait()
        {
            Assert.That(
                LocalHostDecision.For(true, false, false, 0.5f, LocalHostDecision.PatienceSeconds * 2f),
                Is.EqualTo(LocalHostStep.Create));
        }
    }
}
