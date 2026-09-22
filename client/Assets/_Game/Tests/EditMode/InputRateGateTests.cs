using Blastlands.Core.Net;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class InputRateGateTests
    {
        private const int Seat = 0;

        [Test]
        public void AClientSendingOnePacketATickIsNeverRefusedNorDropped()
        {
            var gate = new InputRateGate(1);

            for (int tick = 0; tick < 3000; tick++)
            {
                Assert.That(gate.Admit(Seat), Is.True, $"refused at tick {tick}");
                Assert.That(gate.EndTicks(Seat, 1), Is.False, $"dropped at tick {tick}");
            }
        }

        [Test]
        public void ASecondOfMissedTicksSentAtOnceAllGetsThrough()
        {
            var gate = new InputRateGate(1);

            for (int i = 0; i < InputRateGate.DefaultBurst; i++)
            {
                Assert.That(gate.Admit(Seat), Is.True, $"packet {i} of the burst was refused");
            }

            Assert.That(gate.EndTicks(Seat, 1), Is.False);
        }

        [Test]
        public void PacketsBeyondTheBudgetAreRefused()
        {
            var gate = new InputRateGate(1, 4, 2, 30);

            for (int i = 0; i < 4; i++)
            {
                gate.Admit(Seat);
            }

            Assert.That(gate.Admit(Seat), Is.False);
            gate.EndTicks(Seat, 1);
            Assert.That(gate.Admit(Seat), Is.True);
            Assert.That(gate.Admit(Seat), Is.True);
            Assert.That(gate.Admit(Seat), Is.False);
        }

        [Test]
        public void AClientAtAHundredTimesTheTickRateIsDroppedAfterThePatienceWindow()
        {
            var gate = new InputRateGate(1);
            int droppedAt = -1;

            for (int tick = 0; tick < 200 && droppedAt < 0; tick++)
            {
                for (int i = 0; i < 100; i++)
                {
                    gate.Admit(Seat);
                }

                if (gate.EndTicks(Seat, 1))
                {
                    droppedAt = tick;
                }
            }

            Assert.That(droppedAt, Is.EqualTo(InputRateGate.DefaultPatienceTicks - 1));
        }

        [Test]
        public void AFloodIsStillDroppedWhenTheServerDrainsSeveralTicksPerFrame()
        {
            var gate = new InputRateGate(1);
            int frames = 0;
            bool dropped = false;

            while (!dropped && frames < 100)
            {
                for (int i = 0; i < 150; i++)
                {
                    gate.Admit(Seat);
                }

                dropped = gate.EndTicks(Seat, frames % 2 == 0 ? 1 : 2);
                frames++;
            }

            Assert.That(dropped, Is.True, "a slow server never disconnected the flooder");
            Assert.That(frames, Is.EqualTo(20));
        }

        [Test]
        public void AnHonestClientSurvivesALowFrameRate()
        {
            var gate = new InputRateGate(1);

            for (int frame = 0; frame < 600; frame++)
            {
                for (int i = 0; i < 3; i++)
                {
                    Assert.That(gate.Admit(Seat), Is.True, $"refused at frame {frame}");
                }

                Assert.That(gate.EndTicks(Seat, 3), Is.False);
            }
        }

        [Test]
        public void NoTimePassingChangesNothing()
        {
            var gate = new InputRateGate(1, 4, 2, 1);

            for (int i = 0; i < 10; i++)
            {
                gate.Admit(Seat);
            }

            Assert.That(gate.EndTicks(Seat, 0), Is.False);
            Assert.That(gate.EndTicks(Seat, 1), Is.True);
        }

        [Test]
        public void AFloodThatStopsBeforeThePatienceRunsOutIsForgiven()
        {
            var gate = new InputRateGate(1, 4, 2, 10);

            for (int tick = 0; tick < 9; tick++)
            {
                for (int i = 0; i < 50; i++)
                {
                    gate.Admit(Seat);
                }

                Assert.That(gate.EndTicks(Seat, 1), Is.False);
            }

            Assert.That(gate.EndTicks(Seat, 1), Is.False);

            for (int tick = 0; tick < 9; tick++)
            {
                for (int i = 0; i < 50; i++)
                {
                    gate.Admit(Seat);
                }

                Assert.That(gate.EndTicks(Seat, 1), Is.False, $"the earlier flood still counted at tick {tick}");
            }
        }

        [Test]
        public void OneSeatFloodingDoesNotSpendAnotherSeatsBudget()
        {
            var gate = new InputRateGate(2);

            for (int tick = 0; tick < 100; tick++)
            {
                for (int i = 0; i < 100; i++)
                {
                    gate.Admit(0);
                }

                Assert.That(gate.Admit(1), Is.True);
                gate.EndTicks(0, 1);
                Assert.That(gate.EndTicks(1, 1), Is.False);
            }
        }

        [Test]
        public void ASeatHandedToANewConnectionStartsWithAFullBudget()
        {
            var gate = new InputRateGate(1, 4, 2, 3);

            for (int tick = 0; tick < 2; tick++)
            {
                for (int i = 0; i < 50; i++)
                {
                    gate.Admit(Seat);
                }

                gate.EndTicks(Seat, 1);
            }

            gate.Reset(Seat);

            for (int i = 0; i < 4; i++)
            {
                Assert.That(gate.Admit(Seat), Is.True);
            }

            Assert.That(gate.EndTicks(Seat, 1), Is.False);
            Assert.That(gate.EndTicks(Seat, 1), Is.False);
            Assert.That(gate.EndTicks(Seat, 1), Is.False);
        }
    }
}
