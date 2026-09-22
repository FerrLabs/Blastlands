using System;
using System.Collections;
using System.Collections.Generic;
using Blastlands.Core.Lobby;
using UnityEngine;
using UnityEngine.Networking;

namespace Blastlands.Runtime
{
    // Talks to the lobby service. Transport only: it turns HTTP into the typed results
    // in Core and knows nothing about screens.
    //
    // Coroutines rather than async/await, which is what the rest of the client already
    // runs on and what survives a scene unloading mid-request without leaving a task
    // holding a destroyed object.
    public sealed class LobbyClient : MonoBehaviour
    {
        // Sent on every call. The lobby refuses a request without it, and refuses one
        // whose build it considers too old, which is the whole point: a client that
        // cannot speak the current protocol should be told at the door rather than
        // halfway into a match.
        private const string VersionHeader = "x-blastlands-version";

        [SerializeField] private string baseUrl = "http://127.0.0.1:8080";
        [SerializeField] private float timeoutSeconds = 5f;

        public void Use(string lobbyUrl)
        {
            baseUrl = lobbyUrl;
        }

        // The lobby writes both versions as strings rather than numbers, so they arrive
        // as text and are parsed by GameVersion rather than by JsonUtility.
        [Serializable]
        private sealed class ReleaseDto
        {
            public string latest;
            public string minimum;
            public string download_url;
            public string sha256;
        }

        [Serializable]
        private sealed class EndpointDto
        {
            public string host;
            public int port;
        }

        // What create hands back: the listing, where the game server is, the ticket for
        // that game server, and the separate ticket that proves who may start it.
        [Serializable]
        private sealed class MatchCreatedDto
        {
            public string id;
            public string name;
            public string host;
            public int players;
            public int max_players;
            public EndpointDto endpoint;
            public string game_ticket;
            public string ticket;
        }

        [Serializable]
        private sealed class JoinAcceptedDto
        {
            public EndpointDto endpoint;
            public string ticket;
        }

        [Serializable]
        private sealed class MatchStatusDto
        {
            public string id;
            public string name;
            public string host;
            public int players;
            public int max_players;
            public string state;
        }

        [Serializable]
        private sealed class MatchSummaryDto
        {
            public string id;
            public string name;
            public string host;
            public int players;
            public int max_players;
        }

        [Serializable]
        private sealed class MatchSummaryListDto
        {
            public MatchSummaryDto[] items;
        }

        [Serializable]
        private sealed class ErrorDto
        {
            public string code;
            public string message;
        }

        // Asked before anything else, because the answer decides whether the rest is
        // worth asking. A build below the published minimum desyncs the simulation
        // quietly rather than failing loudly, so it is turned away here rather than
        // halfway into somebody's match.
        public IEnumerator Version(Action<LobbyResult<ClientRelease>> done)
        {
            using (UnityWebRequest request = Get("/v1/version"))
            {
                yield return request.SendWebRequest();

                LobbyFailure failure;
                if (!Succeeded(request, out failure))
                {
                    done(LobbyResult<ClientRelease>.Failed(failure));
                    yield break;
                }

                ReleaseDto dto;
                try
                {
                    dto = JsonUtility.FromJson<ReleaseDto>(request.downloadHandler.text);
                }
                catch (ArgumentException)
                {
                    dto = null;
                }

                // Both versions required, not just a non-null object. Any 200 carrying
                // valid JSON parses into a DTO with every field null, so a captive portal
                // or a misrouted proxy would be reported as a successful version check
                // and the gate would then say nothing at all: the one case where a log
                // would tell you the answer was junk is the case that would stay silent.
                if (dto == null || string.IsNullOrEmpty(dto.latest) || string.IsNullOrEmpty(dto.minimum))
                {
                    done(LobbyResult<ClientRelease>.Failed(LobbyFailure.Unreadable));
                    yield break;
                }

                done(LobbyResult<ClientRelease>.Success(
                    new ClientRelease(dto.latest, dto.minimum, dto.download_url, dto.sha256)));
            }
        }

