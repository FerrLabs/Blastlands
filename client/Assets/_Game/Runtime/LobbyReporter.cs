#if UNITY_SERVER
using System;
using System.Collections;
using Blastlands.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Blastlands.Runtime
{
    // The instance side of the lobby's internal endpoints: say you are alive, and give
    // the match back when you are done with it.
    //
    // A port is capacity. An instance that never releases one takes it out of the pool
    // until somebody restarts the lobby, so releasing is not a tidy-up at the end, it is
    // the last thing this process exists to do.
    public sealed class LobbyReporter : MonoBehaviour
    {
        // Three of these fit inside the lobby's thirty second silence window, so one
        // dropped beat is a hiccup and three in a row is a process that is not coming
        // back. The two numbers are a pair: moving this without moving
        // BLASTLANDS_LOBBY_SILENT_TTL_SECONDS reaps healthy instances.
        private const float BeatSeconds = 10f;

        private const int TimeoutSeconds = 5;

        private ServerOptions options;
        private bool releasing;

        public void Configure(ServerOptions serverOptions)
        {
            options = serverOptions;
            StartCoroutine(Beating());
        }

        // The caller is quitting after this, so it hands in what to do when the lobby has
        // answered rather than being left to guess how long to wait.
        public void Release(Action done)
        {
            if (releasing)
            {
                return;
            }

            releasing = true;
            StartCoroutine(Releasing(done));
        }

        // The first beat goes out immediately, not one interval later. The lobby starts
        // counting silence the moment it allocates the match, so every second an
        // instance spends booting is already spent out of that window: waiting a full
        // interval before saying anything hands back a third of it for nothing, and an
        // instance slower than the window is reaped before it ever reports.
        //
        // Found the hard way. A match left sitting while the instance was still starting
        // answered the first heartbeat with 404, because by then it had been reaped.
        private IEnumerator Beating()
        {
            var wait = new WaitForSeconds(BeatSeconds);

            while (!releasing)
            {
                using (UnityWebRequest beat = Post(Url("heartbeat")))
                {
                    yield return beat.SendWebRequest();

                    // Warned about and carried on. A missed beat is not worth abandoning
                    // a match over: the lobby allows three, and the players in this one
                    // would rather it kept going.
                    if (beat.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning($"Blastlands server: heartbeat failed, {Describe(beat)}");
                    }
                }

                yield return wait;
            }
        }

        private IEnumerator Releasing(Action done)
        {
            using (UnityWebRequest release = UnityWebRequest.Delete(Url(null)))
            {
                Authorise(release);
                release.timeout = TimeoutSeconds;
                yield return release.SendWebRequest();

                if (release.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"Blastlands server: match {options.MatchId} released");
                }
                else
                {
                    // Loud, because the port is now stranded until the lobby reaps this
                    // match for silence. That path exists and works, but it costs the
                    // silence window rather than being immediate.
                    Debug.LogError($"Blastlands server: could not release the match, {Describe(release)}");
                }
            }

            done?.Invoke();
        }

        private UnityWebRequest Post(string url)
        {
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = TimeoutSeconds
            };

            Authorise(request);
            return request;
        }

        private void Authorise(UnityWebRequest request)
        {
            request.SetRequestHeader("Authorization", "Bearer " + options.InstanceToken);
        }

        private string Url(string suffix)
        {
            string root = options.LobbyUrl.TrimEnd('/');
            string match = $"{root}/internal/matches/{options.MatchId}";
            return suffix == null ? match : match + "/" + suffix;
        }

        private static string Describe(UnityWebRequest request)
        {
            return request.responseCode > 0
                ? $"HTTP {request.responseCode}"
                : request.error ?? "no response";
        }
    }
}
#endif
