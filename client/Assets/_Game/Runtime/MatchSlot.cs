#if UNITY_SERVER
using System;
using System.Collections;
using System.Globalization;
using Blastlands.Core;
using Blastlands.Core.Net;
using UnityEngine;
using UnityEngine.Networking;

namespace Blastlands.Runtime
{
    // One port of a server process, and the match it is serving if there is one.
    //
    // Idle, it asks the lobby every few seconds which match the port has been given, and
    // the asking is what keeps the port on offer: PortPool only hands out a port that
    // polled recently. Busy, it stops asking, so the lobby stops offering it. When the
    // match is over and released, the slot tears it down and goes back to asking.
    public sealed class MatchSlot : MonoBehaviour
    {
        // Wider than a poll interval, narrower than the lobby's readiness window once the
        // interval is added: HostOptions.MaxPollSeconds is sized against this.
        private const int RequestTimeoutSeconds = 10;

        [Serializable]
        private sealed class AssignmentDto
        {
            public string match_id;
            public int players;
            public int humans;
            public string mode;
            public string bot_skill;
        }

        private int port;
        private HostOptions host;
        private byte[] ticketKey;
        private GameObject running;
        private string finishedMatch;
        private string taking;
        private bool draining;

        public bool Busy
        {
            get { return running != null; }
        }

        public void Watch(int slotPort, HostOptions hostOptions, byte[] key)
        {
            port = slotPort;
            host = hostOptions;
            ticketKey = key;
            StartCoroutine(Polling());
        }

        public void Drain()
        {
            draining = true;
        }

        private IEnumerator Polling()
        {
            var wait = new WaitForSeconds(host.PollSeconds);

            while (!draining)
            {
                if (running == null)
                {
                    yield return Asking();
                }

                yield return wait;
            }
        }

        private IEnumerator Asking()
        {
            string url = $"{host.LobbyUrl.TrimEnd('/')}/internal/instances/{port}";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("Authorization", "Bearer " + host.InstanceToken);
                request.timeout = RequestTimeoutSeconds;

                UnityWebRequestAsyncOperation sending = LobbyReporter.Begin(request);
                if (sending == null)
                {
                    yield break;
                }

                yield return sending;

                switch (request.responseCode)
                {
                    case 204:
                        break;

                    case 200:
                        TakeSafely(request.downloadHandler.text);
                        break;

                    default:
                        Debug.LogWarning(
                            $"Blastlands server: the lobby answered {LobbyReporter.Describe(request)} for port {port}");
                        break;
                }
            }
        }

        // Anything thrown while taking a match would leave the polling coroutine, and
        // Unity stops a coroutine that throws. The slot would never ask again, still
        // count as idle, and the pod would serve one port fewer with the container green.
        // When a match was one process, the same throw ended the container and kubelet
        // brought it back; here it has to be caught.
        //
        // The match is remembered like a refused one. A throw that comes from the
        // assignment itself would otherwise repeat on every poll, building and tearing
        // down the same match until the pod is replaced.
        private void TakeSafely(string body)
        {
            taking = null;

            try
            {
                Take(body);
            }
            catch (Exception failed)
            {
                Debug.LogError($"Blastlands server: taking a match on {port} failed, {failed}");

                if (running != null)
                {
                    Destroy(running);
                    running = null;
                }

                if (taking != null)
                {
                    finishedMatch = taking;
                }
            }
        }

        private void Take(string body)
        {
            AssignmentDto assignment;
            try
            {
                assignment = JsonUtility.FromJson<AssignmentDto>(body);
            }
            catch (Exception bad)
            {
                Debug.LogError($"Blastlands server: the lobby's answer for {port} could not be read, {bad.Message}");
                return;
            }

            if (assignment == null || string.IsNullOrWhiteSpace(assignment.match_id))
            {
                Debug.LogError($"Blastlands server: the lobby's answer for {port} named no match");
                return;
            }

            taking = assignment.match_id;

            // Seeing the match this slot just finished means the release never landed.
            // Replaying it would run a finished game on a loop; waiting instead lets the
            // lobby's reaper free the port.
            if (assignment.match_id == finishedMatch)
            {
                Debug.LogWarning($"Blastlands server: {finishedMatch} is still assigned to {port} after it ended, not replaying it");
                return;
            }

            // A poll that was already in flight when the drain began. The match stays with
            // the port, which the pod's replacement will serve.
            if (draining)
            {
                Debug.Log($"Blastlands server: draining, leaving {assignment.match_id} on {port} for another instance");
                return;
            }

            if (!ServerOptions.TryCreate(
                    port,
                    host.LobbyUrl,
                    host.InstanceToken,
                    assignment.match_id,
                    assignment.players.ToString(CultureInfo.InvariantCulture),
                    assignment.humans > 0 ? assignment.humans.ToString(CultureInfo.InvariantCulture) : null,
                    assignment.mode,
                    assignment.bot_skill,
                    out ServerOptions options,
                    out string problem))
            {
                Refuse(assignment.match_id, problem);
                return;
            }

            MatchState state;
            try
            {
                state = MatchFactory.Create(
                    ArenaSettings.For(options.Mode),
                    MatchSettings.For(options.Mode),
                    options.ExpectedPlayers,
                    Seed(),
                    CharacterKits.ForSeat);
            }
            catch (ArgumentOutOfRangeException bad)
            {
                // The arena is what knows how many it seats, so this is where a player
                // count too high for the board is caught rather than when it was read.
                Refuse(options.MatchId, bad.Message);
                return;
            }

            Debug.Log(
                $"Blastlands server: taking {options.Mode} match {options.MatchId} on {port}, "
                + $"{options.ExpectedPlayers} seats for {options.ExpectedHumans} players, {options.BotSkill} bots");

            running = new GameObject("Match " + options.MatchId);
            running.transform.SetParent(transform, false);

            string matchId = options.MatchId;
            running.AddComponent<ServerLoop>().Run(
                state, options, new GameTicketVerifier(ticketKey, matchId), clean => Ended(matchId, clean));
        }

        private void Ended(string matchId, bool clean)
        {
            if (clean)
            {
                Debug.Log($"Blastlands server: match {matchId} on {port} is over");
            }
            else
            {
                Debug.LogError($"Blastlands server: match {matchId} on {port} ended badly, see above");
            }

            finishedMatch = matchId;
            Destroy(running);
            running = null;
        }

        // Remembered like a finished match, so the next poll does not pick the same bad
        // assignment up again every few seconds. It stays assigned until the lobby reaps
        // it for silence, which is the path a match nobody can start takes anyway.
        private void Refuse(string matchId, string problem)
        {
            Debug.LogError($"Blastlands server: refusing match {matchId} on {port}, {problem}");
            finishedMatch = matchId;
        }

        // Fresh per match. A fixed seed would give every match on the host the same arena.
        private static uint Seed()
        {
            return (uint)UnityEngine.Random.Range(1, int.MaxValue);
        }
    }
}
#endif