        public IEnumerator List(Action<LobbyResult<IReadOnlyList<MatchListing>>> done)
        {
            using (UnityWebRequest request = Get("/v1/matches"))
            {
                yield return request.SendWebRequest();

                LobbyFailure failure;
                if (!Succeeded(request, out failure))
                {
                    done(LobbyResult<IReadOnlyList<MatchListing>>.Failed(failure));
                    yield break;
                }

                // JsonUtility cannot read a top-level array, so the payload is wrapped
                // before it is handed over. Cheaper than taking a JSON dependency for
                // one endpoint.
                MatchSummaryListDto parsed = null;
                try
                {
                    parsed = JsonUtility.FromJson<MatchSummaryListDto>(
                        "{\"items\":" + request.downloadHandler.text + "}");
                }
                catch (ArgumentException)
                {
                    parsed = null;
                }

                if (parsed == null || parsed.items == null)
                {
                    done(LobbyResult<IReadOnlyList<MatchListing>>.Failed(LobbyFailure.Unreadable));
                    yield break;
                }

                var listings = new List<MatchListing>(parsed.items.Length);
                foreach (MatchSummaryDto dto in parsed.items)
                {
                    listings.Add(new MatchListing(dto.id, dto.name, dto.host, dto.players, dto.max_players));
                }

                done(LobbyResult<IReadOnlyList<MatchListing>>.Success(listings));
            }
        }

        public IEnumerator Create(string name, string host, int maxPlayers, Action<LobbyResult<MatchHosting>> done)
        {
            string body = "{\"name\":\"" + Escape(name)
                + "\",\"host\":\"" + Escape(host)
                + "\",\"max_players\":" + maxPlayers + "}";

            using (UnityWebRequest request = Post("/v1/matches", body))
            {
                yield return request.SendWebRequest();

                LobbyFailure failure;
                if (!Succeeded(request, out failure))
                {
                    done(LobbyResult<MatchHosting>.Failed(failure));
                    yield break;
                }

                MatchCreatedDto dto = Read<MatchCreatedDto>(request);
                if (dto == null || string.IsNullOrEmpty(dto.id) || dto.endpoint == null)
                {
                    done(LobbyResult<MatchHosting>.Failed(LobbyFailure.Unreadable));
                    yield break;
                }

                done(LobbyResult<MatchHosting>.Success(new MatchHosting(
                    new MatchListing(dto.id, dto.name, dto.host, dto.players, dto.max_players),
                    new MatchInvite(dto.id, dto.endpoint.host, dto.endpoint.port, dto.game_ticket),
                    dto.ticket)));
            }
        }

        public IEnumerator Join(string id, string player, Action<LobbyResult<MatchInvite>> done)
        {
            string body = "{\"player\":\"" + Escape(player) + "\"}";

            using (UnityWebRequest request = Post("/v1/matches/" + id + "/join", body))
            {
                yield return request.SendWebRequest();

                LobbyFailure failure;
                if (!Succeeded(request, out failure))
                {
                    done(LobbyResult<MatchInvite>.Failed(failure));
                    yield break;
                }

                JoinAcceptedDto dto = Read<JoinAcceptedDto>(request);
                if (dto == null || dto.endpoint == null)
                {
                    done(LobbyResult<MatchInvite>.Failed(LobbyFailure.Unreadable));
                    yield break;
                }

                done(LobbyResult<MatchInvite>.Success(
                    new MatchInvite(id, dto.endpoint.host, dto.endpoint.port, dto.ticket)));
            }
        }

        // Polled by everyone waiting on a match they did not create: the host presses
        // start on their own machine, and this is how the others hear about it.
        public IEnumerator Status(string id, Action<LobbyResult<MatchProgress>> done)
        {
            using (UnityWebRequest request = Get("/v1/matches/" + id))
            {
                yield return request.SendWebRequest();

                LobbyFailure failure;
                if (!Succeeded(request, out failure))
                {
                    done(LobbyResult<MatchProgress>.Failed(failure));
                    yield break;
                }

                MatchStatusDto dto = Read<MatchStatusDto>(request);
                if (dto == null || string.IsNullOrEmpty(dto.id))
                {
                    done(LobbyResult<MatchProgress>.Failed(LobbyFailure.Unreadable));
                    yield break;
                }

                done(LobbyResult<MatchProgress>.Success(new MatchProgress(
                    new MatchListing(dto.id, dto.name, dto.host, dto.players, dto.max_players),
                    MatchProgress.Reads(dto.state))));
            }
        }

