using Blastlands.Core.Lobby;
using Blastlands.Core.Net;

namespace Blastlands.Core.Update
{
    public readonly struct UpdateBadge
    {
        private UpdateBadge(string version, string action, bool pressable)
        {
            Version = version;
            Action = action;
            Pressable = pressable;
        }

        public string Version { get; }

        public string Action { get; }

        public bool Pressable { get; }

        public bool Offers
        {
            get { return Action != null; }
        }

        public static UpdateBadge For(
            string installed, UpdateVerdict verdict, UpdateStage stage, ClientRelease release)
        {
            string version = "v" + installed;

            bool newer = verdict == UpdateVerdict.UpdateAvailable || verdict == UpdateVerdict.MustUpdate;
            if (!newer || string.IsNullOrEmpty(release.Latest))
            {
                return new UpdateBadge(version, null, false);
            }

            switch (stage)
            {
                case UpdateStage.Running:
                    return new UpdateBadge(version, "Updating to " + release.Latest, false);

                case UpdateStage.Unpublished:
                case UpdateStage.Unsupported:
                    return new UpdateBadge(version, release.Latest + " is out", false);

                default:
                    return release.CanBeFetched
                        ? new UpdateBadge(version, "Update to " + release.Latest, true)
                        : new UpdateBadge(version, release.Latest + " is out", false);
            }
        }
    }
}
