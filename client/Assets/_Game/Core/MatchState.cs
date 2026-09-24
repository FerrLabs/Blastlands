using System;
using System.Collections.Generic;

namespace Blastlands.Core
{
    public sealed class ActiveBomb
    {
        public ActiveBomb(Bomb bomb, int fuseTicks)
        {
            Bomb = bomb;
            FuseTicks = fuseTicks;
            FuseRemaining = fuseTicks;
        }

        public Bomb Bomb { get; private set; }

        public int Id { get; internal set; }

        public bool Remote { get; set; }

        public Direction Sliding { get; set; }

        public int SlideCountdown { get; set; }

        public void MoveTo(GridPos tile)
        {
            Bomb = new Bomb(tile, Bomb.OwnerId, Bomb.FireRange, Bomb.Kind);
        }

        // What this bomb started with, which is not the same for every bomb: one a
        // player drops burns for MatchSettings.FuseTicks, one the fire lights burns for
        // LooseBombFuseTicks. Anything reading the fuse as a fraction has to divide by
        // the bomb's own total rather than by the settings.
        public int FuseTicks { get; }

        public int FuseRemaining { get; set; }

        public GridPos Position
        {
            get { return Bomb.Position; }
        }
    }

    public sealed class ActiveFlame
    {
        public ActiveFlame(GridPos tile, int ticks, int spawnedTick)
        {
            Tile = tile;
            TicksRemaining = ticks;
            SpawnedTick = spawnedTick;
        }

        public GridPos Tile { get; }

        public int TicksRemaining { get; set; }

        // A flame outlives the tick that created it, so "is this tile on fire" cannot
        // tell a pickup whether the fire is the one that just uncovered it. Refreshing
        // a burning tile counts as fresh fire, so this tracks the latest ignition.
        public int SpawnedTick { get; set; }
    }

    public sealed class PowerUp
    {
        public PowerUp(GridPos tile, PowerUpKind kind, int revealedTick)
        {
            Tile = tile;
            Kind = kind;
            RevealedTick = revealedTick;
        }

        public GridPos Tile { get; }

        public PowerUpKind Kind { get; }

        public int RevealedTick { get; }
    }

    public enum RoundOutcome : byte
    {
        Running = 0,
        Winner = 1,
        Draw = 2,
        Survived = 3,
        Overrun = 4
    }

    public sealed class MatchState
    {
        private readonly List<PlayerState> players = new List<PlayerState>();
        private readonly List<ActiveBomb> bombs = new List<ActiveBomb>();
        private readonly List<ActiveFlame> flames = new List<ActiveFlame>();
        private readonly List<PowerUp> powerUps = new List<PowerUp>();
        private readonly Dictionary<GridPos, PowerUpKind> hiddenPowerUps = new Dictionary<GridPos, PowerUpKind>();
        private readonly List<GridPos> looseBombs = new List<GridPos>();
        private readonly List<WallRegrowth> regrowingWalls = new List<WallRegrowth>();
        private readonly List<RaisedWall> raisedWalls = new List<RaisedWall>();
        private readonly List<Zombie> zombies = new List<Zombie>();
        private int[] coastDistance;

        internal readonly Queue<GridPos> ZombieQueue = new Queue<GridPos>();
        internal int[] ZombieField;
        internal int[] ChewField;

        public MatchState(Arena arena, MatchSettings settings, uint seed)
        {
            if (arena == null)
            {
                throw new ArgumentNullException(nameof(arena));
            }

            Arena = arena;
            Settings = settings;
            Seed = seed;
            Random = new DeterministicRandom(seed);
            Outcome = RoundOutcome.Running;
            WinnerId = -1;
            WaveCountdown = settings.Survival.FirstWaveTicks;
        }

        public IReadOnlyList<Zombie> Zombies
        {
            get { return zombies; }
        }

        public int Wave { get; set; }

        public int WaveCountdown { get; set; }

        public int ZombiesSlain { get; set; }

