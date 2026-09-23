using System;
using Blastlands.Core.Net;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    // A snapshot is the only thing a client ever learns about the match, so a field that
    // does not survive the trip is a client rendering something the server never said.
    // None of that fails loudly: it looks like a bug in the game.
    public class SnapshotCodecTests
    {
        private static readonly MatchSettings Settings = MatchSettings.Default;

        // Played rather than built, and played to several depths, so the round trip is
        // checked against boards that actually contain bombs, flames, power-ups and
        // walls growing back. An opening position has none of them and would let a codec
        // that drops every list pass.
        private static MatchState Played(uint seed, int ticks)
        {
            MatchState state = MatchFactory.Create(ArenaSettings.Default, Settings, 4, seed);
            var brains = new BotBrain[state.Players.Count];
            for (int i = 0; i < brains.Length; i++)
            {
                brains[i] = new BotBrain(i, BotSettings.Hard);
            }

            var inputs = new PlayerInput[state.Players.Count];
            for (int tick = 0; tick < ticks; tick++)
            {
                for (int i = 0; i < brains.Length; i++)
                {
                    inputs[i] = brains[i].Think(state);
                }

                MatchSim.Tick(state, inputs);
            }

            return state;
        }

        private static MatchState Blank(uint seed)
        {
            return MatchFactory.Create(ArenaSettings.Default, Settings, 4, seed);
        }

        [Test]
        public void AWholeMatchStateSurvivesTheRoundTrip()
        {
            var buffer = new byte[SnapshotCodec.MaxSize];

            foreach (int depth in new[] { 1, 40, 200, 600, 1500 })
            {
                MatchState server = Played(7u, depth);
                MatchState client = Blank(7u);

                Assert.That(SnapshotCodec.Write(server, buffer), Is.GreaterThan(0), $"depth {depth}");
                Assert.That(SnapshotCodec.TryApply(buffer, client), Is.True, $"depth {depth}");

                AssertSame(server, client, depth);
            }
        }

        [Test]
        public void WhoIsABotSurvivesTheRoundTripBothWays()
        {
            var buffer = new byte[SnapshotCodec.MaxSize];
            MatchState server = Played(7u, 40);
            MatchState client = Blank(7u);
            server.Players[1].IsBot = true;
            server.Players[3].IsBot = true;

            Assert.That(SnapshotCodec.TryApply(buffer, SnapshotCodec.Write(server, buffer), client), Is.True);

            Assert.That(client.Players[0].IsBot, Is.False);
            Assert.That(client.Players[1].IsBot, Is.True);
            Assert.That(client.Players[2].IsBot, Is.False);
            Assert.That(client.Players[3].IsBot, Is.True);

            server.Players[1].IsBot = false;
            Assert.That(SnapshotCodec.TryApply(buffer, SnapshotCodec.Write(server, buffer), client), Is.True);

            Assert.That(client.Players[1].IsBot, Is.False, "a player who reconnected still reads as a bot");
            Assert.That(client.Players[3].IsBot, Is.True);
        }

        [Test]
        public void ATruncatedSnapshotIsRefusedAtEveryLength()
        {
            // Every cut, not one: a reader that checks its bounds in most places and not
            // one is a reader that works until the day the message is cut there.
            var buffer = new byte[SnapshotCodec.MaxSize];
            int used = SnapshotCodec.Write(Played(7u, 600), buffer);

            for (int cut = 0; cut < used; cut += 7)
            {
                var shortened = new byte[cut];
                Array.Copy(buffer, shortened, cut);

                Assert.That(
                    SnapshotCodec.TryApply(shortened, Blank(7u)),
                    Is.False,
                    $"a message cut at {cut} of {used} bytes was accepted");
            }
        }

        [Test]
        public void ACorruptSnapshotIsRefusedRatherThanThrowing()
        {
            // It may be refused or it may happen to stay valid. What it may not do is
            // take the process down: a packet being wrong is an ordinary event on a
            // network, and an exception here would end the match for everybody.
            var buffer = new byte[SnapshotCodec.MaxSize];
            int used = SnapshotCodec.Write(Played(7u, 600), buffer);
            var random = new Random(20260904);

            for (int trial = 0; trial < 500; trial++)
            {
                var corrupt = new byte[used];
                Array.Copy(buffer, corrupt, used);

                for (int hit = 0; hit < 3; hit++)
                {
                    corrupt[random.Next(used)] = (byte)random.Next(256);
                }

                // Spelled TestDelegate rather than passed inline, for the reason #122
                // records: NUnit 4 overloads this on both TestDelegate and Action, so a
                // bare lambda is ambiguous, while Unity ships 3.5 and compiles it
                // happily. The editor stays green and the dotnet build in CI does not.
                TestDelegate applying = () => SnapshotCodec.TryApply(corrupt, Blank(7u));
                Assert.DoesNotThrow(applying);
            }
        }

        [Test]
        public void ARefusedSnapshotLeavesTheStateAlone()
        {
            // Half applied is worse than dropped. A board whose bombs come from one tick
            // and whose flames come from another is a board that never existed.
            var buffer = new byte[SnapshotCodec.MaxSize];
            int used = SnapshotCodec.Write(Played(7u, 600), buffer);

            var rubbish = new byte[used];
            Array.Copy(buffer, rubbish, used);
            rubbish[used - 1] ^= 0xFF;
            rubbish[20] = 0xFE;

            MatchState client = Blank(7u);
            int tickBefore = client.Tick;
            int bombsBefore = client.Bombs.Count;

            SnapshotCodec.TryApply(rubbish, client);

            Assert.That(client.Tick, Is.EqualTo(tickBefore));
            Assert.That(client.Bombs.Count, Is.EqualTo(bombsBefore));
        }

        [Test]
        public void ASnapshotThatDoesNotFitReportsNothingWritten()
        {
            Assert.That(SnapshotCodec.Write(Played(7u, 600), new byte[10]), Is.Zero);
        }

        [Test]
        public void NothingIsNotASnapshot()
        {
            Assert.That(SnapshotCodec.TryApply(null, Blank(7u)), Is.False);
            Assert.That(SnapshotCodec.TryApply(new byte[SnapshotCodec.MaxSize], null), Is.False);
        }

        [Test]
        public void AnOutcomeThatMeansNothingIsRefused()
        {
            // The one enum on the wire that was not bounded. A flipped byte gave the
            // client an outcome that is neither Running nor Winner nor Draw, so anything
            // choosing between "keep playing" and "show the result" fell through every
            // case: the match stopped being over and stopped being running at once, and
            // the corruption test did not catch it because not throwing is exactly what
            // it did.
            var buffer = new byte[SnapshotCodec.MaxSize];
            int used = SnapshotCodec.Write(Played(7u, 200), buffer);

            buffer[4] = 200;

            Assert.That(SnapshotCodec.TryApply(buffer, used, Blank(7u)), Is.False);
        }

        [Test]
        public void AShortMessageIsNotReadIntoTheTailOfTheLastOne()
        {
            // Buffers are reused, so the bytes past a short message are the previous
            // snapshot. Bounded on the array rather than on what arrived, the reader runs
            // straight through the seam and applies a board that is half one tick and
            // half another, with every count in range and nothing reporting a thing.
            // Which is precisely what applying all or nothing exists to prevent.
            var buffer = new byte[SnapshotCodec.MaxSize];
            int full = SnapshotCodec.Write(Played(7u, 600), buffer);

            // A second, shorter snapshot written over the front of the first, leaving the
            // first's tail behind it exactly as a socket would.
            int shortened = SnapshotCodec.Write(Played(9u, 1), buffer);

            Assert.That(shortened, Is.LessThan(full), "the fixture needs the second to be shorter");

            for (int cut = 4; cut < shortened; cut += 11)
            {
                Assert.That(
                    SnapshotCodec.TryApply(buffer, cut, Blank(7u)),
                    Is.False,
                    $"a message cut at {cut} was completed from the bytes behind it");
            }
        }

        [Test]
        public void AnEntityOffTheBoardIsRefused()
        {
            // These bytes stop coming from the writer above the moment a socket is on the
            // other end, and a corrupt coordinate gives a bomb at x=300 on a 15-wide
            // board. Nothing in Core throws on that: ExplosionResolver gates on
            // arena.Contains and BlastMap only ever puts the tile in a dictionary. What
            // you get is an entity drawn off the board, quietly.
            //
            // Written from a state that really holds one rather than by hunting for a
            // pair of bytes to corrupt, so the test says what it means and does not move
            // when the format does.
            var buffer = new byte[SnapshotCodec.MaxSize];
            MatchState server = Played(7u, 40);
            server.AddBomb(new ActiveBomb(new Bomb(new GridPos(300, 3), 0, 2), 75));

            int used = SnapshotCodec.Write(server, buffer);

            Assert.That(used, Is.GreaterThan(0), "the fixture snapshot did not fit");
            Assert.That(SnapshotCodec.TryApply(buffer, used, Blank(7u)), Is.False);
        }

        [Test]
        public void AFuseCountingTheWrongWayIsRefused()
        {
            // Negative remaining reads as "already burning" to anything treating the fuse
            // as a countdown, and the pulse the view draws divides by it.
            var buffer = new byte[SnapshotCodec.MaxSize];
            MatchState server = Played(7u, 40);

            var bomb = new ActiveBomb(new Bomb(new GridPos(3, 3), 0, 2), 75);
            bomb.FuseRemaining = -5;
            server.AddBomb(bomb);

            int used = SnapshotCodec.Write(server, buffer);

            Assert.That(SnapshotCodec.TryApply(buffer, used, Blank(7u)), Is.False);
        }

        [Test]
        public void TheValidationBoundsStillMatchTheEnums()
        {
            // The codec range-checks rather than calling Enum.IsDefined, which boxes and
            // would run once per tile. That is only safe while the enums stay contiguous
            // from zero, so adding a kind has to fail here rather than quietly letting a
            // byte that means nothing through.
            Assert.That(
                (int)SnapshotCodec.HighestOutcome,
                Is.EqualTo(Enum.GetValues(typeof(RoundOutcome)).Length - 1));
            Assert.That((int)SnapshotCodec.HighestTile, Is.EqualTo(Enum.GetValues(typeof(TileKind)).Length - 1));
            Assert.That((int)SnapshotCodec.HighestBombKind, Is.EqualTo(Enum.GetValues(typeof(BombKind)).Length - 1));
            Assert.That(
                (int)SnapshotCodec.HighestPowerUpKind,
                Is.EqualTo(Enum.GetValues(typeof(PowerUpKind)).Length - 1));
            Assert.That((int)SnapshotCodec.HighestDirection, Is.EqualTo(Enum.GetValues(typeof(Direction)).Length - 1));
            Assert.That(
                (int)SnapshotCodec.HighestCharacter,
                Is.EqualTo(Enum.GetValues(typeof(CharacterKind)).Length - 1));
        }

        private static void AssertSame(MatchState server, MatchState client, int depth)
        {
            Assert.That(client.Tick, Is.EqualTo(server.Tick), $"tick, depth {depth}");
            Assert.That(client.Outcome, Is.EqualTo(server.Outcome), $"outcome, depth {depth}");
            Assert.That(client.WinnerId, Is.EqualTo(server.WinnerId), $"winner, depth {depth}");
            Assert.That(
                client.SuddenDeathRings,
                Is.EqualTo(server.SuddenDeathRings),
                $"sudden death rings, depth {depth}");

            for (int y = 0; y < server.Arena.Height; y++)
            {
                for (int x = 0; x < server.Arena.Width; x++)
                {
                    var tile = new GridPos(x, y);
                    Assert.That(client.Arena[tile], Is.EqualTo(server.Arena[tile]), $"tile {x},{y}, depth {depth}");
                }
            }

            Assert.That(client.Players.Count, Is.EqualTo(server.Players.Count), $"players, depth {depth}");
            for (int i = 0; i < server.Players.Count; i++)
            {
                PlayerState sent = server.Players[i];
                PlayerState got = client.Players[i];

                Assert.That(got.Position, Is.EqualTo(sent.Position), $"player {i} position, depth {depth}");
                Assert.That(got.Alive, Is.EqualTo(sent.Alive), $"player {i} alive, depth {depth}");
                Assert.That(got.IsBot, Is.EqualTo(sent.IsBot), $"player {i} bot, depth {depth}");
                Assert.That(got.BombsHeld, Is.EqualTo(sent.BombsHeld), $"player {i} bombs, depth {depth}");
                Assert.That(got.CarryCapacity, Is.EqualTo(sent.CarryCapacity), $"player {i} capacity, depth {depth}");
                Assert.That(got.FireRange, Is.EqualTo(sent.FireRange), $"player {i} range, depth {depth}");
                Assert.That(got.SpeedSteps, Is.EqualTo(sent.SpeedSteps), $"player {i} speed, depth {depth}");
                Assert.That(got.NextBombKind, Is.EqualTo(sent.NextBombKind), $"player {i} bomb kind, depth {depth}");
                Assert.That(got.Facing, Is.EqualTo(sent.Facing), $"player {i} facing, depth {depth}");
                Assert.That(got.DashTicksRemaining, Is.EqualTo(sent.DashTicksRemaining), $"player {i} dash, depth {depth}");
                Assert.That(got.StunTicksRemaining, Is.EqualTo(sent.StunTicksRemaining), $"player {i} stun, depth {depth}");
                Assert.That(
                    got.RevealTicksRemaining,
                    Is.EqualTo(sent.RevealTicksRemaining),
                    $"player {i} reveal, depth {depth}");
                Assert.That(got.Character, Is.EqualTo(sent.Character), $"player {i} character, depth {depth}");
                Assert.That(
                    got.AbilityCooldownRemaining,
                    Is.EqualTo(sent.AbilityCooldownRemaining),
                    $"player {i} ability cooldown, depth {depth}");
                Assert.That(got.VanishTicksRemaining, Is.EqualTo(sent.VanishTicksRemaining), $"player {i} vanish, depth {depth}");
            }

            Assert.That(client.Bombs.Count, Is.EqualTo(server.Bombs.Count), $"bombs, depth {depth}");
            for (int i = 0; i < server.Bombs.Count; i++)
            {
                Assert.That(client.Bombs[i].Bomb.Position, Is.EqualTo(server.Bombs[i].Bomb.Position));
                Assert.That(client.Bombs[i].Bomb.OwnerId, Is.EqualTo(server.Bombs[i].Bomb.OwnerId));
                Assert.That(client.Bombs[i].Bomb.FireRange, Is.EqualTo(server.Bombs[i].Bomb.FireRange));
                Assert.That(client.Bombs[i].Bomb.Kind, Is.EqualTo(server.Bombs[i].Bomb.Kind));
                Assert.That(client.Bombs[i].FuseTicks, Is.EqualTo(server.Bombs[i].FuseTicks));
                Assert.That(client.Bombs[i].FuseRemaining, Is.EqualTo(server.Bombs[i].FuseRemaining));
            }

            Assert.That(client.Flames.Count, Is.EqualTo(server.Flames.Count), $"flames, depth {depth}");
            for (int i = 0; i < server.Flames.Count; i++)
            {
                Assert.That(client.Flames[i].Tile, Is.EqualTo(server.Flames[i].Tile));
                Assert.That(client.Flames[i].TicksRemaining, Is.EqualTo(server.Flames[i].TicksRemaining));
                Assert.That(client.Flames[i].SpawnedTick, Is.EqualTo(server.Flames[i].SpawnedTick));
            }

            Assert.That(client.PowerUps.Count, Is.EqualTo(server.PowerUps.Count), $"power-ups, depth {depth}");
            for (int i = 0; i < server.PowerUps.Count; i++)
            {
                Assert.That(client.PowerUps[i].Tile, Is.EqualTo(server.PowerUps[i].Tile));
                Assert.That(client.PowerUps[i].Kind, Is.EqualTo(server.PowerUps[i].Kind));
                Assert.That(client.PowerUps[i].RevealedTick, Is.EqualTo(server.PowerUps[i].RevealedTick));
            }

            Assert.That(client.LooseBombs.Count, Is.EqualTo(server.LooseBombs.Count), $"loose bombs, depth {depth}");
            for (int i = 0; i < server.LooseBombs.Count; i++)
            {
                Assert.That(client.LooseBombs[i], Is.EqualTo(server.LooseBombs[i]));
            }

            Assert.That(
                client.RegrowingWalls.Count,
                Is.EqualTo(server.RegrowingWalls.Count),
                $"regrowing walls, depth {depth}");
            for (int i = 0; i < server.RegrowingWalls.Count; i++)
            {
                Assert.That(client.RegrowingWalls[i].Tile, Is.EqualTo(server.RegrowingWalls[i].Tile));
                Assert.That(client.RegrowingWalls[i].Kind, Is.EqualTo(server.RegrowingWalls[i].Kind));
                Assert.That(
                    client.RegrowingWalls[i].TicksRemaining,
                    Is.EqualTo(server.RegrowingWalls[i].TicksRemaining));
            }

            Assert.That(client.RaisedWalls.Count, Is.EqualTo(server.RaisedWalls.Count), $"raised walls, depth {depth}");
            for (int i = 0; i < server.RaisedWalls.Count; i++)
            {
                Assert.That(client.RaisedWalls[i].Tile, Is.EqualTo(server.RaisedWalls[i].Tile));
                Assert.That(client.RaisedWalls[i].TicksRemaining, Is.EqualTo(server.RaisedWalls[i].TicksRemaining));
            }
        }
    }
}
