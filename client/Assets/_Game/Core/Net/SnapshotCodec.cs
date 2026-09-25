namespace Blastlands.Core.Net
{
    // The whole of a match's changing state, as the server saw it on one tick.
    //
    // Applied onto a MatchState rather than decoded into a shape of its own, so the
    // view, the HUD, the camera and the audio all keep working against the same type
    // they already render a local match from. Gameplay code exists once; so does the
    // code that draws it.
    //
    // Everything that never changes during a match travels once when the client joins:
    // the settings, the seed, the arena's size. What is here is what a tick can move.
    //
    // Whole rather than a delta on purpose. A delta is smaller and needs the client to
    // have received every snapshot before it, which is a second problem to solve and a
    // second way to be subtly wrong. The arena is the bulk of it, 525 bytes on the
    // Classic board, and at thirty ticks that is 16 KB/s to one client. Worth measuring
    // before it is worth compressing.
    public static class SnapshotCodec
    {
        // Generous rather than exact. A snapshot is written into a buffer this big and
        // reports how much it used.
        public const int MaxSize = 16 * 1024;

        // Nothing legitimate approaches these. They exist so a corrupt count cannot ask
        // for an allocation the size of whatever the bytes happened to say.
        private const int MostPlayers = 64;
        private const int MostBombs = 512;
        private const int MostFlames = 4096;
        private const int MostPowerUps = 512;
        private const int MostLooseBombs = 512;
        private const int MostRegrowing = 1024;
        private const int MostRaisedWalls = 256;
        private const int MostZombies = 256;

        public static int Write(MatchState state, byte[] buffer)
        {
            var writer = new NetWriter(buffer);

            writer.Int32(state.Tick);
            writer.Byte((byte)state.Outcome);
            writer.Int16(state.WinnerId);
            writer.Byte(state.WonByHoldingOut ? (byte)1 : (byte)0);
            writer.Int32(state.SuddenDeathRings);
            writer.Int32(state.NextBombId);
            writer.Int16(state.Wave);
            writer.Int16(state.WaveCountdown);
            writer.Int32(state.ZombiesSlain);
            writer.Int32(state.NextZombieId);

            WriteArena(ref writer, state.Arena);
            WritePlayers(ref writer, state.Players);
            WriteBombs(ref writer, state.Bombs);
            WriteFlames(ref writer, state.Flames);
            WritePowerUps(ref writer, state.PowerUps);
            WriteLooseBombs(ref writer, state.LooseBombs);
            WriteRegrowing(ref writer, state.RegrowingWalls);
            WriteRaisedWalls(ref writer, state.RaisedWalls);
            WriteZombies(ref writer, state.Zombies);

            return writer.Ok ? writer.Length : 0;
        }

        // The state is left alone until the whole message has been read, because a
        // snapshot applied halfway is worse than one dropped: the client would render a
        // board whose bombs belong to one tick and whose flames belong to another.
        public static bool TryApply(byte[] buffer, MatchState state)
        {
            return TryApply(buffer, buffer == null ? 0 : buffer.Length, state);
        }

        // The length that arrived, not the size of the buffer it arrived in. Anything
        // reading from a socket has to use this one: buffers are reused, so a short
        // message sits in front of the tail of the last one, and a reader bounded on the
        // array would parse straight through the seam and apply a board that is half this
        // tick and half the one before it. Every count would be in range and nothing
        // would report a thing, which is precisely what applying all or nothing is for.
        public static bool TryApply(byte[] buffer, int length, MatchState state)
        {
            if (buffer == null || state == null || length <= 0 || length > buffer.Length)
            {
                return false;
            }

            var reader = new NetReader(buffer, length);

            int tick = reader.Int32();
            var outcome = (RoundOutcome)reader.Byte();
            int winner = reader.Int16();
            byte heldOut = reader.Byte();
            int rings = reader.Int32();
            int nextBomb = reader.Int32();
            int wave = reader.Int16();
            int waveCountdown = reader.Int16();
            int slain = reader.Int32();
            int nextZombie = reader.Int32();

            // RoundOutcome is on the wire like the other four enums and was the only one
            // not bounded. A flipped byte gave the client an outcome that is neither
            // Running nor Winner nor Draw, so anything choosing between "keep playing"
            // and "show the result" fell through every case: the match stopped being
            // over and stopped being running at the same time, silently.
            if (!reader.Ok || tick < 0 || (byte)outcome > HighestOutcome || heldOut > 1
                || wave < 0 || waveCountdown < 0 || slain < 0 || nextZombie < 0 || nextBomb < 1)
            {
                return false;
            }

            var scratch = new Scratch();
            if (!ReadArena(ref reader, state.Arena, scratch)
                || !ReadPlayers(ref reader, state, scratch)
                || !ReadBombs(ref reader, state.Arena, scratch)
                || !ReadFlames(ref reader, state.Arena, scratch)
                || !ReadPowerUps(ref reader, state.Arena, scratch)
                || !ReadLooseBombs(ref reader, state.Arena, scratch)
                || !ReadRegrowing(ref reader, state.Arena, scratch)
                || !ReadRaisedWalls(ref reader, state.Arena, scratch)
                || !ReadZombies(ref reader, state.Arena, scratch))
            {
                return false;
            }

            state.Tick = tick;
            state.Outcome = outcome;
            state.WinnerId = winner;
            state.WonByHoldingOut = heldOut == 1;
            state.SuddenDeathRings = rings;
            state.NextBombId = nextBomb;
            state.Wave = wave;
            state.WaveCountdown = waveCountdown;
            state.ZombiesSlain = slain;
            state.NextZombieId = nextZombie;

            for (int i = 0; i < scratch.Tiles.Count; i++)
            {
                state.Arena[scratch.TileAt[i]] = scratch.Tiles[i];
            }

            for (int i = 0; i < scratch.Players.Count && i < state.Players.Count; i++)
            {
                scratch.Players[i].ApplyTo(state.Players[i]);
            }

            state.ClearForSnapshot();

            for (int i = 0; i < scratch.Bombs.Count; i++)
            {
                state.AddBomb(scratch.Bombs[i]);
            }

            for (int i = 0; i < scratch.Flames.Count; i++)
            {
                FlameLine flame = scratch.Flames[i];
                state.AddFlame(flame.Tile, flame.TicksRemaining, flame.SpawnedTick);
            }

            for (int i = 0; i < scratch.PowerUps.Count; i++)
            {
                state.AddPowerUpFromSnapshot(scratch.PowerUps[i]);
            }

            for (int i = 0; i < scratch.LooseBombs.Count; i++)
            {
                state.AddLooseBomb(scratch.LooseBombs[i]);
            }

            for (int i = 0; i < scratch.Regrowing.Count; i++)
            {
                state.AddRegrowthFromSnapshot(scratch.Regrowing[i]);
            }

            for (int i = 0; i < scratch.RaisedWalls.Count; i++)
            {
                state.AddRaisedWallFromSnapshot(scratch.RaisedWalls[i]);
            }

            for (int i = 0; i < scratch.Zombies.Count; i++)
            {
                state.AddZombieFromSnapshot(scratch.Zombies[i]);
            }

            return true;
        }

        private static void WriteArena(ref NetWriter writer, Arena arena)
        {
            writer.Int16(arena.Width);
            writer.Int16(arena.Height);

            for (int y = 0; y < arena.Height; y++)
            {
                for (int x = 0; x < arena.Width; x++)
                {
                    writer.Byte((byte)arena[new GridPos(x, y)]);
                }
            }
        }

        // Refused rather than resized when the sizes disagree. A client and a server
        // holding different boards have already lost, and writing tiles into the wrong
        // rows would draw a plausible arena that is not the one being played.
        private static bool ReadArena(ref NetReader reader, Arena arena, Scratch scratch)
        {
            int width = reader.Int16();
            int height = reader.Int16();

            if (!reader.Ok || width != arena.Width || height != arena.Height)
            {
                return false;
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte raw = reader.Byte();
                    if (!IsTile(raw))
                    {
                        return false;
                    }

                    scratch.TileAt.Add(new GridPos(x, y));
                    scratch.Tiles.Add((TileKind)raw);
                }
            }

            return reader.Ok;
        }

        private static void WritePlayers(ref NetWriter writer, System.Collections.Generic.IReadOnlyList<PlayerState> players)
        {
            writer.Int32(players.Count);

            for (int i = 0; i < players.Count; i++)
            {
                PlayerState player = players[i];
                writer.Int32(player.Position.X);
                writer.Int32(player.Position.Y);
                writer.Bool(player.Alive);
                writer.Bool(player.IsBot);
                writer.Int16(player.BombsHeld);
                writer.Int16(player.CarryCapacity);
                writer.Int16(player.FireRange);
                writer.Int16(player.SpeedSteps);
                writer.Byte((byte)player.NextBombKind);
                writer.Byte((byte)player.Facing);
                writer.Byte((byte)player.DashDirection);
                writer.Int16(player.DashTicksRemaining);
                writer.Int16(player.DashCooldownRemaining);
                writer.Byte((byte)player.ShoveDirection);
                writer.Int16(player.ShoveTicksRemaining);
                writer.Int16(player.StunTicksRemaining);
                writer.Int16(player.PushCooldownRemaining);
                writer.Int16(player.RevealTicksRemaining);
                writer.Byte((byte)player.Character);
                writer.Int16(player.AbilityCooldownRemaining);
                writer.Int16(player.VanishTicksRemaining);
                writer.Bool(player.CanKick);
                writer.Bool(player.HasRemote);
                writer.Byte((byte)player.Curse);
                writer.Int16(player.CurseTicksRemaining);
            }
        }

        private static bool ReadPlayers(ref NetReader reader, MatchState state, Scratch scratch)
        {
            int count = reader.Count(MostPlayers);
            if (!reader.Ok || count != state.Players.Count)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                var line = new PlayerLine
                {
                    Position = new SubPos(reader.Int32(), reader.Int32()),
                    Alive = reader.Bool(),
                    IsBot = reader.Bool(),
                    BombsHeld = reader.Int16(),
                    CarryCapacity = reader.Int16(),
                    FireRange = reader.Int16(),
                    SpeedSteps = reader.Int16(),
                    NextBombKind = reader.Byte(),
                    Facing = reader.Byte(),
                    DashDirection = reader.Byte(),
                    DashTicksRemaining = reader.Int16(),
                    DashCooldownRemaining = reader.Int16(),
                    ShoveDirection = reader.Byte(),
                    ShoveTicksRemaining = reader.Int16(),
                    StunTicksRemaining = reader.Int16(),
                    PushCooldownRemaining = reader.Int16(),
                    RevealTicksRemaining = reader.Int16(),
                    Character = reader.Byte(),
                    AbilityCooldownRemaining = reader.Int16(),
                    VanishTicksRemaining = reader.Int16(),
                    CanKick = reader.Bool(),
                    HasRemote = reader.Bool(),
                    Curse = reader.Byte(),
                    CurseTicksRemaining = reader.Int16()
                };

                if (!line.IsSane())
                {
                    return false;
                }

                scratch.Players.Add(line);
            }

            return reader.Ok;
        }

        private static void WriteBombs(ref NetWriter writer, System.Collections.Generic.IReadOnlyList<ActiveBomb> bombs)
        {
            writer.Int32(bombs.Count);

            for (int i = 0; i < bombs.Count; i++)
            {
                ActiveBomb bomb = bombs[i];
                writer.Int32(bomb.Id);
                writer.Int16(bomb.Bomb.Position.X);
                writer.Int16(bomb.Bomb.Position.Y);
                writer.Int16(bomb.Bomb.OwnerId);
                writer.Int16(bomb.Bomb.FireRange);
                writer.Byte((byte)bomb.Bomb.Kind);
                writer.Int32(bomb.FuseTicks);
                writer.Int32(bomb.FuseRemaining);
                writer.Bool(bomb.Remote);
                writer.Byte((byte)bomb.Sliding);
                writer.Int16(bomb.SlideCountdown);
            }
        }

        private static bool ReadBombs(ref NetReader reader, Arena arena, Scratch scratch)
        {
            int count = reader.Count(MostBombs);
            if (!reader.Ok)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                int id = reader.Int32();
                var tile = new GridPos(reader.Int16(), reader.Int16());
                int owner = reader.Int16();
                int range = reader.Int16();
                byte kind = reader.Byte();
                int fuseTicks = reader.Int32();
                int fuseRemaining = reader.Int32();
                bool remote = reader.Bool();
                byte sliding = reader.Byte();
                int slideCountdown = reader.Int16();

                // Every coordinate is checked now that these bytes come from a socket
                // rather than from the writer above. ReadArena has already agreed the
                // dimensions, so this costs a comparison and closes an entity rendered
                // off the board with a fuse counting the wrong way.
                // Zero as well as negative: the view draws the pulse as FuseRemaining over
                // FuseTicks, so a total of zero is a NaN scale and a log line for every
                // frame the bomb is on the board. A real total always comes from settings,
                // so refusing zero costs nothing.
                if (!arena.Contains(tile) || !IsBombKind(kind) || fuseTicks <= 0 || fuseRemaining < 0
                    || !IsDirection(sliding) || slideCountdown < 0 || id < 1)
                {
                    return false;
                }

                var bomb = new ActiveBomb(new Bomb(tile, owner, range, (BombKind)kind), fuseTicks);
                bomb.FuseRemaining = fuseRemaining;
                bomb.Id = id;
                bomb.Remote = remote;
                bomb.Sliding = (Direction)sliding;
                bomb.SlideCountdown = slideCountdown;
                scratch.Bombs.Add(bomb);
            }

            return reader.Ok;
        }

        private static void WriteFlames(ref NetWriter writer, System.Collections.Generic.IReadOnlyList<ActiveFlame> flames)
        {
            writer.Int32(flames.Count);

            for (int i = 0; i < flames.Count; i++)
            {
                ActiveFlame flame = flames[i];
                writer.Int16(flame.Tile.X);
                writer.Int16(flame.Tile.Y);
                writer.Int16(flame.TicksRemaining);
                writer.Int32(flame.SpawnedTick);
            }
        }

        private static bool ReadFlames(ref NetReader reader, Arena arena, Scratch scratch)
        {
            int count = reader.Count(MostFlames);
            if (!reader.Ok)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                var tile = new GridPos(reader.Int16(), reader.Int16());
                int remaining = reader.Int16();
                int spawned = reader.Int32();

                if (!arena.Contains(tile))
                {
                    return false;
                }

                scratch.Flames.Add(new FlameLine
                {
                    Tile = tile,
                    TicksRemaining = remaining,
                    SpawnedTick = spawned
                });
            }

            return reader.Ok;
        }

        private static void WritePowerUps(ref NetWriter writer, System.Collections.Generic.IReadOnlyList<PowerUp> powerUps)
        {
            writer.Int32(powerUps.Count);

            for (int i = 0; i < powerUps.Count; i++)
            {
                PowerUp powerUp = powerUps[i];
                writer.Int16(powerUp.Tile.X);
                writer.Int16(powerUp.Tile.Y);
                writer.Byte((byte)powerUp.Kind);
                writer.Int32(powerUp.RevealedTick);
            }
        }

        private static bool ReadPowerUps(ref NetReader reader, Arena arena, Scratch scratch)
        {
            int count = reader.Count(MostPowerUps);
            if (!reader.Ok)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                var tile = new GridPos(reader.Int16(), reader.Int16());
                byte kind = reader.Byte();
                int revealed = reader.Int32();

                if (!arena.Contains(tile) || !IsPowerUpKind(kind))
                {
                    return false;
                }

                scratch.PowerUps.Add(new PowerUp(tile, (PowerUpKind)kind, revealed));
            }

            return reader.Ok;
        }

        private static void WriteLooseBombs(ref NetWriter writer, System.Collections.Generic.IReadOnlyList<GridPos> looseBombs)
        {
            writer.Int32(looseBombs.Count);

            for (int i = 0; i < looseBombs.Count; i++)
            {
                writer.Int16(looseBombs[i].X);
                writer.Int16(looseBombs[i].Y);
            }
        }

        private static bool ReadLooseBombs(ref NetReader reader, Arena arena, Scratch scratch)
        {
            int count = reader.Count(MostLooseBombs);
            if (!reader.Ok)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                var tile = new GridPos(reader.Int16(), reader.Int16());
                if (!arena.Contains(tile))
                {
                    return false;
                }

                scratch.LooseBombs.Add(tile);
            }

            return reader.Ok;
        }

        private static void WriteRegrowing(ref NetWriter writer, System.Collections.Generic.IReadOnlyList<WallRegrowth> walls)
        {
            writer.Int32(walls.Count);

            for (int i = 0; i < walls.Count; i++)
            {
                WallRegrowth wall = walls[i];
                writer.Int16(wall.Tile.X);
                writer.Int16(wall.Tile.Y);
                writer.Byte((byte)wall.Kind);
                writer.Int16(wall.TicksRemaining);
            }
        }

        private static bool ReadRegrowing(ref NetReader reader, Arena arena, Scratch scratch)
        {
            int count = reader.Count(MostRegrowing);
            if (!reader.Ok)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                var tile = new GridPos(reader.Int16(), reader.Int16());
                byte kind = reader.Byte();
                int ticks = reader.Int16();

                if (!arena.Contains(tile) || !IsTile(kind))
                {
                    return false;
                }

                scratch.Regrowing.Add(new WallRegrowth(tile, (TileKind)kind, ticks));
            }

            return reader.Ok;
        }

        private static void WriteRaisedWalls(ref NetWriter writer, System.Collections.Generic.IReadOnlyList<RaisedWall> walls)
        {
            writer.Int32(walls.Count);

            for (int i = 0; i < walls.Count; i++)
            {
                RaisedWall wall = walls[i];
                writer.Int16(wall.Tile.X);
                writer.Int16(wall.Tile.Y);
                writer.Int16(wall.TicksRemaining);
            }
        }

        private static bool ReadRaisedWalls(ref NetReader reader, Arena arena, Scratch scratch)
        {
            int count = reader.Count(MostRaisedWalls);
            if (!reader.Ok)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                var tile = new GridPos(reader.Int16(), reader.Int16());
                int ticks = reader.Int16();

                if (!arena.Contains(tile))
                {
                    return false;
                }

                scratch.RaisedWalls.Add(new RaisedWall(tile, ticks));
            }

            return reader.Ok;
        }

        private static void WriteZombies(ref NetWriter writer, System.Collections.Generic.IReadOnlyList<Zombie> zombies)
        {
            writer.Int32(zombies.Count);

            for (int i = 0; i < zombies.Count; i++)
            {
                Zombie zombie = zombies[i];
                writer.Int32(zombie.Id);
                writer.Int32(zombie.Position.X);
                writer.Int32(zombie.Position.Y);
                writer.Byte((byte)zombie.Facing);
                writer.Int16(zombie.ChewTicks);
            }
        }

        private static bool ReadZombies(ref NetReader reader, Arena arena, Scratch scratch)
        {
            int count = reader.Count(MostZombies);
            if (!reader.Ok)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                int id = reader.Int32();
                var position = new SubPos(reader.Int32(), reader.Int32());
                byte facing = reader.Byte();
                int chew = reader.Int16();

                if (id < 0 || !arena.Contains(position.Tile) || !IsDirection(facing) || chew < 0)
                {
                    return false;
                }

                scratch.Zombies.Add(new Zombie(id, position) { Facing = (Direction)facing, ChewTicks = chew });
            }

            return reader.Ok;
        }

        // Range checks rather than Enum.IsDefined, which boxes and would run once per
        // tile: 525 of them per snapshot at thirty ticks a second. All four enums are
        // contiguous from zero, and a test pins these bounds against the real member
        // counts so adding a kind fails there rather than silently letting a byte that
        // means nothing through.
        public const byte HighestOutcome = (byte)RoundOutcome.Overrun;
        public const byte HighestTile = (byte)TileKind.Void;
        public const byte HighestBombKind = (byte)BombKind.Cluster;
        public const byte HighestPowerUpKind = (byte)PowerUpKind.Skull;
        public const byte HighestDirection = (byte)Direction.Up;
        public const byte HighestCharacter = (byte)CharacterKind.Sapper;
        public const byte HighestCurse = (byte)CurseKind.Dry;

        private static bool IsTile(byte raw)
        {
            return raw <= HighestTile;
        }

        private static bool IsBombKind(byte raw)
        {
            return raw <= HighestBombKind;
        }

        private static bool IsPowerUpKind(byte raw)
        {
            return raw <= HighestPowerUpKind;
        }

        private static bool IsDirection(byte raw)
        {
            return raw <= HighestDirection;
        }

        private sealed class Scratch
        {
            public readonly System.Collections.Generic.List<GridPos> TileAt =
                new System.Collections.Generic.List<GridPos>();

            public readonly System.Collections.Generic.List<TileKind> Tiles =
                new System.Collections.Generic.List<TileKind>();

            public readonly System.Collections.Generic.List<PlayerLine> Players =
                new System.Collections.Generic.List<PlayerLine>();

            public readonly System.Collections.Generic.List<ActiveBomb> Bombs =
                new System.Collections.Generic.List<ActiveBomb>();

            public readonly System.Collections.Generic.List<FlameLine> Flames =
                new System.Collections.Generic.List<FlameLine>();

            public readonly System.Collections.Generic.List<PowerUp> PowerUps =
                new System.Collections.Generic.List<PowerUp>();

            public readonly System.Collections.Generic.List<GridPos> LooseBombs =
                new System.Collections.Generic.List<GridPos>();

            public readonly System.Collections.Generic.List<WallRegrowth> Regrowing =
                new System.Collections.Generic.List<WallRegrowth>();

            public readonly System.Collections.Generic.List<RaisedWall> RaisedWalls =
                new System.Collections.Generic.List<RaisedWall>();

            public readonly System.Collections.Generic.List<Zombie> Zombies =
                new System.Collections.Generic.List<Zombie>();
        }

        private struct FlameLine
        {
            public GridPos Tile;
            public int TicksRemaining;
            public int SpawnedTick;
        }

        private struct PlayerLine
        {
            public SubPos Position;
            public bool Alive;
            public bool IsBot;
            public int BombsHeld;
            public int CarryCapacity;
            public int FireRange;
            public int SpeedSteps;
            public byte NextBombKind;
            public byte Facing;
            public byte DashDirection;
            public int DashTicksRemaining;
            public int DashCooldownRemaining;
            public byte ShoveDirection;
            public int ShoveTicksRemaining;
            public int StunTicksRemaining;
            public int PushCooldownRemaining;
            public int RevealTicksRemaining;
            public byte Character;
            public int AbilityCooldownRemaining;
            public int VanishTicksRemaining;
            public bool CanKick;
            public bool HasRemote;
            public byte Curse;
            public int CurseTicksRemaining;

            public bool IsSane()
            {
                return IsBombKind(NextBombKind)
                    && Curse <= HighestCurse
                    && CurseTicksRemaining >= 0
                    && Character <= HighestCharacter
                    && IsDirection(Facing)
                    && IsDirection(DashDirection)
                    && IsDirection(ShoveDirection);
            }

            public void ApplyTo(PlayerState player)
            {
                player.Position = Position;
                player.Alive = Alive;
                player.IsBot = IsBot;
                player.BombsHeld = BombsHeld;
                player.CarryCapacity = CarryCapacity;
                player.FireRange = FireRange;
                player.SpeedSteps = SpeedSteps;
                player.NextBombKind = (BombKind)NextBombKind;
                player.Facing = (Direction)Facing;
                player.DashDirection = (Direction)DashDirection;
                player.DashTicksRemaining = DashTicksRemaining;
                player.DashCooldownRemaining = DashCooldownRemaining;
                player.ShoveDirection = (Direction)ShoveDirection;
                player.ShoveTicksRemaining = ShoveTicksRemaining;
                player.StunTicksRemaining = StunTicksRemaining;
                player.PushCooldownRemaining = PushCooldownRemaining;
                player.RevealTicksRemaining = RevealTicksRemaining;
                player.Character = (CharacterKind)Character;
                player.AbilityCooldownRemaining = AbilityCooldownRemaining;
                player.VanishTicksRemaining = VanishTicksRemaining;
                player.CanKick = CanKick;
                player.HasRemote = HasRemote;
                player.Curse = (CurseKind)Curse;
                player.CurseTicksRemaining = CurseTicksRemaining;
            }
        }
    }
}