        public int NextZombieId { get; set; }

        public Zombie AddZombie(SubPos position)
        {
            var zombie = new Zombie(NextZombieId++, position);
            zombies.Add(zombie);
            return zombie;
        }

        public void RemoveZombieAt(int index)
        {
            zombies.RemoveAt(index);
        }

        internal void AddZombieFromSnapshot(Zombie zombie)
        {
            zombies.Add(zombie);
        }

        public Arena Arena { get; }

        public MatchSettings Settings { get; }

        // Kept so presentation can derive stable per-tile variation without consuming
        // the simulation's random stream, which would desync it.
        public uint Seed { get; }

        public DeterministicRandom Random { get; }

        public int Tick { get; set; }

        public RoundOutcome Outcome { get; set; }

        public int WinnerId { get; set; }

        // How many rings sudden death has already closed, so a match that skips ticks
        // still closes every ring rather than only the one it happens to land on.
        public int SuddenDeathRings { get; set; }

        // Measured on first use rather than in the constructor: the island never
        // changes shape once generated, but most matches never reach sudden death and
        // would pay for the flood for nothing.
        public int DistanceToCoast(GridPos tile)
        {
            if (coastDistance == null)
            {
                coastDistance = CoastDistance.Measure(Arena);
            }

            return coastDistance[(tile.Y * Arena.Width) + tile.X];
        }

        // Internal rather than public, and grouped here rather than spread among the
        // methods the simulation uses. A snapshot arriving from the server replaces the
        // whole of the dynamic state at once, which no rule of the game ever does: these
        // are for Core.Net and would be a foot-gun anywhere else, so the assembly
        // boundary is what keeps them out of the runtime's reach.
        internal void ClearForSnapshot()
        {
            bombs.Clear();
            flames.Clear();
            powerUps.Clear();
            looseBombs.Clear();
            regrowingWalls.Clear();
            raisedWalls.Clear();
            zombies.Clear();
        }

        internal void AddPowerUpFromSnapshot(PowerUp powerUp)
        {
            powerUps.Add(powerUp);
        }

        internal void AddRegrowthFromSnapshot(WallRegrowth regrowth)
        {
            regrowingWalls.Add(regrowth);
        }

        internal void AddRaisedWallFromSnapshot(RaisedWall wall)
        {
            raisedWalls.Add(wall);
        }

        public IReadOnlyList<PlayerState> Players
        {
            get { return players; }
        }

        public IReadOnlyList<ActiveBomb> Bombs
        {
            get { return bombs; }
        }

        public IReadOnlyList<ActiveFlame> Flames
        {
            get { return flames; }
        }

        public bool ChooseCharacter(int seat, CharacterKind character)
        {
            if (Tick != 0 || !Settings.Rules.AllowsCharacters || seat < 0 || seat >= players.Count)
            {
                return false;
            }

            PlayerState player = players[seat];
            player.Rekit(
                character == CharacterKind.None ? player.StartingCharacter : character, Settings);
            return true;
        }

        public PlayerState AddPlayer(GridPos spawn)
        {
            return AddPlayer(spawn, CharacterKind.None);
        }

        public PlayerState AddPlayer(GridPos spawn, CharacterKind character)
        {
            var player = new PlayerState(players.Count, SubPos.AtTileCentre(spawn), Settings, character);
            players.Add(player);
            return player;
        }

        public int NextBombId { get; set; } = 1;

        public void AddBomb(ActiveBomb bomb)
        {
            if (bomb.Id == 0)
            {
                bomb.Id = NextBombId++;
            }

            bombs.Add(bomb);
        }

        public void RemoveBombAt(int index)
        {
            bombs.RemoveAt(index);
        }

        public void AddFlame(GridPos tile, int ticks, int spawnedTick)
        {
            for (int i = 0; i < flames.Count; i++)
            {
                if (flames[i].Tile == tile)
                {
                    flames[i].TicksRemaining = ticks;
                    flames[i].SpawnedTick = spawnedTick;
                    return;
                }
            }

            flames.Add(new ActiveFlame(tile, ticks, spawnedTick));
        }

