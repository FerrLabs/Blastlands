using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Blastlands.Core.Lobby;
using Blastlands.Core.Net;
using Blastlands.Core.Update;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace Blastlands.Runtime
{
    public sealed class ClientUpdater : MonoBehaviour
    {
        private const string DataSuffix = "_Data";

        public bool Updating { get; private set; }

        public IEnumerator Apply(ClientRelease release)
        {
            if (Updating)
            {
                yield break;
            }

            if (!Application.isEditor && Application.platform == RuntimePlatform.WindowsPlayer)
            {
                Updating = true;
                yield return Install(release);
                Updating = false;
            }
            else
            {
                Debug.Log("Blastlands: this build cannot update itself; only a Windows player can.");
            }
        }

        private IEnumerator Install(ClientRelease release)
        {
            if (!release.CanBeFetched || !GameVersion.TryParse(release.Latest, out GameVersion version))
            {
                Debug.LogWarning("Blastlands: the lobby did not describe a release this build can fetch.");
                yield break;
            }

            if (!GameVersion.TryParse(Application.version, out GameVersion current) || version <= current)
            {
                Debug.LogWarning(
                    "Blastlands: the lobby offers " + release.Latest + " and this build is "
                    + Application.version + ", so there is nothing newer to install.");
                yield break;
            }

            string dataDirectory = Application.dataPath;
            string install = Path.GetDirectoryName(dataDirectory);
            string executable = Path.GetFileName(dataDirectory);
            executable = executable.Substring(0, executable.Length - DataSuffix.Length) + ".exe";

            var layout = new UpdateLayout(install, version);
            string archive = Path.Combine(Application.temporaryCachePath, "Blastlands-" + version + ".zip");

            Debug.Log("Blastlands: downloading " + version + ".");
            using (UnityWebRequest request = UnityWebRequest.Get(release.DownloadUrl))
            {
                request.downloadHandler = new DownloadHandlerFile(archive) { removeFileOnAbort = true };
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("Blastlands: the update could not be downloaded: " + request.error);
                    yield break;
                }
            }

            Task<string> staging = Task.Run(() => Stage(archive, release.Sha256, layout, executable));
            while (!staging.IsCompleted)
            {
                yield return null;
            }

            string failure = staging.IsFaulted ? staging.Exception.GetBaseException().Message : staging.Result;
            if (failure != null)
            {
                Debug.LogWarning("Blastlands: the update was not installed: " + failure);
                yield break;
            }

            string script = Path.Combine(Application.temporaryCachePath, "blastlands-update.ps1");
            File.WriteAllText(
                script,
                SwapScript.PowerShell(Process.GetCurrentProcess().Id, layout, executable),
                new UTF8Encoding(true));

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + script + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Application.temporaryCachePath,
            });

            Debug.Log("Blastlands: " + version + " is staged, restarting to install it.");
            Application.Quit();
        }

        private static string Stage(string archive, string sha256, UpdateLayout layout, string executable)
        {
            try
            {
                using (FileStream content = File.OpenRead(archive))
                {
                    if (!Digest.Matches(content, sha256))
                    {
                        return "the download does not match the published sha256";
                    }
                }

                if (Directory.Exists(layout.Staged))
                {
                    Directory.Delete(layout.Staged, true);
                }

                using (ZipArchive zip = ZipFile.OpenRead(archive))
                {
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        if (!ArchivePath.IsSafe(entry.FullName))
                        {
                            return "the archive contains an entry outside the game: " + entry.FullName;
                        }

                        string destination = Path.Combine(layout.Staged, entry.FullName);
                        if (entry.Name.Length == 0)
                        {
                            Directory.CreateDirectory(destination);
                            continue;
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        entry.ExtractToFile(destination, true);
                    }
                }

                return File.Exists(Path.Combine(layout.Staged, executable))
                    ? null
                    : "the archive does not contain " + executable;
            }
            finally
            {
                File.Delete(archive);
            }
        }
    }
}
