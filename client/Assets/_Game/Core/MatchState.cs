using System;
using System.Collections.Generic;

namespace Blastlands.Core
{
    public sealed class ActiveBomb
    {
        public ActiveBomb(Bomb bomb, int fuseTicks)
        {
            Bomb = bomb;
            FuseRemaining = fuseTicks;
        }

        public Bomb Bomb { get; }

        public int FuseRemaining { get; set; }

        public GridPos Position
        {
            get { return Bomb.Position; }
        }
    }

    public sealed class ActiveFlame
    {
        public ActiveFlame(GridPos tile, int ticks)
        {
            Tile = tile;
            TicksRemaining = ticks;
        }

        public GridPos Tile { get; }

        public int TicksRemaining { get; set; }
    }

    public enum RoundOutcome : byte
    {
        Running = 0,
        Winner = 1,
        Draw = 2
    }

    public sealed class MatchState
    {
        private readonly List<PlayerState> players = new List<PlayerState>();
        private readonly List<ActiveBomb> bombs = new List<ActiveBomb>();
        private readonly List<ActiveFlame> flames = new List<ActiveFlame>();

        public MatchState(Arena arena, MatchSettings settings, uint seed)
        {
            if (arena == null)
            {
                throw new ArgumentNullException(nameof(arena));
            }

            Arena = arena;
            Settings = settings;
            Random = new DeterministicRandom(seed);
            Outcome = RoundOutcome.Running;
            WinnerId = -1;
        }

        public Arena Arena { get; }

        public MatchSettings Settings { get; }

        public DeterministicRandom Random { get; }

        public int Tick { get; set; }

        public RoundOutcome Outcome { get; set; }

        public int WinnerId { get; set; }

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

        public PlayerState AddPlayer(GridPos spawn)
        {
            var player = new PlayerState(players.Count, SubPos.AtTileCentre(spawn), Settings);
            players.Add(player);
            return player;
        }

        public void AddBomb(ActiveBomb bomb)
        {
            bombs.Add(bomb);
        }

        public void RemoveBombAt(int index)
        {
            bombs.RemoveAt(index);
        }

        public void AddFlame(GridPos tile, int ticks)
        {
            for (int i = 0; i < flames.Count; i++)
            {
                if (flames[i].Tile == tile)
                {
                    flames[i].TicksRemaining = ticks;
                    return;
                }
            }

            flames.Add(new ActiveFlame(tile, ticks));
        }

        public void RemoveFlameAt(int index)
        {
            flames.RemoveAt(index);
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
