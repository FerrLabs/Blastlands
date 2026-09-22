using System;

namespace Blastlands.Core.Net
{
    public sealed class ClientPrediction
    {
        public const int DefaultCapacity = 64;

        private const int Empty = -1;

        private readonly MatchState predicted;
        private readonly int seat;
        private readonly PlayerInput[] sent;
        private readonly int[] sentTicks;
        private readonly PlayerInput[] tickInputs;
        private readonly byte[] copy = new byte[SnapshotCodec.MaxSize];
        private int newestSent = Empty;
        private int reconciledTick = Empty;

        public ClientPrediction(MatchState predicted, int seat, int capacity = DefaultCapacity)
        {
            if (predicted == null)
            {
                throw new ArgumentNullException(nameof(predicted));
            }

            if (seat < 0 || seat >= predicted.Players.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(seat));
            }

            this.predicted = predicted;
            this.seat = seat;
            sent = new PlayerInput[Math.Max(1, capacity)];
            sentTicks = new int[sent.Length];
            tickInputs = new PlayerInput[predicted.Players.Count];
            for (int i = 0; i < sentTicks.Length; i++)
            {
                sentTicks[i] = Empty;
            }
        }

        public MatchState State
        {
            get { return predicted; }
        }

        public bool Ready
        {
            get { return reconciledTick != Empty; }
        }

        public SubPos Position
        {
            get { return predicted.Players[seat].Position; }
        }

        public void Step(int tick, PlayerInput input)
        {
            if (!Ready || tick < predicted.Tick || tick <= newestSent)
            {
                return;
            }

            int slot = tick % sent.Length;
            sent[slot] = input;
            sentTicks[slot] = tick;
            newestSent = tick;

            ReplayTo(tick + 1);
        }

        public bool Reconcile(MatchState authoritative)
        {
            if (authoritative == null || authoritative.Tick <= reconciledTick)
            {
                return false;
            }

            int size = SnapshotCodec.Write(authoritative, copy);
            if (size <= 0 || !SnapshotCodec.TryApply(copy, size, predicted))
            {
                return false;
            }

            reconciledTick = authoritative.Tick;
            ReplayTo(newestSent + 1);
            return true;
        }

        private void ReplayTo(int target)
        {
            int limit = predicted.Tick + sent.Length;
            while (predicted.Tick < target && predicted.Tick < limit)
            {
                for (int i = 0; i < tickInputs.Length; i++)
                {
                    tickInputs[i] = PlayerInput.None;
                }

                tickInputs[seat] = InputFor(predicted.Tick);
                MatchSim.Tick(predicted, tickInputs);

                if (predicted.Outcome != RoundOutcome.Running)
                {
                    return;
                }
            }
        }

        private PlayerInput InputFor(int tick)
        {
            if (sentTicks[tick % sent.Length] == tick)
            {
                return sent[tick % sent.Length];
            }

            for (int back = tick - 1; back > tick - sent.Length && back >= 0; back--)
            {
                int slot = back % sent.Length;
                if (sentTicks[slot] == back)
                {
                    PlayerInput held = sent[slot];
                    return new PlayerInput(held.MoveX, held.MoveY, false, false, false);
                }
            }

            return PlayerInput.None;
        }
    }
}
