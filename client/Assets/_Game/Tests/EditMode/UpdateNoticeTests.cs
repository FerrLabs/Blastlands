using Blastlands.Core.Lobby;
using Blastlands.Core.Net;
using Blastlands.Core.Update;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class UpdateNoticeTests
    {
        private static ClientRelease Release()
        {
            return new ClientRelease("26.9.5", "26.9.0", "https://lobby.example/v1/client/26.9.5/download", new string('a', 64));
        }

        private static UpdateNotice Notice(UpdateVerdict verdict, UpdateStage stage, string failure = null)
        {
            return UpdateNotice.For(verdict, stage, failure, "26.8.1", Release());
        }

        [Test]
        public void AnOptionalUpdateWaitingOnThePlayerStaysOnTheSmallButton()
        {
            Assert.That(Notice(UpdateVerdict.UpdateAvailable, UpdateStage.Idle).Prominent, Is.False);
            Assert.That(Notice(UpdateVerdict.UpdateAvailable, UpdateStage.Running).Prominent, Is.False, "the button already says it is updating");
        }

        [Test]
        public void AnOptionalUpdateThatCannotGoAheadSaysWhy()
        {
            foreach (UpdateStage stage in new[] { UpdateStage.Failed, UpdateStage.Unsupported, UpdateStage.Unpublished })
            {
                Assert.That(Notice(UpdateVerdict.UpdateAvailable, stage, "disk full").Prominent, Is.True, stage.ToString());
            }
        }

        [Test]
        public void AMandatoryUpdateIsAlwaysProminent()
        {
            Assert.That(Notice(UpdateVerdict.MustUpdate, UpdateStage.Idle).Prominent, Is.True);
        }

        [Test]
        public void ACurrentBuildIsShownNothing()
        {
            Assert.That(Notice(UpdateVerdict.UpToDate, UpdateStage.Idle).Visible, Is.False);
        }

        [Test]
        public void AnUnreachableLobbyIsShownNothing()
        {
            Assert.That(Notice(UpdateVerdict.Unknown, UpdateStage.Idle).Visible, Is.False);
        }

        [Test]
        public void ABuildBelowTheMinimumIsBlockedAndToldWhy()
        {
            UpdateNotice notice = Notice(UpdateVerdict.MustUpdate, UpdateStage.Running);

            Assert.That(notice.Visible, Is.True);
            Assert.That(notice.BlocksPlay, Is.True);
            Assert.That(notice.Detail, Does.Contain("26.8.1").And.Contain("26.9.0"));
        }

        [Test]
        public void ABlockedBuildThatCannotUpdateItselfIsToldToInstallByHand()
        {
            UpdateNotice notice = Notice(UpdateVerdict.MustUpdate, UpdateStage.Unsupported);

            Assert.That(notice.BlocksPlay, Is.True);
            Assert.That(notice.Detail, Does.Contain("by hand"));
            Assert.That(notice.Detail, Does.Contain("26.9.5"));
            Assert.That(notice.OffersUpdate, Is.False);
        }

        [Test]
        public void ABlockedBuildWaitingOnAReleaseIsToldToTryAgain()
        {
            Assert.That(
                Notice(UpdateVerdict.MustUpdate, UpdateStage.Unpublished).Detail,
                Does.Contain("nothing to download yet"));
        }

        [Test]
        public void AFailedUpdateSaysWhatWentWrong()
        {
            UpdateNotice notice = Notice(
                UpdateVerdict.MustUpdate, UpdateStage.Failed, "the download does not match the published sha256");

            Assert.That(notice.BlocksPlay, Is.True);
            Assert.That(notice.Detail, Does.Contain("the download does not match the published sha256"));
        }

        [Test]
        public void AFailureWithNoReasonStillReadsAsASentence()
        {
            string detail = Notice(UpdateVerdict.MustUpdate, UpdateStage.Failed).Detail;

            Assert.That(detail, Does.Not.Contain(": ."));
            Assert.That(detail, Does.Contain("could not be installed."));
        }

        [Test]
        public void ABuildBetweenTheMinimumAndTheLatestIsOfferedTheUpdateAndKeepsPlaying()
        {
            UpdateNotice notice = Notice(UpdateVerdict.UpdateAvailable, UpdateStage.Idle);

            Assert.That(notice.Visible, Is.True);
            Assert.That(notice.BlocksPlay, Is.False);
            Assert.That(notice.OffersUpdate, Is.True);
            Assert.That(notice.Headline, Does.Contain("26.9.5"));
        }

        [Test]
        public void AnOfferIsWithdrawnOnceTheUpdateIsUnderWay()
        {
            Assert.That(Notice(UpdateVerdict.UpdateAvailable, UpdateStage.Running).OffersUpdate, Is.False);
            Assert.That(Notice(UpdateVerdict.UpdateAvailable, UpdateStage.Unpublished).OffersUpdate, Is.False);
            Assert.That(Notice(UpdateVerdict.UpdateAvailable, UpdateStage.Unsupported).OffersUpdate, Is.False);
            Assert.That(Notice(UpdateVerdict.UpdateAvailable, UpdateStage.Failed).OffersUpdate, Is.False);
        }

        [Test]
        public void AnOptionalUpdateThatFailedDoesNotBlockPlay()
        {
            UpdateNotice notice = Notice(UpdateVerdict.UpdateAvailable, UpdateStage.Failed, "the download timed out");

            Assert.That(notice.BlocksPlay, Is.False);
            Assert.That(notice.Detail, Does.Contain("the download timed out"));
        }

        [Test]
        public void EveryVisibleNoticeSaysSomething()
        {
            foreach (UpdateVerdict verdict in new[] { UpdateVerdict.MustUpdate, UpdateVerdict.UpdateAvailable })
            {
                foreach (UpdateStage stage in System.Enum.GetValues(typeof(UpdateStage)))
                {
                    UpdateNotice notice = Notice(verdict, (UpdateStage)stage);

                    Assert.That(notice.Headline, Is.Not.Null.And.Not.Empty, verdict + " " + stage);
                    Assert.That(notice.Detail, Is.Not.Null.And.Not.Empty, verdict + " " + stage);
                }
            }
        }
    }
}
