using Blastlands.Core.Lobby;
using Blastlands.Core.Net;

namespace Blastlands.Core.Update
{
    public readonly struct UpdateNotice
    {
        private UpdateNotice(string headline, string detail, bool blocksPlay, bool offersUpdate, bool prominent)
        {
            Headline = headline;
            Detail = detail;
            BlocksPlay = blocksPlay;
            OffersUpdate = offersUpdate;
            Prominent = prominent;
        }

        public string Headline { get; }

        public string Detail { get; }

        public bool BlocksPlay { get; }

        public bool OffersUpdate { get; }

        public bool Prominent { get; }

        public bool Visible
        {
            get { return Headline != null; }
        }

        public static UpdateNotice Silent
        {
            get { return default; }
        }

        public static UpdateNotice For(
            UpdateVerdict verdict, UpdateStage stage, string failure, string installed, ClientRelease release)
        {
            switch (verdict)
            {
                case UpdateVerdict.MustUpdate:
                    return new UpdateNotice("Update required", Blocked(stage, failure, installed, release), true, false, true);

                case UpdateVerdict.UpdateAvailable:
                    return new UpdateNotice(
                        "Blastlands " + release.Latest + " is out",
                        Offered(stage, failure, installed, release),
                        false,
                        stage == UpdateStage.Idle,
                        stage == UpdateStage.Unpublished || stage == UpdateStage.Unsupported || stage == UpdateStage.Failed);

                default:
                    return Silent;
            }
        }

        private static string Blocked(UpdateStage stage, string failure, string installed, ClientRelease release)
        {
            string tooOld = "Build " + installed + " is older than " + release.Minimum
                + ", the oldest this lobby lets into a match.";

            switch (stage)
            {
                case UpdateStage.Running:
                    return tooOld + " Downloading " + release.Latest
                        + ", Blastlands will restart once it is installed.";

                case UpdateStage.Unpublished:
                    return tooOld + " There is nothing to download yet, so try again in a few minutes.";

                case UpdateStage.Unsupported:
                    return tooOld + " This build cannot update itself, so install " + release.Latest + " by hand.";

                case UpdateStage.Failed:
                    return tooOld + " " + release.Latest + " could not be installed" + Because(failure)
                        + ". Install it by hand to play.";

                default:
                    return tooOld;
            }
        }

        private static string Offered(UpdateStage stage, string failure, string installed, ClientRelease release)
        {
            string thisBuild = "This build is " + installed + ".";

            switch (stage)
            {
                case UpdateStage.Running:
                    return "Downloading " + release.Latest + ", Blastlands will restart once it is installed.";

                case UpdateStage.Unpublished:
                    return thisBuild + " There is nothing to download yet.";

                case UpdateStage.Unsupported:
                    return thisBuild + " It cannot update itself, so install " + release.Latest + " by hand.";

                case UpdateStage.Failed:
                    return thisBuild + " The update could not be installed" + Because(failure)
                        + ", so this one keeps playing.";

                default:
                    return thisBuild;
            }
        }

        private static string Because(string failure)
        {
            return string.IsNullOrEmpty(failure) ? string.Empty : ": " + failure;
        }
    }
}
