using Blastlands.Core.Net;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    // Which connection is which player. Every failure here is somebody else's character
    // moving when you press a key, which is the kind of bug that reads as the whole
    // netcode being broken.
    public class SeatTableTests
    {
        [Test]
        public void SeatsAreHandedOutFromTheLowestFreeOne()
        {
            var table = new SeatTable(4);

            Assert.That(table.Claim(100), Is.EqualTo(0));
            Assert.That(table.Claim(200), Is.EqualTo(1));
            Assert.That(table.Claim(300), Is.EqualTo(2));
        }

        [Test]
        public void NoTwoConnectionsShareASeat()
        {
            // The one that matters most: two people driving one character.
            var table = new SeatTable(4);
            var seen = new System.Collections.Generic.HashSet<int>();

            for (ulong connection = 1; connection <= 4; connection++)
            {
                int seat = table.Claim(connection);
                Assert.That(seen.Add(seat), Is.True, $"seat {seat} was handed out twice");
            }
        }

        [Test]
        public void AConnectionThatAlreadyHasASeatKeepsIt()
        {
            // NGO can raise a connect for a client it already knows during a reconnect.
            // Taking a second seat for it would strand the first as occupied for ever.
            var table = new SeatTable(4);
            int first = table.Claim(100);

            Assert.That(table.Claim(100), Is.EqualTo(first));
            Assert.That(table.Occupied, Is.EqualTo(1));
        }

        [Test]
        public void AFullMatchTurnsTheNextOneAway()
        {
            var table = new SeatTable(2);
            table.Claim(1);
            table.Claim(2);

            Assert.That(table.Full, Is.True);
            Assert.That(table.Claim(3), Is.EqualTo(SeatTable.NoSeat));
            Assert.That(table.Occupied, Is.EqualTo(2), "the refused connection was counted anyway");
        }

        [Test]
        public void ASeatIsFreedWhenItsPlayerLeaves()
        {
            var table = new SeatTable(2);
            table.Claim(1);
            table.Claim(2);

            Assert.That(table.Release(1), Is.EqualTo(0));
            Assert.That(table.Full, Is.False);
            Assert.That(table.Claim(3), Is.EqualTo(0), "the freed seat was not reused");
        }

        [Test]
        public void TheLowestFreeSeatIsTakenRatherThanTheNextAlong()
        {
            // A match that lost a player and gained another should not end up with a hole
            // in the middle and everybody shuffled up, because the seat is the player
            // index the simulation uses.
            var table = new SeatTable(4);
            table.Claim(1);
            table.Claim(2);
            table.Claim(3);
            table.Release(2);

            Assert.That(table.Claim(9), Is.EqualTo(1));
        }

        [Test]
        public void ReleasingSomebodyWhoWasNeverThereChangesNothing()
        {
            var table = new SeatTable(2);
            table.Claim(1);

            Assert.That(table.Release(77), Is.EqualTo(SeatTable.NoSeat));
            Assert.That(table.Occupied, Is.EqualTo(1));
            Assert.That(table.SeatOf(1), Is.EqualTo(0), "an unrelated release moved somebody");
        }

        [Test]
        public void AnUnknownConnectionHasNoSeat()
        {
            // The server looks this up on every input packet, so it has to answer rather
            // than throw: a packet from a connection that just dropped is ordinary.
            var table = new SeatTable(2);

            Assert.That(table.SeatOf(404), Is.EqualTo(SeatTable.NoSeat));
        }

        [Test]
        public void AnEmptySeatHasNoOccupantToAnswerFor()
        {
            var table = new SeatTable(2);
            table.Claim(50);

            Assert.That(table.TryOccupant(0, out ulong who), Is.True);
            Assert.That(who, Is.EqualTo(50));
            Assert.That(table.TryOccupant(1, out _), Is.False);
            Assert.That(table.TryOccupant(-1, out _), Is.False);
            Assert.That(table.TryOccupant(99, out _), Is.False);
        }

        [Test]
        public void AMatchWithNoSeatsSeatsNobody()
        {
            var table = new SeatTable(0);

            Assert.That(table.Full, Is.True);
            Assert.That(table.Claim(1), Is.EqualTo(SeatTable.NoSeat));
        }
    }
}