        public void RemoveFlameAt(int index)
        {
            flames.RemoveAt(index);
        }

        public IReadOnlyList<PowerUp> PowerUps
        {
            get { return powerUps; }
        }

        public void HidePowerUp(GridPos tile, PowerUpKind kind)
        {
            hiddenPowerUps[tile] = kind;
        }

        // Reveals whatever was under a block, once. The entry is removed so a tile
        // cannot produce two pickups if it is somehow destroyed twice.
        public bool TryRevealPowerUp(GridPos tile, out PowerUpKind kind)
        {
            if (!hiddenPowerUps.TryGetValue(tile, out kind))
            {
                return false;
            }

            hiddenPowerUps.Remove(tile);
            powerUps.Add(new PowerUp(tile, kind, Tick));
            return true;
        }

        public int PowerUpIndexAt(GridPos tile)
        {
            for (int i = 0; i < powerUps.Count; i++)
            {
                if (powerUps[i].Tile == tile)
                {
                    return i;
                }
            }

            return -1;
        }

        public void RemovePowerUpAt(int index)
        {
            powerUps.RemoveAt(index);
        }

        public IReadOnlyList<GridPos> LooseBombs
        {
            get { return looseBombs; }
        }

        public void AddLooseBomb(GridPos tile)
        {
            looseBombs.Add(tile);
        }

        public int LooseBombIndexAt(GridPos tile)
        {
            for (int i = 0; i < looseBombs.Count; i++)
            {
                if (looseBombs[i] == tile)
                {
                    return i;
                }
            }

            return -1;
        }

        public void RemoveLooseBombAt(int index)
        {
            looseBombs.RemoveAt(index);
        }

        public IReadOnlyList<WallRegrowth> RegrowingWalls
        {
            get { return regrowingWalls; }
        }

        public void ScheduleRegrowth(GridPos tile, TileKind kind, int ticks)
        {
            for (int i = 0; i < regrowingWalls.Count; i++)
            {
                if (regrowingWalls[i].Tile == tile)
                {
                    // Replaced rather than nudged, so a tile that was a bush and is now
                    // scheduled as a wall comes back as the wall.
                    regrowingWalls[i] = new WallRegrowth(tile, kind, ticks);
                    return;
                }
            }

            regrowingWalls.Add(new WallRegrowth(tile, kind, ticks));
        }

        public void RemoveRegrowthAt(int index)
        {
            regrowingWalls.RemoveAt(index);
        }

        public bool IsRegrowing(GridPos tile)
        {
            for (int i = 0; i < regrowingWalls.Count; i++)
            {
                if (regrowingWalls[i].Tile == tile)
                {
                    return true;
                }
            }

            return false;
        }

        public IReadOnlyList<RaisedWall> RaisedWalls
        {
            get { return raisedWalls; }
        }

        public void AddRaisedWall(RaisedWall wall)
        {
            raisedWalls.Add(wall);
        }

        public int RaisedWallIndexAt(GridPos tile)
        {
            for (int i = 0; i < raisedWalls.Count; i++)
            {
                if (raisedWalls[i].Tile == tile)
                {
                    return i;
                }
            }

            return -1;
        }

        public void RemoveRaisedWallAt(int index)
        {
            raisedWalls.RemoveAt(index);
        }

        public bool HasBombAt(GridPos tile)
        {
            for (int i = 0; i < bombs.Count; i++)
            {
                if (bombs[i].Position == tile)
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasFlameAt(GridPos tile)
        {
            for (int i = 0; i < flames.Count; i++)
            {
                if (flames[i].Tile == tile)
                {
                    return true;
                }
            }

            return false;
        }

        public int AliveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i].Alive)
                    {
                        count++;
                    }
                }

                return count;
            }
        }
    }
}
