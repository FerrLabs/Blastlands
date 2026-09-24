using Blastlands.Core;
using Blastlands.Runtime;
using NUnit.Framework;

namespace Blastlands.Runtime.Tests
{
    public class MatchHandoffTests
    {
        [TearDown]
        public void TearDown()
        {
            MatchHandoff.TryTakePractice(out _);
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
    }
}
