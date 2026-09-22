using System;
using System.Collections.Generic;
using Blastlands.Core.Net;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class ClientPredictionTests
    {
        private const int Seat = 0;
        private const int SnapshotDelay = 3;

        private static MatchState Board()
        {
            var state = new MatchState(new Arena(15, 9), MatchSettings.Classic, 1u);
            state.Arena[new GridPos(6, 1)] = TileKind.HardBlock;
            state.Arena[new GridPos(3, 4)] = TileKind.HardBlock;
            state.AddPlayer(new GridPos(1, 1));
            state.AddPlayer(new GridPos(13, 7));
            return state;
        }

        private sealed class Session
        {
            private readonly Queue<KeyValuePair<int, byte[]>> inFlight = new Queue<KeyValuePair<int, byte[]>>();
            private readonly InputBuffer serverInputs = new InputBuffer();
            private readonly PlayerInput[] tickInputs = { PlayerInput.None, PlayerInput.None };
            private readonly MatchState arrived = Board();

            public Session()
            {
                Server = Board();
                Prediction = new ClientPrediction(Board(), Seat);
                Prediction.Reconcile(Server);
            }

            public MatchState Server { get; }

            public ClientPrediction Prediction { get; }

            public readonly List<SubPos> ServerPositions = new List<SubPos>();

            public void Send(int tick, PlayerInput input, bool arrives = true)
            {
                Prediction.Step(tick, input);
                if (arrives)
                {
                    serverInputs.Offer(tick, input);
                }
            }

            public void ServerPlays(Func<PlayerInput, PlayerInput> tamper = null)
            {
                PlayerInput input = serverInputs.Take(Server.Tick);
                tickInputs[Seat] = tamper == null ? input : tamper(input);
                MatchSim.Tick(Server, tickInputs);
                ServerPositions.Add(Server.Players[Seat].Position);

                var bytes = new byte[SnapshotCodec.MaxSize];
                int size = SnapshotCodec.Write(Server, bytes);
                Array.Resize(ref bytes, size);
                inFlight.Enqueue(new KeyValuePair<int, byte[]>(Server.Tick, bytes));
            }

            public bool Deliver(int clientTick)
            {
                bool any = false;
                while (inFlight.Count > 0 && inFlight.Peek().Key <= clientTick - SnapshotDelay)
                {
                    byte[] bytes = inFlight.Dequeue().Value;
                    Assert.That(SnapshotCodec.TryApply(bytes, arrived), Is.True);
                    any |= Prediction.Reconcile(arrived);
                }

                return any;
            }
        }

        private static PlayerInput Script(int tick)
        {
            if (tick == 4)
            {
                return new PlayerInput(Direction.Right, true);
            }

            if (tick < 40)
            {
                return PlayerInput.Moving(Direction.Right);
            }

            return tick < 70 ? PlayerInput.Moving(Direction.Down) : PlayerInput.Moving(Direction.Left);
        }

        [Test]
        public void WithNothingElseInTheWayThePredictionIsWhereTheServerWillPutThePlayer()
        {
            var session = new Session();

            for (int tick = 0; tick < 100; tick++)
            {
                session.Send(tick, Script(tick));
                session.ServerPlays();
                session.Deliver(tick);

                Assert.That(session.Prediction.Position, Is.EqualTo(session.ServerPositions[tick]), $"tick {tick}");
                Assert.That(session.Prediction.State.Bombs.Count, Is.EqualTo(session.Server.Bombs.Count), $"bombs at tick {tick}");
            }

            Assert.That(session.Server.Players[Seat].Position, Is.Not.EqualTo(SubPos.AtTileCentre(new GridPos(1, 1))));
        }

        [Test]
        public void TheBombIsOnTheBoardTheTickItIsDroppedNotARoundTripLater()
        {
            var session = new Session();

            for (int tick = 0; tick <= 4; tick++)
            {
                session.Send(tick, Script(tick));
            }

            Assert.That(session.Prediction.State.Bombs.Count, Is.EqualTo(1));
        }

        [Test]
        public void ADisagreementIsRewoundAndReplayedUntilThePredictionMatchesTheServerAgain()
        {
            var session = new Session();
            const int From = 10;
            const int Until = 16;

            for (int tick = 0; tick < 90; tick++)
            {
                session.Send(tick, Script(tick));
                session.ServerPlays(input => tick >= From && tick < Until ? PlayerInput.None : input);
                session.Deliver(tick);

                bool corrected = tick >= Until + SnapshotDelay;
                if (corrected)
                {
                    Assert.That(session.Prediction.Position, Is.EqualTo(session.ServerPositions[tick]), $"tick {tick}");
                }
                else if (tick >= From)
                {
                    Assert.That(session.Prediction.Position, Is.Not.EqualTo(session.ServerPositions[tick]), $"tick {tick}");
                }
            }
        }

        [Test]
        public void ATickTheClientSkippedIsFilledTheWayTheServerFillsIt()
        {
            var session = new Session();

            for (int tick = 0; tick < 30; tick++)
            {
                bool skipped = tick % 4 == 1;
                if (!skipped)
                {
                    session.Send(tick, PlayerInput.Moving(Direction.Right));
                }

                session.ServerPlays();

                if (!skipped)
                {
                    Assert.That(session.Prediction.Position, Is.EqualTo(session.ServerPositions[tick]), $"tick {tick}");
                }
            }
        }

        [Test]
        public void NothingIsPredictedBeforeTheFirstSnapshot()
        {
            MatchState predicted = Board();
            var prediction = new ClientPrediction(predicted, Seat);

            prediction.Step(0, PlayerInput.Moving(Direction.Right));

            Assert.That(prediction.Ready, Is.False);
            Assert.That(predicted.Tick, Is.EqualTo(0));
            Assert.That(prediction.Position, Is.EqualTo(SubPos.AtTileCentre(new GridPos(1, 1))));
        }
    }
}
