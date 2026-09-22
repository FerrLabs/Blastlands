using Blastlands.Core.Net;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    // What a client is allowed to say, and what happens when it says nothing. Both
    // halves fail quietly when they fail: a mis-packed field is a player who moves
    // slightly wrong, and a mishandled gap is a stutter blamed on the network.
    public class InputWireTests
    {
        private const int Range = StickReader.Range;

        [Test]
        public void AnInputSurvivesTheRoundTrip()
        {
            var buffer = new byte[InputCodec.Size];
            var sent = new PlayerInput(Range, -Range, true, false, true);

            Assert.That(InputCodec.TryWrite(buffer, 0, 4242, sent), Is.True);
            Assert.That(InputCodec.TryRead(buffer, 0, out int tick, out PlayerInput got), Is.True);

            Assert.That(tick, Is.EqualTo(4242));
            Assert.That(got.MoveX, Is.EqualTo(sent.MoveX));
            Assert.That(got.MoveY, Is.EqualTo(sent.MoveY));
            Assert.That(got.DropBomb, Is.True);
            Assert.That(got.Dash, Is.False);
            Assert.That(got.Push, Is.True);
        }

        [Test]
        public void EachFlagTravelsOnItsOwn()
        {
            // Three bits in one byte is exactly where a copied line puts the wrong mask
            // twice, and the result is a player who dashes when they meant to shove.
            var buffer = new byte[InputCodec.Size];

            foreach (PlayerInput expected in new[]
                     {
                         new PlayerInput(0, 0, true, false, false),
                         new PlayerInput(0, 0, false, true, false),
                         new PlayerInput(0, 0, false, false, true)
                     })
            {
                InputCodec.TryWrite(buffer, 0, 1, expected);
                InputCodec.TryRead(buffer, 0, out _, out PlayerInput got);

                Assert.That(got.DropBomb, Is.EqualTo(expected.DropBomb));
                Assert.That(got.Dash, Is.EqualTo(expected.Dash));
                Assert.That(got.Push, Is.EqualTo(expected.Push));
            }
        }

        [Test]
        public void MovementBeyondWhatAStickCanDoIsClampedOnArrival()
        {
            // The security one. The server must not be the thing that remembers to check
            // this, so decoding goes through the PlayerInput constructor, and a client
            // claiming a stick thirty times the size of a stick moves at everybody else's
            // pace.
            var buffer = new byte[InputCodec.Size];
            InputCodec.TryWrite(buffer, 0, 1, PlayerInput.None);

            buffer[4] = 0xFF;
            buffer[5] = 0x7F;
            buffer[6] = 0x00;
            buffer[7] = 0x80;

            Assert.That(InputCodec.TryRead(buffer, 0, out _, out PlayerInput got), Is.True);
            Assert.That(got.MoveX, Is.EqualTo(Range));
            Assert.That(got.MoveY, Is.EqualTo(-Range));
        }

        [Test]
        public void AShortBufferIsRefusedRatherThanReadPast()
        {
            var tooSmall = new byte[InputCodec.Size - 1];

            Assert.That(InputCodec.TryWrite(tooSmall, 0, 1, PlayerInput.None), Is.False);
            Assert.That(InputCodec.TryRead(tooSmall, 0, out _, out _), Is.False);
            Assert.That(InputCodec.TryRead(new byte[InputCodec.Size], 1, out _, out _), Is.False);
            Assert.That(InputCodec.TryRead(null, 0, out _, out _), Is.False);
        }

        [Test]
        public void ATickBeforeTheMatchStartedIsRefused()
        {
            // Negative ticks index arrays. Reading one as a slot number is how a bad
            // packet becomes an exception in the middle of a match.
            var buffer = new byte[InputCodec.Size];
            InputCodec.TryWrite(buffer, 0, 0, PlayerInput.None);
            buffer[3] = 0x80;

            Assert.That(InputCodec.TryRead(buffer, 0, out _, out _), Is.False);
        }

        [Test]
        public void TheBytesAreTheSameOnEveryMachine()
        {
            // Pinned rather than round-tripped, because a round trip passes just as well
            // when both ends are wrong together. A server and a client built for
            // different architectures have to agree, and BitConverter would not.
            var buffer = new byte[InputCodec.Size];
            InputCodec.TryWrite(buffer, 0, 0x01020304, new PlayerInput(1000, -1000, true, false, true));

            Assert.That(
                buffer,
                Is.EqualTo(new byte[] { 0x04, 0x03, 0x02, 0x01, 0xE8, 0x03, 0x18, 0xFC, 0x05 }));
        }

        [Test]
        public void ASeatStopsAnsweringOnlyAfterAFewSecondsOfSilence()
        {
            var inputs = new InputBuffer();
            int tick = 1;

            for (; tick < InputBuffer.SilenceBeforeStandIn; tick++)
            {
                inputs.Take(tick);
                Assert.That(inputs.Answering, Is.True, $"handed over after {tick} silent ticks");
            }

            inputs.Take(tick);
            Assert.That(inputs.Answering, Is.False);
        }

        [Test]
        public void ASeatAnswersAgainOnlyOnceItsInputsAreSteady()
        {
            InputBuffer inputs = Silenced(out int tick);

            for (int i = 0; i < 60; i++, tick++)
            {
                if (i % 20 < 10)
                {
                    inputs.Offer(tick, PlayerInput.None);
                }

                inputs.Take(tick);
                Assert.That(inputs.Answering, Is.False, $"half the inputs missing handed the seat back at {i}");
            }
        }

        [Test]
        public void OrdinaryLossDoesNotKeepTheBotDriving()
        {
            InputBuffer inputs = Silenced(out int tick);

            int taken = 0;
            while (!inputs.Answering && taken < 60)
            {
                if (taken % 7 != 3)
                {
                    inputs.Offer(tick, PlayerInput.None);
                }

                inputs.Take(tick++);
                taken++;
            }

            Assert.That(inputs.Answering, Is.True, "one input in seven lost kept the bot driving");
            Assert.That(taken, Is.LessThanOrEqualTo(InputBuffer.SteadyWindow));
        }

        private static InputBuffer Silenced(out int tick)
        {
            var inputs = new InputBuffer();
            for (tick = 1; tick <= InputBuffer.SilenceBeforeStandIn; tick++)
            {
                inputs.Take(tick);
            }

            Assert.That(inputs.Answering, Is.False);
            return inputs;
        }

        [Test]
        public void AMissingInputRepeatsTheLastOneRatherThanStopping()
        {
            // The rule the whole buffer exists for. Stalling a tick would hand every
            // player in the match a stutter caused by one bad connection.
            var buffer = new InputBuffer();
            var held = new PlayerInput(Range, 0, false, false, false);

            buffer.Offer(0, held);

            Assert.That(buffer.Take(0).MoveX, Is.EqualTo(Range));
            Assert.That(buffer.Take(1).MoveX, Is.EqualTo(Range), "a gap stopped the player dead");
            Assert.That(buffer.Take(2).MoveX, Is.EqualTo(Range));
        }

        [Test]
        public void APlayerWhoHasSaidNothingYetStandsStill()
        {
            var buffer = new InputBuffer();

            PlayerInput first = buffer.Take(0);

            Assert.That(first.IsMoving, Is.False);
            Assert.That(first.DropBomb, Is.False);
        }

        [Test]
        public void AnInputForATickAlreadyPlayedIsDropped()
        {
            // It lost a race it was always going to lose. Applying it late would move a
            // player on the strength of a decision they made in the past.
            var buffer = new InputBuffer();
            buffer.Take(5);

            Assert.That(buffer.Offer(5, new PlayerInput(Range, 0, false, false, false)), Is.False);
            Assert.That(buffer.Offer(3, new PlayerInput(Range, 0, false, false, false)), Is.False);
            Assert.That(buffer.Take(6).IsMoving, Is.False, "a stale input was applied anyway");
        }

        [Test]
        public void AnInputTooFarAheadIsDropped()
        {
            // A client cannot make the server hold packets for it. Everything past the
            // window is refused rather than queued. Nothing has been played yet here, so
            // that window is the four ticks from zero.
            var buffer = new InputBuffer(4);

            Assert.That(buffer.Offer(3, PlayerInput.None), Is.True, "the last slot in the window");
            Assert.That(buffer.Offer(4, PlayerInput.None), Is.False, "one past the window");
            Assert.That(buffer.Offer(9999, PlayerInput.None), Is.False);

            // The window travels with the tick being played rather than staying put.
            buffer.Take(0);
            Assert.That(buffer.Offer(4, PlayerInput.None), Is.True, "the window did not move on");
            Assert.That(buffer.Offer(5, PlayerInput.None), Is.False);
        }

        [Test]
        public void AnInputIsUsedOnceAndNotAgainWhenTheRingWrapsOntoIt()
        {
            // The slot for tick 1 is the slot for tick 5 when the ring holds four. Left in
            // place, the first would be read a second time and the player would move on a
            // decision they made four ticks ago without asking.
            var buffer = new InputBuffer(4);
            buffer.Offer(1, new PlayerInput(Range, 0, false, false, false));

            Assert.That(buffer.Take(1).MoveX, Is.EqualTo(Range));

            // Offered only now: the window runs four ticks from the last one played, so
            // tick 5 is out of reach until tick 1 has been taken.
            buffer.Offer(5, new PlayerInput(0, Range, false, false, false));

            for (int tick = 2; tick <= 4; tick++)
            {
                Assert.That(buffer.Take(tick).MoveX, Is.EqualTo(Range), "the repeat lost the direction");
            }

            Assert.That(buffer.Take(5).MoveY, Is.EqualTo(Range), "tick 5 read tick 1 out of the same slot");
        }

        [Test]
        public void ARepeatKeepsTheDirectionAndDropsTheButtons()
        {
            // MatchSim reads all three buttons as level-triggered, so a repeat that kept
            // them would spend bombs and dash charges on ticks the player never sent: a
            // dropped packet would cost them inventory. A held direction costs nothing,
            // because that is what holding a key already looks like.
            var buffer = new InputBuffer();
            buffer.Offer(0, new PlayerInput(Range, 0, true, true, true));

            PlayerInput sent = buffer.Take(0);
            Assert.That(sent.DropBomb, Is.True, "the input that did arrive lost its buttons");
            Assert.That(sent.Dash, Is.True);
            Assert.That(sent.Push, Is.True);

            PlayerInput repeated = buffer.Take(1);
            Assert.That(repeated.MoveX, Is.EqualTo(Range), "the repeat should still walk");
            Assert.That(repeated.DropBomb, Is.False, "a lost packet dropped a bomb");
            Assert.That(repeated.Dash, Is.False);
            Assert.That(repeated.Push, Is.False);
        }

        [Test]
        public void ArrivingOutOfOrderStillPlaysInOrder()
        {
            var buffer = new InputBuffer();
            var left = new PlayerInput(-Range, 0, false, false, false);
            var right = new PlayerInput(Range, 0, false, false, false);

            buffer.Offer(3, right);
            buffer.Offer(2, left);

            Assert.That(buffer.Take(2).MoveX, Is.EqualTo(-Range));
            Assert.That(buffer.Take(3).MoveX, Is.EqualTo(Range));
        }
    }
}
