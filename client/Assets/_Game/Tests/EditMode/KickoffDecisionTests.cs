using Blastlands.Core.Net;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    // When a match may start, and when an instance should stop waiting for it. Getting
    // the second one wrong is the expensive half: the port stays allocated, the lobby
    // keeps handing it out, and nothing in the logs says so.
    public class KickoffDecisionTests
    {
        private const float Patience = 30f;

        [Test]
        public void AFullArenaPlaysWithoutWaitingOutTheClock()
        {
            Assert.That(KickoffDecision.For(4, 4, 0f, Patience), Is.EqualTo(Kickoff.Play));
        }

        [Test]
        public void AMissingSeatKeepsTheMatchWaitingWhileThereIsTimeLeft()
        {
            Assert.That(KickoffDecision.For(4, 3, 0f, Patience), Is.EqualTo(Kickoff.Wait));
            Assert.That(KickoffDecision.For(4, 3, 29.9f, Patience), Is.EqualTo(Kickoff.Wait));
        }

        [Test]
        public void TheWaitRunningOutWithEnoughPlayersStartsTheMatchShortHanded()
        {
            // Three turned up out of four. Giving up here would cost the three their
            // match to punish the one who did not connect.
            Assert.That(KickoffDecision.For(4, 3, Patience, Patience), Is.EqualTo(Kickoff.Play));
            Assert.That(KickoffDecision.For(4, 2, Patience, Patience), Is.EqualTo(Kickoff.Play));
        }

        [Test]
        public void TheWaitRunningOutAloneGivesUpRatherThanPlayingAgainstNobody()
        {
            Assert.That(KickoffDecision.For(4, 1, Patience, Patience), Is.EqualTo(Kickoff.GiveUp));
            Assert.That(KickoffDecision.For(2, 0, Patience, Patience), Is.EqualTo(Kickoff.GiveUp));
        }

        [Test]
        public void ATwoSeatMatchStillNeedsBothOfThem()
        {
            // The floor and the capacity are the same number here, so the short-handed
            // branch must not quietly start a duel with one player in it.
            Assert.That(KickoffDecision.For(2, 1, 0f, Patience), Is.EqualTo(Kickoff.Wait));
            Assert.That(KickoffDecision.For(2, 1, Patience, Patience), Is.EqualTo(Kickoff.GiveUp));
        }

        [Test]
        public void MorePlayersThanSeatsIsStillAStart()
        {
            // SeatTable turns away anyone past capacity, so this cannot happen from the
            // transport. It is here so the comparison stays the one that is meant.
            Assert.That(KickoffDecision.For(2, 3, 0f, Patience), Is.EqualTo(Kickoff.Play));
        }
    }
}