        private static T Read<T>(UnityWebRequest request) where T : class
        {
            try
            {
                return JsonUtility.FromJson<T>(request.downloadHandler.text);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        // Not Start. Unity reserves that name as a lifecycle message and refuses one
        // that takes parameters, so a component carrying this logged
        // "Start() can not take parameters" every time it was instantiated.
        // The ticket the lobby handed back when this client created the match. Starting
        // is the host's call, and match ids are public, so the lobby will not take
        // anyone's word for who is asking.
        public IEnumerator StartMatch(string id, string ticket, Action<LobbyResult<bool>> done)
        {
            string body = "{\"ticket\":\"" + Escape(ticket) + "\"}";

            using (UnityWebRequest request = Post("/v1/matches/" + id + "/start", body))
            {
                yield return request.SendWebRequest();

                LobbyFailure failure;
                done(Succeeded(request, out failure)
                    ? LobbyResult<bool>.Success(true)
                    : LobbyResult<bool>.Failed(failure));
            }
        }

        private IEnumerator Send(string path, string body, Action<LobbyResult<MatchListing>> done)
        {
            using (UnityWebRequest request = Post(path, body))
            {
                yield return request.SendWebRequest();

                LobbyFailure failure;
                if (!Succeeded(request, out failure))
                {
                    done(LobbyResult<MatchListing>.Failed(failure));
                    yield break;
                }

                MatchSummaryDto dto = null;
                try
                {
                    dto = JsonUtility.FromJson<MatchSummaryDto>(request.downloadHandler.text);
                }
                catch (ArgumentException)
                {
                    dto = null;
                }

                done(dto == null || string.IsNullOrEmpty(dto.id)
                    ? LobbyResult<MatchListing>.Failed(LobbyFailure.Unreadable)
                    : LobbyResult<MatchListing>.Success(
                        new MatchListing(dto.id, dto.name, dto.host, dto.players, dto.max_players)));
            }
        }

        private UnityWebRequest Get(string path)
        {
            UnityWebRequest request = UnityWebRequest.Get(baseUrl + path);
            Prepare(request);
            return request;
        }

        private UnityWebRequest Post(string path, string body)
        {
            var request = new UnityWebRequest(baseUrl + path, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            Prepare(request);
            return request;
        }

        private void Prepare(UnityWebRequest request)
        {
            request.timeout = Mathf.Max(1, Mathf.CeilToInt(timeoutSeconds));
            request.SetRequestHeader(VersionHeader, Application.version);
        }

        // A refusal carries a code in its body, and that code is the whole answer. The
        // HTTP status is deliberately not consulted: the lobby says match_full with a
        // 409 today and could say it with a 400 tomorrow without the screens caring.
        private static bool Succeeded(UnityWebRequest request, out LobbyFailure failure)
        {
            if (request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.DataProcessingError)
            {
                failure = LobbyFailure.Unreachable;
                return false;
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                failure = LobbyFailure.Unknown;
                return true;
            }

            failure = LobbyFailure.Unknown;
            string payload = request.downloadHandler == null ? null : request.downloadHandler.text;
            if (string.IsNullOrEmpty(payload))
            {
                return false;
            }

            try
            {
                ErrorDto error = JsonUtility.FromJson<ErrorDto>(payload);
                if (error != null && !string.IsNullOrEmpty(error.code))
                {
                    failure = LobbyFailures.FromCode(error.code);
                }
            }
            catch (ArgumentException)
            {
                failure = LobbyFailure.Unreadable;
            }

            return false;
        }

        // Names are already restricted to letters, digits, space, hyphen and underscore
        // by DisplayName, so this only has to survive the ones that slipped past a
        // caller that forgot to check. Which means it has to cover control characters
        // too: a raw newline inside a JSON string is not merely ugly, it makes the whole
        // body unparseable, and the lobby would answer with something about the request
        // rather than about the name.
        private static string Escape(string value)
        {
            var builder = new System.Text.StringBuilder();

            foreach (char c in value ?? string.Empty)
            {
                if (c == '\\')
                {
                    builder.Append("\\\\");
                }
                else if (c == '"')
                {
                    builder.Append("\\\"");
                }
                else if (c == '\n')
                {
                    builder.Append("\\n");
                }
                else if (c == '\r')
                {
                    builder.Append("\\r");
                }
                else if (c == '\t')
                {
                    builder.Append("\\t");
                }
                else if (c < 0x20)
                {
                    builder.Append("\\u").Append(((int)c).ToString("x4"));
                }
                else
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }
    }
}
