namespace Blastlands.Core.Net
{
    // Appends little-endian numbers to a buffer and refuses to run off the end.
    //
    // Little-endian by hand for the reason InputCodec gives: BitConverter follows the
    // host, so two machines built differently would disagree about every number with
    // nothing reporting an error.
    //
    // A full buffer is remembered rather than thrown, so a caller can write a whole
    // message and ask once at the end whether it fitted, instead of testing every field.
    public struct NetWriter
    {
        private readonly byte[] buffer;
        private int at;
        private bool overflowed;

        public NetWriter(byte[] target)
        {
            buffer = target;
            at = 0;
            overflowed = target == null;
        }

        public int Length
        {
            get { return at; }
        }

        public bool Ok
        {
            get { return !overflowed; }
        }

        public void Byte(byte value)
        {
            if (!Room(1))
            {
                return;
            }

            buffer[at++] = value;
        }

        public void Bool(bool value)
        {
            Byte(value ? (byte)1 : (byte)0);
        }

        public void Int16(int value)
        {
            if (!Room(2))
            {
                return;
            }

            buffer[at++] = (byte)value;
            buffer[at++] = (byte)(value >> 8);
        }

        public void Int32(int value)
        {
            if (!Room(4))
            {
                return;
            }

            buffer[at++] = (byte)value;
            buffer[at++] = (byte)(value >> 8);
            buffer[at++] = (byte)(value >> 16);
            buffer[at++] = (byte)(value >> 24);
        }

        private bool Room(int bytes)
        {
            if (overflowed || buffer.Length - at < bytes)
            {
                overflowed = true;
                return false;
            }

            return true;
        }
    }
}
