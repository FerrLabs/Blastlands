using Blastlands.Core.Lobby;
using Blastlands.Core.Net;
using UnityEngine;

namespace Blastlands.Runtime
{
    // Asks the lobby what a player should be running, and decides what this build is
    // allowed to do about it.
    //
    // The rule lives in UpdateDecision and is tested there. What is here is the asking:
    // when, what happens while the answer is outstanding, and what happens when it never
    // comes. All three are decisions in their own right.
    public sealed class VersionGate : MonoBehaviour
    {
        [SerializeField] private LobbyClient lobby;

        public UpdateVerdict Verdict { get; private set; } = UpdateVerdict.Unknown;

        public ClientRelease Release { get; private set; }

        public bool Answered { get; private set; }

        // Playable until told otherwise, rather than blocked until allowed. A lobby that
        // is slow, unreachable or wrong must not stop somebody playing: the version gate
        // that actually keeps an old build out of a match is the server's, and this one
        // only decides what the player is shown. Starting from MustUpdate would mean an
        // outage locks every player out of a game they already have.
        public bool MayPlay
        {
            get { return UpdateDecision.MayPlay(Verdict); }
        }

        private void Start()
        {
#if UNITY_SERVER
            // A server does not update itself and has nobody to tell.
            enabled = false;
            return;
#else
            if (lobby == null)
            {
                Debug.LogWarning("Blastlands: no lobby to ask about updates, so this build assumes it is current.");
                Answered = true;
                return;
            }

            StartCoroutine(Ask());
#endif
        }

#if !UNITY_SERVER
        private System.Collections.IEnumerator Ask()
        {
            yield return lobby.Version(result =>
            {
                Answered = true;

                if (!result.Ok)
                {
                    // Deliberately not an error. A lobby that cannot be reached is the
                    // ordinary case on a train, and the outcome is that the player plays.
                    Debug.Log("Blastlands: the lobby did not say which build is current, so this one plays.");
                    return;
                }

                Release = result.Value;
                Verdict = UpdateDecision.For(Application.version, Release.Latest, Release.Minimum);

                Report();
            });
        }

        private void Report()
        {
            switch (Verdict)
            {
                case UpdateVerdict.MustUpdate:
                    // An error rather than a warning: this build cannot join anything,
                    // and the server would refuse it at the door anyway.
                    Debug.LogError(
                        "Blastlands: this build (" + Application.version + ") is below the minimum the lobby "
                        + "accepts (" + Release.Minimum + ") and cannot join a match. " + Where());
                    break;

                case UpdateVerdict.UpdateAvailable:
                    Debug.Log(
                        "Blastlands: " + Release.Latest + " is out, this is " + Application.version + ". " + Where());
                    break;
            }
        }

        // Said separately from the verdict, because a lobby can know a build is out of
        // date before it knows where the new one lives: the release publishes the
        // archive and its hash together, and there is a window between a version being
        // cut and those being written.
        private string Where()
        {
            return Release.CanBeFetched
                ? "It can be fetched from " + Release.DownloadUrl
                : "The lobby has not published a download for it yet.";
        }
#endif
    }
}
