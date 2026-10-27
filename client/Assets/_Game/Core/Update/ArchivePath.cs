using System;
using System.IO;

namespace Blastlands.Core.Update
{
    public static class ArchivePath
    {
        public static bool IsSafe(string entryName)
        {
            if (string.IsNullOrEmpty(entryName) || entryName.IndexOf(':') >= 0)
            {
                return false;
            }

            if (entryName[0] == '/' || entryName[0] == '\\')
            {
                return false;
            }

            foreach (string segment in entryName.Split('/', '\\'))
            {
                if (segment == "..")
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsInside(string root, string destination)
        {
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            return Path.GetFullPath(destination).StartsWith(prefix, StringComparison.Ordinal);
        }
    }
}
