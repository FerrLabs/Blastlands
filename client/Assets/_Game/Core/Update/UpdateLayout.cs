using System;
using System.Collections.Generic;
using System.IO;
using Blastlands.Core.Net;

namespace Blastlands.Core.Update
{
    public readonly struct UpdateLayout
    {
        public const int LongestPath = 259;

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

        public string TooDeepFor(IEnumerable<string> relativePaths)
        {
            int longest = 0;
            foreach (string relative in relativePaths)
            {
                int length = Path.Combine(Staged, relative).Length;
                if (length > longest)
                {
                    longest = length;
                }
            }

            return longest > LongestPath
                ? "the game's folder is too deep for Windows to unpack the update beside it ("
                    + longest + " characters, the limit is " + LongestPath + "), so move it to a shorter folder"
                : null;
        }
    }
}
