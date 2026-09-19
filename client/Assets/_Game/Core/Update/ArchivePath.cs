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
    }
}
