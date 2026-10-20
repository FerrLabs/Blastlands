namespace Blastlands.Core.Lobby
{
    public sealed class ServerCredentials
    {
        public ServerRegistration Current { get; private set; }

        public int Generation { get; private set; }

        public bool Renewing { get; private set; }

        public bool Ready
        {
            get { return Current != null && !Renewing; }
        }

        public bool TryBeginRenewal(int seenGeneration)
        {
            if (Renewing || seenGeneration != Generation)
            {
                return false;
            }

            Renewing = true;
            return true;
        }

        public void Install(ServerRegistration registration)
        {
            Current = registration;
            Generation++;
            Renewing = false;
        }
    }
}
