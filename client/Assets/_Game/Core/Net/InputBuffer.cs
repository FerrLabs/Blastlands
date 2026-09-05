namespace Blastlands.Core.Net
{
    // One player's inputs, waiting for the ticks they answer.
    //
    // The server never waits. A tick whose input has not arrived repeats the last one
    // the player sent rather than stalling, because a stalled tick would hand every
    // player in the match a stutter caused by one bad connection. Repeating is also what
    // a held key looks like, so a dropped packet costs nothing when the player was
    // holding a direction and one tick of a stale direction when they were not.
    public sealed class InputBuffer
    {
        // A second at thirty ticks. A client further ahead than that is either running
        // on a broken clock or trying to make the server hold packets for it, and both
        // are better dropped than stored.
        public const int DefaultCapacity = 30;

        private const int Empty = -1;

        private readonly PlayerInput[] slots;
        private readonly int[] slotTicks;

        private PlayerInput last = PlayerInput.None;
        private int taken = Empty;

        public InputBuffer()
            : this(DefaultCapacity)
        {
        }

        public InputBuffer(int capacity)
        {
            if (capacity < 1)
            {
                capacity = 1;
            }

            slots = new PlayerInput[capacity];
            slotTicks = new int[capacity];
            for (int i = 0; i < slotTicks.Length; i++)
            {
                slotTicks[i] = Empty;
            }
        }

        public int Capacity
        {
            get { return slots.Length; }
        }

        // Refused rather than stored when it answers a tick already played or one too far
        // ahead to be honest. Neither is an error worth reporting: the first is a packet
        // that lost a race it was always going to lose, and the second is a client that
        // cannot help itself.
        public bool Offer(int tick, PlayerInput input)
        {
            if (tick <= taken || tick > taken + slots.Length)
            {
                return false;
            }

            int slot = tick % slots.Length;
            slots[slot] = input;
            slotTicks[slot] = tick;
            return true;
        }

        // Consumed rather than left in place, so a slot cannot be read twice when the
        // ring wraps back onto it.
        public PlayerInput Take(int tick)
        {
            int slot = tick % slots.Length;
            taken = tick;

            if (slotTicks[slot] == tick)
            {
                last = slots[slot];
                slotTicks[slot] = Empty;
                return last;
            }

            // A repeat keeps the direction and drops the buttons. MatchSim reads all
            // three as level-triggered, so repeating them spends bombs and dash charges
            // on ticks the player never sent: a lost packet would cost them inventory.
            // Holding a direction through a gap costs nothing, because a held key already
            // looks like this and the player is walking where they were walking anyway.
            return new PlayerInput(last.MoveX, last.MoveY, false, false, false);
        }
    }
}
