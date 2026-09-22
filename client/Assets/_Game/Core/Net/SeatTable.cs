using System.Collections.Generic;

namespace Blastlands.Core.Net
{
    // Which player each connection is.
    //
    // Kept away from the transport because it is the part that has to be right rather
    // than the part that has to talk to a socket: a seat handed out twice is two people
    // driving one character, and a seat handed to a connection that already had one is a
    // player who loses theirs by reconnecting.
    //
    // Seats are the simulation's player indices, so seat 0 is state.Players[0]. A
    // player reconnecting with the ticket they held gets that seat back; otherwise a
    // seat nobody has held is preferred, then the lowest free one, so a match that lost
    // a player does not leave a hole in the middle with everybody shuffled up.
    public sealed class SeatTable
    {
        public const int NoSeat = -1;

        private readonly Dictionary<ulong, int> byConnection = new Dictionary<ulong, int>();
        private readonly ulong[] occupants;
        private readonly bool[] taken;
        private readonly string[] holders;

        public SeatTable(int seats)
        {
            if (seats < 0)
            {
                seats = 0;
            }

            occupants = new ulong[seats];
            taken = new bool[seats];
            holders = new string[seats];
        }

        public int Seats
        {
            get { return taken.Length; }
        }

        public int Occupied
        {
            get { return byConnection.Count; }
        }

        public bool Full
        {
            get { return byConnection.Count >= taken.Length; }
        }

        // A connection that already has a seat keeps it. NGO can raise a connect for a
        // client that is already known during a reconnect, and taking a second seat for
        // it would strand the first as occupied for ever.
        public int Claim(ulong connection)
        {
            return Claim(connection, null);
        }

        public int Claim(ulong connection, string holder)
        {
            if (byConnection.TryGetValue(connection, out int already))
            {
                return already;
            }

            int seat = holder == null ? NoSeat : FreeSeatHeldBy(holder);
            if (seat == NoSeat)
            {
                seat = FreeSeatHeldBy(null);
            }

            if (seat == NoSeat)
            {
                seat = FreeSeat();
            }

            if (seat == NoSeat)
            {
                return NoSeat;
            }

            taken[seat] = true;
            occupants[seat] = connection;
            holders[seat] = holder;
            byConnection[connection] = seat;
            return seat;
        }

        private int FreeSeatHeldBy(string holder)
        {
            for (int seat = 0; seat < taken.Length; seat++)
            {
                if (!taken[seat] && holders[seat] == holder)
                {
                    return seat;
                }
            }

            return NoSeat;
        }

        private int FreeSeat()
        {
            for (int seat = 0; seat < taken.Length; seat++)
            {
                if (!taken[seat])
                {
                    return seat;
                }
            }

            return NoSeat;
        }

        // The seat is freed rather than kept warm. Holding it would mean a match that
        // lost a player can never be filled, and filling it is #25.
        public int Release(ulong connection)
        {
            if (!byConnection.TryGetValue(connection, out int seat))
            {
                return NoSeat;
            }

            byConnection.Remove(connection);
            taken[seat] = false;
            occupants[seat] = 0;
            return seat;
        }

        public int SeatOf(ulong connection)
        {
            return byConnection.TryGetValue(connection, out int seat) ? seat : NoSeat;
        }

        public bool TryOccupant(int seat, out ulong connection)
        {
            connection = 0;
            if (seat < 0 || seat >= taken.Length || !taken[seat])
            {
                return false;
            }

            connection = occupants[seat];
            return true;
        }
    }
}
