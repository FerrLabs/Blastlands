#if UNITY_SERVER
using System;
using System.Collections;
using System.Text;
using Blastlands.Core;
using Blastlands.Core.Lobby;
using UnityEngine;
using UnityEngine.Networking;

namespace Blastlands.Runtime
{
    public sealed class LobbyRegistrar : MonoBehaviour
    {
        private const string VersionHeader = "x-blastlands-version";
        private const int TimeoutSeconds = 10;

        [Serializable]
        private sealed class RegistrationDto
        {
            public string id;
            public string key;
            public string ticket_secret;
        }

        [Serializable]
        private sealed class ErrorDto
        {
            public string code;
            public string message;
        }

        private readonly ServerCredentials credentials = new ServerCredentials();
        private HostOptions host;

        public ServerCredentials Credentials
        {
            get { return credentials; }
        }

        public void Begin(HostOptions hostOptions)
        {
            host = hostOptions;
            Rejected(credentials.Generation);
        }

        public void Rejected(int generation)
        {
            if (credentials.TryBeginRenewal(generation))
            {
                StartCoroutine(Registering());
            }
        }

        private IEnumerator Registering()
        {
            string url = $"{host.LobbyUrl.TrimEnd('/')}/v1/servers";
            byte[] body = Encoding.UTF8.GetBytes(RegistrationPolicy.Body(host.FirstPort, host.Slots));

            for (int attempt = 0; ; attempt++)
            {
                ServerRegistration registration = null;

                using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
                {
                    request.uploadHandler = new UploadHandlerRaw(body);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader(VersionHeader, Application.version);
                    request.timeout = TimeoutSeconds;

                    UnityWebRequestAsyncOperation sending = LobbyReporter.Begin(request);
                    if (sending != null)
                    {
                        yield return sending;

                        switch (RegistrationPolicy.Classify(request.responseCode))
                        {
                            case RegistrationOutcome.Registered:
                                registration = Read(request.downloadHandler.text);
                                break;

                            case RegistrationOutcome.ClientTooOld:
                                Stop($"the lobby refused this build as too old, {Reason(request)}");
                                yield break;

                            case RegistrationOutcome.Rejected:
                                Stop($"the lobby refused the registration, {Reason(request)}");
                                yield break;

                            default:
                                Debug.LogWarning(
                                    $"Blastlands server: the lobby answered {LobbyReporter.Describe(request)} to the registration");
                                break;
                        }
                    }
                }

                if (registration != null)
                {
                    credentials.Install(registration);
                    Debug.Log($"Blastlands server: registered with the lobby as {registration.Id}");
                    yield break;
                }

                int seconds = RegistrationPolicy.RetrySeconds(attempt);
                Debug.LogWarning($"Blastlands server: registering again in {seconds} s");
                yield return new WaitForSeconds(seconds);
            }
        }

        private static ServerRegistration Read(string text)
        {
            RegistrationDto dto;
            try
            {
                dto = JsonUtility.FromJson<RegistrationDto>(text);
            }
            catch (Exception bad)
            {
                Debug.LogWarning($"Blastlands server: the lobby's registration could not be read, {bad.Message}");
                return null;
            }

            if (dto == null
                || !ServerRegistration.TryCreate(dto.id, dto.key, dto.ticket_secret, out ServerRegistration registration, out string problem))
            {
                Debug.LogWarning($"Blastlands server: the lobby's registration was unusable, {problem ?? "empty answer"}");
                return null;
            }

            return registration;
        }

        private static string Reason(UnityWebRequest request)
        {
            try
            {
                ErrorDto error = JsonUtility.FromJson<ErrorDto>(request.downloadHandler.text);
                if (error != null && !string.IsNullOrWhiteSpace(error.message))
                {
                    return error.message;
                }
            }
            catch (ArgumentException)
            {
                return LobbyReporter.Describe(request) + " (unreadable body)";
            }

            return LobbyReporter.Describe(request);
        }

        private static void Stop(string reason)
        {
            Debug.LogError("Blastlands server: " + reason);
            Application.Quit(ServerBootstrap.BadConfiguration);
        }
    }
}
#endif
