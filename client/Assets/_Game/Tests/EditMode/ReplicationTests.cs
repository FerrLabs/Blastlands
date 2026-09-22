using System;
using Blastlands.Core.Net;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    // The loop the netcode exists for, end to end: inputs leave as InputCodec bytes, the
    // server plays them and writes a snapshot, and both clients rebuild the board from
    // those bytes with packets lost, late and reordered on the way.
    //
    // Every piece of that has a test of its own. What this covers is the seam between
    // them, where a failure is two players watching different matches with nothing
    // logged anywhere. The socket is the part that is not here: see ReplicationSession.
    public class ReplicationTests
    {
        private const uint ServerSeed = 20260922u;

        [Test]
        public void BothClientsRebuildEveryTickTheServerPlayed()
        {
            var brains = new BotBrain[ReplicationSession.Clients];
            for (int i = 0; i < brains.Length; i++)
            {
                brains[i] = new BotBrain(i, BotSettings.Hard);
            }

            // Answered from what each client can see rather than from the server's board,
            // which is all a player ever has. Two clients deciding on two slightly
            // different pictures is what the snapshot stream is there to reconcile.
            var session = new ReplicationSession(ServerSeed, (seat, seen, tick) => brains[seat].Think(seen), 4102);

            session.Play(400, true);
            Assert.That(session.Server.Outcome, Is.EqualTo(RoundOutcome.Running), "the match ended before the test did");
            session.Settle(10);

            string server = ReplicationSession.Fingerprint(session.Server);
            for (int client = 0; client < ReplicationSession.Clients; client++)
            {
                Assert.That(session.Applied(client), Is.EqualTo(session.Server.Tick), $"client {client} is behind");
                Assert.That(
                    ReplicationSession.Fingerprint(session.Watchers[client]),
                    Is.EqualTo(server),
                    $"client {client} sees another match");
            }

            Assert.That(session.Rebuilt, Is.GreaterThan(300), "too few snapshots landed for this to have proved anything");
        }

        [Test]
        public void AClientEditingItsOwnBoardChangesNothingForAnybody()
        {
            // A client moving its own character, handing itself fire range and putting a
            // wall back has to leave the server untouched, and be gone from its own board
            // on the next snapshot. The session asserts the second half as it applies
            // each one; this is the first half.
            MatchState honest = Run(false);
            MatchState forged = Run(true);

            Assert.That(
                ReplicationSession.Fingerprint(forged),
                Is.EqualTo(ReplicationSession.Fingerprint(honest)),
                "a client edit reached the server");
        }

        [Test]
        public void HostileInputBytesCannotMoveAPlayerFurtherThanALegalStep()
        {
            // Nine bytes of noise per tick, with only the tick field made valid so the
            // packet is taken seriously at all. There is nowhere in those bytes to write
            // a position, and what they do carry is decoded through PlayerInput, so the
            // most a client can ask for is a stick held all the way over.
            MatchState state = MatchFactory.Create(
                ArenaSettings.Default, MatchSettings.Default, ReplicationSession.Seats, ServerSeed);
            var buffer = new InputBuffer();
            var inputs = new PlayerInput[ReplicationSession.Seats];
            var bytes = new byte[InputCodec.Size];
            var noise = new Random(20260922);

            SubPos before = state.Players[0].Position;

            for (int played = 0; played < 300 && state.Outcome == RoundOutcome.Running; played++)
            {
                int tick = state.Tick;
                noise.NextBytes(bytes);
                bytes[0] = (byte)tick;
                bytes[1] = (byte)(tick >> 8);
                bytes[2] = (byte)(tick >> 16);
                bytes[3] = (byte)(tick >> 24);

                Assert.That(InputCodec.TryRead(bytes, 0, out int sent, out PlayerInput hostile), Is.True);
                Assert.That(sent, Is.EqualTo(tick));
                buffer.Offer(sent, hostile);

                inputs[0] = buffer.Take(tick);
                for (int seat = 1; seat < inputs.Length; seat++)
                {
                    inputs[seat] = PlayerInput.None;
                }

                MatchSim.Tick(state, inputs);

                SubPos now = state.Players[0].Position;
                int moved = Math.Abs(now.X - before.X) + Math.Abs(now.Y - before.Y);
                Assert.That(moved, Is.LessThanOrEqualTo(SubPos.UnitsPerTile), $"the player jumped at tick {tick}");
                Assert.That(state.Arena.Contains(now.Tile), Is.True, $"the player left the arena at tick {tick}");
                before = now;
            }
        }

        private static MatchState Run(bool forge)
        {
            var session = new ReplicationSession(ServerSeed, (seat, seen, tick) => Scripted(seat, tick), 7717);

            session.Play(200, true);
            if (forge)
            {
                session.Forge(0);
            }

            session.Play(200, true);
            session.Settle(10);
            return session.Server;
        }

        // Fixed rather than read off the board, so both runs send the server the same
        // stream whatever the forging client believes it is looking at. A bot would
        // answer the forged picture and the two servers would part company honestly.
        private static PlayerInput Scripted(int seat, int tick)
        {
            Direction[] ring = { Direction.Right, Direction.Down, Direction.Left, Direction.Up };
            return new PlayerInput(
                ring[((tick / 7) + (seat * 2)) % ring.Length],
                (tick + (seat * 11)) % 23 == 0);
        }
    }
}
