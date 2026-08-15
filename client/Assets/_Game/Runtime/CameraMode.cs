namespace Blastlands.Runtime
{
    public enum CameraMode
    {
        // One camera framing every living player. Zooms out as they spread, which is why
        // it needs bounds: unbounded, four players in four corners turn everyone into a
        // speck, which is the problem a moving camera was meant to solve.
        Global = 0,

        // One camera following one player. What every client uses online, where the
        // question of who to follow has an obvious answer.
        Follow = 1,

        // A following camera each, sharing the screen. The couch answer to a camera that
        // cannot follow four people at once.
        Split = 2
    }
}
