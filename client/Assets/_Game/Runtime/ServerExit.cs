namespace Blastlands.Runtime
{
    // The exit code a supervisor reads. Anything non-zero has to mean the process
    // failed, or a crash loop looks like a clean shutdown.
    public static class ServerExit
    {
        public const int Ok = 0;
        public const int BadConfiguration = 1;
    }
}
