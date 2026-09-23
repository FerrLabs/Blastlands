namespace Blastlands.Core.Net
{
    // What a client is allowed to say: which tick it is answering, and what it wants to
    // do. Nothing else. A position, a kill or a power-up asserted by a client is not
    // refused here, it simply has nowhere to be written, which is the whole reason the
    // simulation runs on the server.
    //
    // Written by hand and little-endian on purpose. BitConverter follows the host, so a
    // server on one architecture and a client on another would disagree about every
    // number without a single error anywhere.
    public static class InputCodec
    {
        public const int Size = 9;

        private const byte DropBit = 1 << 0;
        private const byte DashBit = 1 << 1;
        private const byte PushBit = 1 << 2;
        private const byte AbilityBit = 1 << 3;

        public static bool TryWrite(byte[] buffer, int offset, int tick, PlayerInput input)
        {
            // Negative ticks are refused here as well as on the read, so a caller with a
            // broken counter fails where the mistake is rather than sending nine bytes
            // that are only ever thrown away at the far end without saying why.
            if (buffer == null || offset < 0 || tick < 0 || buffer.Length - offset < Size)
            {
                return false;
            }

            WriteInt32(buffer, offset, tick);
            WriteInt16(buffer, offset + 4, (short)input.MoveX);
            WriteInt16(buffer, offset + 6, (short)input.MoveY);

            byte flags = 0;
            if (input.DropBomb) { flags |= DropBit; }
            if (input.Dash) { flags |= DashBit; }
            if (input.Push) { flags |= PushBit; }
            if (input.Ability) { flags |= AbilityBit; }
            buffer[offset + 8] = flags;

            return true;
        }

        // Everything that comes back out goes through the PlayerInput constructor, which
        // clamps the movement components. A client claiming to push the stick thirty
        // times further than a stick goes gets the same speed as everybody else, and the
        // server does not have to remember to check.
        public static bool TryRead(byte[] buffer, int offset, out int tick, out PlayerInput input)
        {
            tick = 0;
            input = PlayerInput.None;

            if (buffer == null || offset < 0 || buffer.Length - offset < Size)
            {
                return false;
            }

            tick = ReadInt32(buffer, offset);
            if (tick < 0)
            {
                return false;
            }

            byte flags = buffer[offset + 8];
            input = new PlayerInput(
                ReadInt16(buffer, offset + 4),
                ReadInt16(buffer, offset + 6),
                (flags & DropBit) != 0,
                (flags & DashBit) != 0,
                (flags & PushBit) != 0,
                (flags & AbilityBit) != 0);

            return true;
        }

        private static void WriteInt32(byte[] buffer, int at, int value)
        {
            buffer[at] = (byte)value;
            buffer[at + 1] = (byte)(value >> 8);
            buffer[at + 2] = (byte)(value >> 16);
            buffer[at + 3] = (byte)(value >> 24);
        }

        private static int ReadInt32(byte[] buffer, int at)
        {
            return buffer[at]
                | (buffer[at + 1] << 8)
                | (buffer[at + 2] << 16)
                | (buffer[at + 3] << 24);
        }

        private static void WriteInt16(byte[] buffer, int at, short value)
        {
            buffer[at] = (byte)value;
            buffer[at + 1] = (byte)(value >> 8);
        }

        private static short ReadInt16(byte[] buffer, int at)
        {
            return (short)(buffer[at] | (buffer[at + 1] << 8));
        }
    }
}
