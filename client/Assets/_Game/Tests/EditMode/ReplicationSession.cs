using System;
using System.Collections.Generic;
using System.Text;
using Blastlands.Core.Net;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    // One match played the way ServerLoop plays it, with two clients on the other end of
    // a written-out network: a packet is a copy of the bytes, an arrival tick and
    // nothing else. Loss, latency and reordering are the point, because that is what a
    // snapshot stream lives with.
    //
    // No socket and no Netcode for GameObjects: what runs here is the codecs, the input
    // buffers and the simulation, which is the part of the loop that can be tested
    // without an editor.
    internal sealed class ReplicationSession
    {
        public const int Seats = 4;
        public const int Clients = 2;

        // What NetworkedMatchDriver sends with, and the reason a late packet is ordinary
        // rather than exceptional.
        private const int LeadTicks = 5;

        private const int LossPercent = 12;

        private readonly InputBuffer[] buffers = new InputBuffer[Seats];
        private readonly PlayerInput[] inputs = new PlayerInput[Seats];
        private readonly List<Packet> inputsInFlight = new List<Packet>();
        private readonly List<Packet> snapshotsInFlight = new List<Packet>();
        private readonly Dictionary<int, string> served = new Dictionary<int, string>();
        private readonly byte[] inputBytes = new byte[InputCodec.Size];
        private readonly byte[] snapshotBytes = new byte[SnapshotCodec.MaxSize];
        private readonly int[] nextTick = new int[Clients];
        private readonly int[] applied = new int[Clients];
        private readonly Func<int, MatchState, int, PlayerInput> choose;
        private readonly Random network;

        public ReplicationSession(uint seed, Func<int, MatchState, int, PlayerInput> chooseInput, int networkSeed)
        {
            Server = MatchFactory.Create(ArenaSettings.Default, MatchSettings.Default, Seats, seed);
            choose = chooseInput;
            network = new Random(networkSeed);

            for (int seat = 0; seat < Seats; seat++)
            {
                buffers[seat] = new InputBuffer();
            }

            // Built on a seed of their own, as a real client is: it is told the size of
            // the board and how many players it seats, and every tile it ends up drawing
            // arrives in a snapshot.
            Watchers = new MatchState[Clients];
            for (int client = 0; client < Clients; client++)
            {
                Watchers[client] = MatchFactory.Create(
                    ArenaSettings.Default, MatchSettings.Default, Seats, (uint)(client + 1));
                nextTick[client] = -1;
                applied[client] = -1;
            }
        }

        public MatchState Server { get; }

        public MatchState[] Watchers { get; }

        public int Rebuilt { get; private set; }

        public int Applied(int client)
        {
            return applied[client];
        }

        public void Play(int ticks, bool lossy)
        {
            for (int i = 0; i < ticks && Server.Outcome == RoundOutcome.Running; i++)
            {
                Step(lossy);
            }
        }

        // A calm stretch at the end, then whatever is still in the air. A client that
        // lost the last snapshot of a lossy run is behind by one tick for reasons that
        // say nothing about the codec.
        public void Settle(int ticks)
        {
            Play(ticks, false);

            snapshotsInFlight.Sort((left, right) => left.Tick.CompareTo(right.Tick));
            for (int i = 0; i < snapshotsInFlight.Count; i++)
            {
                Apply(snapshotsInFlight[i]);
            }

            snapshotsInFlight.Clear();
        }

        // The forgery the wire format makes impossible, done where it is still possible:
        // in the client's own copy of the match.
        public void Forge(int client)
        {
            MatchState state = Watchers[client];
            PlayerState player = state.Players[client];

            player.Position = SubPos.AtTileCentre(new GridPos(state.Arena.Width - 2, state.Arena.Height - 2));
            player.FireRange = 9;
            player.SpeedSteps = MatchSettings.Default.MaxSpeedSteps;
            player.Alive = true;
            state.Arena[new GridPos(1, 1)] = TileKind.SoftBlock;
        }

        // Everything a snapshot claims to carry, in one string, so a divergence anywhere
        // fails rather than only in the fields somebody thought to compare.
        public static string Fingerprint(MatchState state)
        {
            var text = new StringBuilder();
            text.Append(state.Tick).Append(' ').Append((int)state.Outcome).Append(' ')
                .Append(state.WinnerId).Append(' ').Append(state.SuddenDeathRings).Append('\n');

            for (int y = 0; y < state.Arena.Height; y++)
            {
                for (int x = 0; x < state.Arena.Width; x++)
                {
                    text.Append((int)state.Arena[new GridPos(x, y)]).Append(',');
                }
            }

            text.Append('\n');

            foreach (PlayerState player in state.Players)
            {
                text.Append(player.Id).Append(' ').Append(player.Position.X).Append(' ')
                    .Append(player.Position.Y).Append(' ').Append(player.Alive).Append(' ')
                    .Append(player.IsBot).Append(' ').Append(player.BombsHeld).Append(' ')
                    .Append(player.CarryCapacity).Append(' ').Append(player.FireRange).Append(' ')
                    .Append(player.SpeedSteps).Append(' ').Append((int)player.NextBombKind).Append(' ')
                    .Append((int)player.Facing).Append(' ').Append(player.DashTicksRemaining).Append(' ')
                    .Append(player.StunTicksRemaining).Append(' ').Append(player.RevealTicksRemaining).Append(' ')
                    .Append((int)player.Character).Append(' ').Append(player.AbilityCooldownRemaining)
                    .Append(' ').Append(player.VanishTicksRemaining)
                    .Append('\n');
            }

            foreach (ActiveBomb bomb in state.Bombs)
            {
                text.Append(bomb.Bomb.Position).Append(' ').Append(bomb.Bomb.OwnerId).Append(' ')
                    .Append(bomb.Bomb.FireRange).Append(' ').Append((int)bomb.Bomb.Kind).Append(' ')
                    .Append(bomb.FuseTicks).Append(' ').Append(bomb.FuseRemaining).Append('\n');
            }

            foreach (ActiveFlame flame in state.Flames)
            {
                text.Append(flame.Tile).Append(' ').Append(flame.TicksRemaining).Append(' ')
                    .Append(flame.SpawnedTick).Append('\n');
            }

            foreach (PowerUp powerUp in state.PowerUps)
            {
                text.Append(powerUp.Tile).Append(' ').Append((int)powerUp.Kind).Append(' ')
                    .Append(powerUp.RevealedTick).Append('\n');
            }

            foreach (GridPos loose in state.LooseBombs)
            {
                text.Append(loose).Append('\n');
            }

            foreach (WallRegrowth wall in state.RegrowingWalls)
            {
                text.Append(wall.Tile).Append(' ').Append((int)wall.Kind).Append(' ')
                    .Append(wall.TicksRemaining).Append('\n');
            }

            foreach (RaisedWall wall in state.RaisedWalls)
            {
                text.Append(wall.Tile).Append(' ').Append(wall.TicksRemaining).Append('\n');
            }

            return text.ToString();
        }

        private void Step(bool lossy)
        {
            SendInputs(lossy);
            Deliver(inputsInFlight, false);

            for (int seat = 0; seat < Seats; seat++)
            {
                inputs[seat] = buffers[seat].Take(Server.Tick);
            }

            MatchSim.Tick(Server, inputs);
            served[Server.Tick] = Fingerprint(Server);

            Broadcast(lossy);
            Deliver(snapshotsInFlight, true);
        }

        private void SendInputs(bool lossy)
        {
            for (int client = 0; client < Clients; client++)
            {
                // Nothing goes out before the first snapshot: until one lands there is no
                // clock to answer.
                if (applied[client] < 0)
                {
                    continue;
                }

                if (nextTick[client] <= applied[client])
                {
                    nextTick[client] = applied[client] + LeadTicks;
                }

                PlayerInput input = choose(client, Watchers[client], nextTick[client]);
                Assert.That(InputCodec.TryWrite(inputBytes, 0, nextTick[client], input), Is.True);
                Queue(inputsInFlight, client, nextTick[client], inputBytes, InputCodec.Size, lossy, 1, 3);
                nextTick[client]++;
            }
        }

        private void Broadcast(bool lossy)
        {
            int size = SnapshotCodec.Write(Server, snapshotBytes);
            Assert.That(size, Is.GreaterThan(0), $"the snapshot for tick {Server.Tick} did not fit");

            for (int client = 0; client < Clients; client++)
            {
                Queue(snapshotsInFlight, client, Server.Tick, snapshotBytes, size, lossy, 0, 2);
            }
        }

        private void Queue(
            List<Packet> flight, int owner, int tick, byte[] bytes, int length, bool lossy, int nearest, int furthest)
        {
            if (lossy && network.Next(100) < LossPercent)
            {
                return;
            }

            var copy = new byte[length];
            Array.Copy(bytes, copy, length);

            flight.Add(new Packet
            {
                Arrives = Server.Tick + (lossy ? network.Next(nearest, furthest + 1) : nearest),
                Owner = owner,
                Tick = tick,
                Bytes = copy,
                Length = length
            });
        }

        private void Deliver(List<Packet> flight, bool snapshots)
        {
            for (int i = flight.Count - 1; i >= 0; i--)
            {
                Packet packet = flight[i];
                if (packet.Arrives > Server.Tick)
                {
                    continue;
                }

                flight.RemoveAt(i);

                if (snapshots)
                {
                    Apply(packet);
                    continue;
                }

                Assert.That(InputCodec.TryRead(packet.Bytes, 0, out int tick, out PlayerInput input), Is.True);
                buffers[packet.Owner].Offer(tick, input);
            }
        }

        // The gate MatchTransport puts in front of the client: an older tick is an
        // ordinary reordering, and applying it would rewind the board.
        private void Apply(Packet packet)
        {
            if (packet.Tick <= applied[packet.Owner])
            {
                return;
            }

            MatchState watcher = Watchers[packet.Owner];
            Assert.That(
                SnapshotCodec.TryApply(packet.Bytes, packet.Length, watcher),
                Is.True,
                $"client {packet.Owner} refused the snapshot for tick {packet.Tick}");

            applied[packet.Owner] = watcher.Tick;
            Rebuilt++;

            Assert.That(
                Fingerprint(watcher),
                Is.EqualTo(served[packet.Tick]),
                $"client {packet.Owner} rebuilt tick {packet.Tick} as another board");
        }

        private struct Packet
        {
            public int Arrives;
            public int Owner;
            public int Tick;
            public byte[] Bytes;
            public int Length;
        }
    }
}
