namespace Blastlands.Core.Net
{
    // Reads back what NetWriter wrote, and never reads past the end of the buffer.
    //
    // A short or corrupt message returns zeroes and sets Ok to false rather than
    // throwing. A packet arrives from a network: it being wrong is an ordinary event,
    // not an exceptional one, and an exception in the middle of applying a snapshot
    // would leave the state half updated.
    public struct NetReader
    {
        private readonly byte[] buffer;
        private readonly int end;
        private int at;
        private bool ran;

        public NetReader(byte[] source)
            : this(source, source == null ? 0 : source.Length)
        {
        }

        // The length that arrived rather than the length of the array it landed in.
        // Buffers are reused, so the bytes past a short message are the tail of the last
        // one: bounding on the array would let a reader run through the seam and parse a
        // message that is half this tick and half the one before it, with every count in
        // range and nothing reporting a problem.
        public NetReader(byte[] source, int length)
        {
            buffer = source;
            at = 0;
            end = source == null ? 0 : (length < 0 ? 0 : (length > source.Length ? source.Length : length));
            ran = source == null;
        }

        public int Position
        {
            get { return at; }
        }

        public bool Ok
        {
            get { return !ran; }
        }

        public byte Byte()
        {
            if (!Room(1))
            {
                return 0;
            }

            return buffer[at++];
        }

        public bool Bool()
        {
            return Byte() != 0;
        }

        public int Int16()
        {
            if (!Room(2))
            {
                return 0;
            }

            int value = buffer[at] | (buffer[at + 1] << 8);
            at += 2;
            return (short)value;
        }

        public int Int32()
        {
            if (!Room(4))
            {
                return 0;
            }

            int value = buffer[at]
                | (buffer[at + 1] << 8)
                | (buffer[at + 2] << 16)
                | (buffer[at + 3] << 24);
            at += 4;
            return value;
        }

        // For the counts a message carries before a run of entities. A corrupt count is
        // the field that turns a bad packet into an allocation the size of the number it
        // happened to contain, so it is clamped rather than trusted and the reader is
        // marked spent when it was out of range.
        public int Count(int most)
        {
            int value = Int32();
            if (value < 0 || value > most)
            {
                ran = true;
                return 0;
            }

            return value;
        }

        private bool Room(int bytes)
        {
            if (ran || end - at < bytes)
            {
                ran = true;
                return false;
            }

            return true;
        }
    }
}
