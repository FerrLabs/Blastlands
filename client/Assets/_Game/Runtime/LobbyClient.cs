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

        public IEnumerator Create(string name, string host, int maxPlayers, Action<LobbyResult<MatchListing>> done)
        {
            string body = "{\"name\":\"" + Escape(name)
                + "\",\"host\":\"" + Escape(host)
                + "\",\"max_players\":" + maxPlayers + "}";

            yield return Send("/v1/matches", body, done);
        }

        public IEnumerator Join(string id, string player, Action<LobbyResult<MatchListing>> done)
        {
            yield return Send("/v1/matches/" + id + "/join", "{\"player\":\"" + Escape(player) + "\"}", done);
        }

        // Not Start. Unity reserves that name as a lifecycle message and refuses one
        // that takes parameters, so a component carrying this logged
        // "Start() can not take parameters" every time it was instantiated.
        public IEnumerator StartMatch(string id, Action<LobbyResult<bool>> done)
        {
            using (UnityWebRequest request = Post("/v1/matches/" + id + "/start", "{}"))
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
