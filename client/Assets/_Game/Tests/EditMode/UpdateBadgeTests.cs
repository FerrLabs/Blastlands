using Blastlands.Core.Lobby;
using Blastlands.Core.Net;
using Blastlands.Core.Update;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class UpdateBadgeTests
    {
        private static readonly ClientRelease Fetchable = new ClientRelease(
            "26.9.5", "26.9.0", "https://lobby.example/v1/client/26.9.5/download", new string('a', 64));

        private static readonly ClientRelease Unfetchable = new ClientRelease("26.9.5", "26.9.0", null, null);

        private static UpdateBadge Badge(UpdateVerdict verdict, UpdateStage stage, ClientRelease release)
        {
            return UpdateBadge.For("26.8.1", verdict, stage, release);
        }

        [Test]
        public void ThePlayerAlwaysSeesWhichBuildTheyAreOn()
        {
            Assert.That(Badge(UpdateVerdict.UpToDate, UpdateStage.Idle, Fetchable).Version, Is.EqualTo("v26.8.1"));
            Assert.That(Badge(UpdateVerdict.Unknown, UpdateStage.Idle, default).Version, Is.EqualTo("v26.8.1"));
        }

        [Test]
        public void ACurrentOrUnansweredBuildOffersNothing()
        {
            Assert.That(Badge(UpdateVerdict.UpToDate, UpdateStage.Idle, Fetchable).Offers, Is.False);
            Assert.That(Badge(UpdateVerdict.Unknown, UpdateStage.Idle, default).Offers, Is.False);
        }

        [Test]
        public void ANewerBuildOffersAPressableUpdate()
        {
            UpdateBadge badge = Badge(UpdateVerdict.UpdateAvailable, UpdateStage.Idle, Fetchable);

            Assert.That(badge.Action, Is.EqualTo("Update to 26.9.5"));
            Assert.That(badge.Pressable, Is.True);
            Assert.That(Badge(UpdateVerdict.MustUpdate, UpdateStage.Failed, Fetchable).Pressable, Is.True, "a failure can be retried");
        }

        [Test]
        public void ADownloadInFlightIsShownButNotPressedTwice()
        {
            UpdateBadge badge = Badge(UpdateVerdict.MustUpdate, UpdateStage.Running, Fetchable);

            Assert.That(badge.Action, Is.EqualTo("Updating to 26.9.5"));
            Assert.That(badge.Pressable, Is.False);
        }

        [Test]
        public void AnUpdateNothingCanFetchOrThisBuildCannotInstallIsOnlyAnnounced()
        {
            Assert.That(Badge(UpdateVerdict.UpdateAvailable, UpdateStage.Idle, Unfetchable).Pressable, Is.False);
            Assert.That(Badge(UpdateVerdict.UpdateAvailable, UpdateStage.Unsupported, Fetchable).Pressable, Is.False);
            Assert.That(Badge(UpdateVerdict.UpdateAvailable, UpdateStage.Unpublished, Fetchable).Action, Is.EqualTo("26.9.5 is out"));
        }
    }
}
