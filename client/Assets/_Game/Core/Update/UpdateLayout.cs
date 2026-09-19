using System;
using System.IO;
using Blastlands.Core.Net;

namespace Blastlands.Core.Update
{
    public readonly struct UpdateLayout
    {
        public UpdateLayout(string installDirectory, GameVersion version)
        {
            if (string.IsNullOrEmpty(installDirectory))
            {
                throw new ArgumentException("the install directory is required", nameof(installDirectory));
            }

            string install = installDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string parent = Path.GetDirectoryName(install);
            if (string.IsNullOrEmpty(parent))
            {
                throw new ArgumentException("the game cannot update itself from a drive root", nameof(installDirectory));
            }

            string name = Path.GetFileName(install);

            Install = install;
            Staged = Path.Combine(parent, name + ".update-" + version);
            Previous = Path.Combine(parent, name + ".previous");
        }

        public string Install { get; }

        public string Staged { get; }

        public string Previous { get; }
    }
}
