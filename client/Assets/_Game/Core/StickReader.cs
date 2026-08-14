namespace Blastlands.Core
{
    // A thumbstick is analogue but movement is four-way, so the stick has to be
    // reduced to one direction. Kept in Core, and therefore in integers, so the rule
    // is testable and identical wherever inputs are produced.
    //
    // Axes follow the grid, not the pad: positive Y is Down. Callers invert the
    // stick's Y themselves.
    public static class StickReader
    {
        public const int Range = 1000;
        public const int DefaultDeadzone = 400;

        public static Direction ToDirection(int x, int y)
        {
            return ToDirection(x, y, DefaultDeadzone);
        }

        public static Direction ToDirection(int x, int y, int deadzone)
        {
            int magnitudeX = x < 0 ? -x : x;
            int magnitudeY = y < 0 ? -y : y;

            if (magnitudeX < deadzone && magnitudeY < deadzone)
            {
                return Direction.None;
            }

            // Ties go to horizontal on purpose: an exact diagonal has to resolve the
            // same way every time or the player drifts unpredictably.
            if (magnitudeX >= magnitudeY)
            {
                return x > 0 ? Direction.Right : Direction.Left;
            }

            return y > 0 ? Direction.Down : Direction.Up;
        }
    }
}
