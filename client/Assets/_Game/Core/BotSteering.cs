namespace Blastlands.Core
{
    internal static class BotSteering
    {
        // The route is a sequence of tiles, so the bot aims at the middle of the next
        // one rather than leaning on a compass point. Free positions removed the
        // re-centring that used to do this for free, and without it a bot drifts off the
        // lane and grinds along corners: it kept moving, but it stopped clearing the
        // arena — 34 blocks a match down to 10.
        internal static PlayerInput Steer(PlayerState player, Direction direction, bool dash)
        {
            if (direction == Direction.None)
            {
                return PlayerInput.None;
            }

            // A dash commits to the dominant axis of whatever vector it is given, so a
            // steering vector aimed at a tile centre can send it off at right angles to
            // the escape it was meant to take. Dashes go out as a clean cardinal.
            if (dash)
            {
                return PlayerInput.Dashing(direction);
            }

            GridPos step = Directions.Delta(direction);
            GridPos target = player.Tile.Offset(step.X, step.Y);

            int toX = SubPos.CentreOf(target.X) - player.Position.X;
            int toY = SubPos.CentreOf(target.Y) - player.Position.Y;

            int magnitude = Magnitude(toX, toY);
            if (magnitude <= 0)
            {
                return new PlayerInput(step.X * StickReader.Range, step.Y * StickReader.Range, false, dash);
            }

            return new PlayerInput(
                toX * StickReader.Range / magnitude,
                toY * StickReader.Range / magnitude,
                false,
                dash);
        }

        private static int Magnitude(int x, int y)
        {
            long squared = ((long)x * x) + ((long)y * y);
            if (squared <= 0)
            {
                return 0;
            }

            int root = 0;
            while ((long)(root + 1) * (root + 1) <= squared)
            {
                root++;
            }

            return root;
        }
    }
}
