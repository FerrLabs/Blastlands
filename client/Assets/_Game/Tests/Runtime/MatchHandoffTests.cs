using Blastlands.Core;
using Blastlands.Core.Lobby;
using Blastlands.Runtime;
using NUnit.Framework;

namespace Blastlands.Runtime.Tests
{
    public class MatchHandoffTests
    {
        [TearDown]
        public void TearDown()
        {
            MatchHandoff.Forget();
        }

        [Test]
        public void APracticeRequestIsTakenOnceWithTheModeItAskedFor()
        {
            MatchHandoff.Practise(GameMode.Classic, "Bryan");

            Assert.That(MatchHandoff.TryTakePractice(out GameMode mode), Is.True);
            Assert.That(mode, Is.EqualTo(GameMode.Classic));
            Assert.That(MatchHandoff.TryTakePractice(out _), Is.False, "opening the Match scene again is not another practice");
            Assert.That(MatchHandoff.Player, Is.EqualTo("Bryan"), "the lobby still knows who you are when you come back");
        }

        [Test]
        public void PractisingNeverLeavesAnOnlineMatchToDial()
        {
            MatchHandoff.Practise(GameMode.Arena, "Bryan");

            Assert.That(MatchHandoff.Waiting, Is.False, "or the networked driver would try to join a match");
        }

        [Test]
        public void PractisingOutlivesTheDriverThatTookTheMode()
        {
            MatchHandoff.Practise(GameMode.Arena, "Bryan");
            MatchHandoff.TryTakePractice(out _);

            Assert.That(
                MatchHandoff.Practising,
                Is.True,
                "both drivers start in an undefined order, so the one that reads second must still see it");
        }

        [Test]
        public void LeavingForAnOnlineMatchEndsThePractice()
        {
            MatchHandoff.Practise(GameMode.Arena, "Bryan");
            MatchHandoff.Leave(new MatchInvite("m1", "host", 7777, "ticket", GameMode.Arena), "Bryan");

            Assert.That(MatchHandoff.Practising, Is.False, "or the networked driver would stand down for a real match");
            Assert.That(MatchHandoff.Waiting, Is.True);
        }

        [Test]
        public void ForgettingClearsBothHalves()
        {
            MatchHandoff.Practise(GameMode.Classic, "Bryan");

            MatchHandoff.Forget();

            Assert.That(MatchHandoff.Practising, Is.False);
            Assert.That(MatchHandoff.TryTakePractice(out _), Is.False);
        }
    }
}
