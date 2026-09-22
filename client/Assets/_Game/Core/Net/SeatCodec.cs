namespace Blastlands.Core.Net
{
    // What the server tells one client the moment it admits it: which player it is, and
    // the board it will be sent snapshots of. The client cannot know the second on its
    // own, and a client that guesses wrong has every snapshot refused, which on screen
    // is a board that sits perfectly still.
    public readonly struct SeatAssignment
    {
        public SeatAssignment(int seat, int width, int height, int players)
        {
            Seat = seat;
            Width = width;
            Height = height;
            Players = players;
        }

        public int Seat { get; }

        public int Width { get; }

        public int Height { get; }

        public int Players { get; }
    }

    public static class SeatCodec
    {
        public const int Size = 16;

        public static bool TryWrite(byte[] buffer, SeatAssignment assignment)
        {
            if (buffer == null || buffer.Length < Size || !Sound(assignment))
            {
                return false;
            }

            var writer = new NetWriter(buffer);
            writer.Int32(assignment.Seat);
            writer.Int32(assignment.Width);
            writer.Int32(assignment.Height);
            writer.Int32(assignment.Players);

            return writer.Ok;
        }

        // A board smaller than the arena allows, or a seat outside the match it belongs
        // to, is refused here rather than thrown at MatchFactory, which would take the
        // client down on a message it should simply distrust.
        public static bool TryRead(byte[] buffer, int size, out SeatAssignment assignment)
        {
            assignment = default;

            if (buffer == null || size < Size)
            {
                return false;
            }

            var reader = new NetReader(buffer, size);
            var read = new SeatAssignment(reader.Int32(), reader.Int32(), reader.Int32(), reader.Int32());

            if (!reader.Ok || !Sound(read))
            {
                return false;
            }

            assignment = read;
            return true;
        }

        // A player count above what any board can seat is refused here, so the far end
        // cannot ask for a match MatchFactory would throw on. A board that keeps fewer
        // spawns than it could still has to be handled where the match is built: this
        // knows the ceiling, not the board.
        private static bool Sound(SeatAssignment assignment)
        {
            return assignment.Seat >= 0
                && assignment.Players > 0
                && assignment.Seat < assignment.Players
                && assignment.Players <= ArenaGenerator.MostSpawns
                && assignment.Width >= ArenaSettings.SmallestSide
                && assignment.Height >= ArenaSettings.SmallestSide;
        }
    }
}
