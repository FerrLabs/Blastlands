using Blastlands.Core.Net;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    // The one message a client cannot recover from losing or misreading: it decides the
    // board every later snapshot is applied to, and a board of the wrong size refuses
    // every one of them silently.
    public class SeatCodecTests
    {
        private static byte[] Written(SeatAssignment assignment)
        {
            var buffer = new byte[SeatCodec.Size];
            Assert.That(SeatCodec.TryWrite(buffer, assignment), Is.True);
            return buffer;
        }

        [Test]
        public void SurvivesTheRoundTrip()
        {
            byte[] buffer = Written(new SeatAssignment(2, 25, 21, 4));

            Assert.That(SeatCodec.TryRead(buffer, SeatCodec.Size, out SeatAssignment read), Is.True);
            Assert.That(read.Seat, Is.EqualTo(2));
            Assert.That(read.Width, Is.EqualTo(25));
            Assert.That(read.Height, Is.EqualTo(21));
            Assert.That(read.Players, Is.EqualTo(4));
        }

        // Little-endian by hand rather than by BitConverter, so a server and a client on
        // different architectures read the same numbers. A test on the bytes is the only
        // thing that catches a switch to the host's order, because a round trip through
        // one machine passes either way.
        [Test]
        public void WritesLittleEndian()
        {
            byte[] buffer = Written(new SeatAssignment(1, 25, 21, 4));

            Assert.That(buffer[0], Is.EqualTo(1));
            Assert.That(buffer[1], Is.EqualTo(0));
            Assert.That(buffer[4], Is.EqualTo(25));
            Assert.That(buffer[5], Is.EqualTo(0));
            Assert.That(buffer[8], Is.EqualTo(21));
            Assert.That(buffer[12], Is.EqualTo(4));
        }

        [Test]
        public void RefusesAMessageCutShort()
        {
            byte[] buffer = Written(new SeatAssignment(0, 25, 21, 4));

            Assert.That(SeatCodec.TryRead(buffer, SeatCodec.Size - 1, out SeatAssignment _), Is.False);
        }

        // A seat outside the match would index past the player list the client is about
        // to build, so it is refused where it arrives rather than where it is used.
        [Test]
        public void RefusesASeatOutsideTheMatch()
        {
            var buffer = new byte[SeatCodec.Size];
            var writer = new NetWriter(buffer);
            writer.Int32(4);
            writer.Int32(25);
            writer.Int32(21);
            writer.Int32(4);

            Assert.That(SeatCodec.TryRead(buffer, SeatCodec.Size, out SeatAssignment _), Is.False);
        }

        // MatchFactory throws on a board this small. A client that trusted the number
        // would take itself down on a message from the network.
        [Test]
        public void RefusesABoardTooSmallToBuild()
        {
            var buffer = new byte[SeatCodec.Size];
            var writer = new NetWriter(buffer);
            writer.Int32(0);
            writer.Int32(ArenaSettings.SmallestSide - 1);
            writer.Int32(21);
            writer.Int32(4);

            Assert.That(SeatCodec.TryRead(buffer, SeatCodec.Size, out SeatAssignment _), Is.False);
        }

        [Test]
        public void RefusesToWriteWhatItWouldRefuseToRead()
        {
            var buffer = new byte[SeatCodec.Size];

            Assert.That(SeatCodec.TryWrite(buffer, new SeatAssignment(0, 25, 21, 0)), Is.False);
            Assert.That(SeatCodec.TryWrite(buffer, new SeatAssignment(-1, 25, 21, 4)), Is.False);
            Assert.That(SeatCodec.TryWrite(buffer, new SeatAssignment(4, 25, 21, 4)), Is.False);
            Assert.That(SeatCodec.TryWrite(buffer, new SeatAssignment(0, 4, 21, 4)), Is.False);
        }

        // MatchFactory throws above the spawns a board can hold, and that throw would
        // land on the message handler.
        [Test]
        public void RefusesMorePlayersThanAnyBoardCanSeat()
        {
            var buffer = new byte[SeatCodec.Size];
            var writer = new NetWriter(buffer);
            writer.Int32(0);
            writer.Int32(25);
            writer.Int32(21);
            writer.Int32(ArenaGenerator.MostSpawns + 1);

            Assert.That(SeatCodec.TryRead(buffer, SeatCodec.Size, out SeatAssignment _), Is.False);
        }
    }
}
