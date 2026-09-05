using Blastlands.Core.Net;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    // Whether a build may play. Both ways of being wrong here are expensive and they are
    // not equally expensive: letting an outdated client in desyncs the match for everyone
    // in it, quietly, while refusing a good one costs one person an evening. The rule is
    // written around that asymmetry and these tests pin it.
    public class UpdateDecisionTests
    {
        [Test]
        public void ACurrentBuildIsToldNothing()
        {
            Assert.That(UpdateDecision.For("26.9.1", "26.9.1", "26.8.0"), Is.EqualTo(UpdateVerdict.UpToDate));
        }

        [Test]
        public void ABuildBehindTheLatestIsOfferedTheUpdate()
        {
            UpdateVerdict verdict = UpdateDecision.For("26.9.0", "26.9.1", "26.8.0");

            Assert.That(verdict, Is.EqualTo(UpdateVerdict.UpdateAvailable));
            Assert.That(UpdateDecision.MayPlay(verdict), Is.True, "an offer is not a refusal");
        }

        [Test]
        public void ABuildBelowTheMinimumIsRefused()
        {
            UpdateVerdict verdict = UpdateDecision.For("26.7.9", "26.9.1", "26.8.0");

            Assert.That(verdict, Is.EqualTo(UpdateVerdict.MustUpdate));
            Assert.That(UpdateDecision.MayPlay(verdict), Is.False);
        }

        [Test]
        public void TheMinimumIsPlayableAndTheVersionBelowItIsNot()
        {
            // The boundary, spelled out. "Below minimum" and "at minimum" are one patch
            // apart and the difference is whether somebody can play tonight.
            Assert.That(UpdateDecision.For("26.8.0", "26.9.1", "26.8.0"), Is.Not.EqualTo(UpdateVerdict.MustUpdate));
            Assert.That(UpdateDecision.For("26.7.99", "26.9.1", "26.8.0"), Is.EqualTo(UpdateVerdict.MustUpdate));
        }

        [Test]
        public void BeingTooOldOutranksBeingOutOfDate()
        {
            // A build below the minimum is also below the latest. It has to be told the
            // one that stops it, not the one that merely suggests.
            Assert.That(UpdateDecision.For("26.1.0", "26.9.1", "26.8.0"), Is.EqualTo(UpdateVerdict.MustUpdate));
        }

        [Test]
        public void ALobbyThatSaysNothingIsNotPermissionToRefuse()
        {
            // A dead lobby must not brick the game. This is the case that decides whether
            // an outage is an inconvenience or an outage plus everybody locked out.
            foreach (UpdateVerdict verdict in new[]
                     {
                         UpdateDecision.For("26.9.0", null, null),
                         UpdateDecision.For("26.9.0", "", ""),
                         UpdateDecision.For("26.9.0", "not a version", "nonsense")
                     })
            {
                Assert.That(verdict, Is.EqualTo(UpdateVerdict.Unknown));
                Assert.That(UpdateDecision.MayPlay(verdict), Is.True);
            }
        }

        [Test]
        public void HalfAnAnswerIsStillUsed()
        {
            // A lobby that publishes a minimum and a latest that does not parse should
            // still be able to keep an ancient build out, and the other way round.
            Assert.That(UpdateDecision.For("26.1.0", "rubbish", "26.8.0"), Is.EqualTo(UpdateVerdict.MustUpdate));
            Assert.That(UpdateDecision.For("26.9.0", "26.9.1", "rubbish"), Is.EqualTo(UpdateVerdict.UpdateAvailable));
        }

        [Test]
        public void ABuildNewerThanTheLobbyKnowsAboutMayPlay()
        {
            // What every developer running from the editor looks like, and what a player
            // looks like in the minutes between a release going out and the lobby being
            // told. Refusing it would make shipping an update lock out the person who
            // shipped it.
            UpdateVerdict verdict = UpdateDecision.For("27.0.0", "26.9.1", "26.8.0");

            Assert.That(verdict, Is.EqualTo(UpdateVerdict.UpToDate));
            Assert.That(UpdateDecision.MayPlay(verdict), Is.True);
        }

        [Test]
        public void ABuildThatCannotSayWhatItIsIsNotBricked()
        {
            // Refusing here would lock out every client the day a version string changed
            // shape, and the server gate is what actually keeps a bad build out of a
            // match: this rule only decides what the player is shown.
            Assert.That(UpdateDecision.For(null, "26.9.1", "26.8.0"), Is.EqualTo(UpdateVerdict.Unknown));
            Assert.That(UpdateDecision.For("26.9", "26.9.1", "26.8.0"), Is.EqualTo(UpdateVerdict.Unknown));
        }

        [Test]
        public void VersionsCompareComponentWiseAndNotAsText()
        {
            // The one that catches a string comparison: "26.10.0" sorts before "26.9.0"
            // as text and after it as a version, so a text compare would offer an update
            // to the newer build and refuse the older one.
            Assert.That(GameVersion.TryParse("26.10.0", out GameVersion ten), Is.True);
            Assert.That(GameVersion.TryParse("26.9.0", out GameVersion nine), Is.True);

            Assert.That(ten > nine, Is.True);
            Assert.That(UpdateDecision.For("26.9.0", "26.10.0", "26.8.0"), Is.EqualTo(UpdateVerdict.UpdateAvailable));
            Assert.That(UpdateDecision.For("26.10.0", "26.9.0", "26.8.0"), Is.EqualTo(UpdateVerdict.UpToDate));
        }

        [Test]
        public void AVersionWithSomethingOtherThanDigitsIsRefused()
        {
            // The lobby parses these the same way, so anything accepted here that it
            // rejects is a client that thinks it may play and is turned away at the door.
            foreach (string bad in new[] { "26.9.0-rc1", "v26.9.0", "26.9.0.1", "26..0", "-1.9.0", "26.9. 0" })
            {
                Assert.That(GameVersion.TryParse(bad, out _), Is.False, bad + " was accepted");
            }
        }

        [Test]
        public void SurroundingSpaceIsTrimmedRatherThanRefused()
        {
            // JSON and environment variables both leave it behind, and a version that
            // fails to parse over a space would read as a lobby that said nothing.
            Assert.That(GameVersion.TryParse(" 26.9.1 ", out GameVersion version), Is.True);
            Assert.That(version.ToString(), Is.EqualTo("26.9.1"));
        }
    }
}
