namespace Blastlands.Core.Update
{
    public enum UpdateStage : byte
    {
        Idle = 0,

        Running = 1,

        Unpublished = 2,

        Unsupported = 3,

        Failed = 4
    }
}
