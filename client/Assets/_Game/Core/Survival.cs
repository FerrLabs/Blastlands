using System.Collections.Generic;

namespace Blastlands.Core
{
    public static class Survival
    {
        private const int Unreached = int.MaxValue;

        private static readonly GridPos[] Cardinals =
        {
            new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1)
        };

        public static void Tick(MatchState state)
        {
            SurvivalSettings settings = state.Settings.Survival;
            if (!settings.Enabled)
            {
                return;
            }

            BurnZombies(state);
            RunWaves(state, settings);
            MoveZombies(state, settings);
            Bite(state, settings);
        }

        public static bool Cleared(MatchState state)
        {
            return state.Wave >= state.Settings.Survival.Waves && state.Zombies.Count == 0;
        }

        private static void BurnZombies(MatchState state)
        {
            for (int i = state.Zombies.Count - 1; i >= 0; i--)
            {
                if (state.HasFlameAt(state.Zombies[i].Tile))
                {
                    state.RemoveZombieAt(i);
                    state.ZombiesSlain++;
                }
            }
        }

        private static void RunWaves(MatchState state, SurvivalSettings settings)
        {
            if (state.Zombies.Count > 0 || state.Wave >= settings.Waves)
            {
                return;
            }

            if (state.WaveCountdown > 0)
            {
                state.WaveCountdown--;
            }

            if (state.WaveCountdown > 0)
            {
                return;
            }

            state.Wave++;
            state.WaveCountdown = settings.BreatherTicks;
            Spawn(state, settings.ZombiesIn(state.Wave, state.Players.Count), settings.SpawnDistance);
        }

        public static void Spawn(MatchState state, int count, int distance)
        {
            var far = new List<GridPos>();
            var near = new List<GridPos>();
            int[] field = DistanceField(state, false);

            for (int y = 0; y < state.Arena.Height; y++)
            {
                for (int x = 0; x < state.Arena.Width; x++)
                {
                    int steps = field[Index(state.Arena, x, y)];
                    if (steps == Unreached || steps == 0)
                    {
                        continue;
                    }

                    (steps >= distance ? far : near).Add(new GridPos(x, y));
                }
            }

            List<GridPos> pool = far.Count > 0 ? far : near;
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int pick = state.Random.NextInt(pool.Count);
                state.AddZombie(SubPos.AtTileCentre(pool[pick]));
                if (pool.Count > 1)
                {
                    pool.RemoveAt(pick);
                }
            }
        }

        private static void MoveZombies(MatchState state, SurvivalSettings settings)
        {
            if (state.Zombies.Count == 0 || state.AliveCount == 0)
            {
                return;
            }

            int[] open = DistanceField(state, false);
            int[] chewing = null;
            int speed = settings.SpeedIn(state.Wave);

            for (int i = 0; i < state.Zombies.Count; i++)
            {
                Zombie zombie = state.Zombies[i];
                GridPos tile = zombie.Tile;
                int here = open[Index(state.Arena, tile.X, tile.Y)];

                if (here == 0)
                {
                    Approach(zombie, Nearest(state, zombie.Position), speed);
                    continue;
                }

                if (here != Unreached)
                {
                    zombie.ChewTicks = 0;
                    Step(zombie, Downhill(state, open, tile), speed);
                    continue;
                }

                chewing = chewing ?? DistanceField(state, true);
                GridPos next = Downhill(state, chewing, tile);
                if (next == tile)
                {
                    continue;
                }

                if (state.Arena[next] != TileKind.SoftBlock)
                {
                    Step(zombie, next, speed);
                    continue;
                }

                zombie.Facing = Facing(next.X - tile.X, next.Y - tile.Y);
                if (++zombie.ChewTicks >= settings.ChewTicks)
                {
                    zombie.ChewTicks = 0;
                    state.Arena[next] = TileKind.Floor;
                    state.ScheduleRegrowth(next, TileKind.SoftBlock, state.Settings.WallRegrowTicks);
                    state.TryRevealPowerUp(next, out _);
                }
            }
        }

        private static void Bite(MatchState state, SurvivalSettings settings)
        {
            for (int p = 0; p < state.Players.Count; p++)
            {
                PlayerState player = state.Players[p];
                if (!player.Alive)
                {
                    continue;
                }

                for (int z = 0; z < state.Zombies.Count; z++)
                {
                    SubPos at = state.Zombies[z].Position;
                    if (System.Math.Abs(at.X - player.Position.X) <= settings.ContactReach
                        && System.Math.Abs(at.Y - player.Position.Y) <= settings.ContactReach)
                    {
                        player.Alive = false;
                        break;
                    }
                }
            }
        }

        private static int[] DistanceField(MatchState state, bool throughSoftBlocks)
        {
            Arena arena = state.Arena;
            var field = new int[arena.Width * arena.Height];
            for (int i = 0; i < field.Length; i++)
            {
                field[i] = Unreached;
            }

            var queue = new Queue<GridPos>();
            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                int at = Index(arena, player.Tile.X, player.Tile.Y);
                if (player.Alive && arena.Contains(player.Tile) && field[at] != 0)
                {
                    field[at] = 0;
                    queue.Enqueue(player.Tile);
                }
            }

            while (queue.Count > 0)
            {
                GridPos current = queue.Dequeue();
                int steps = field[Index(arena, current.X, current.Y)] + 1;

                for (int i = 0; i < Cardinals.Length; i++)
                {
                    GridPos next = current.Offset(Cardinals[i].X, Cardinals[i].Y);
                    if (!arena.Contains(next) || field[Index(arena, next.X, next.Y)] != Unreached)
                    {
                        continue;
                    }

                    TileKind kind = arena[next];
                    bool passable = Tiles.CanBeStoodOn(kind) || (throughSoftBlocks && kind == TileKind.SoftBlock);
                    if (!passable || state.HasBombAt(next))
                    {
                        continue;
                    }

                    field[Index(arena, next.X, next.Y)] = steps;
                    queue.Enqueue(next);
                }
            }

            return field;
        }

        private static GridPos Downhill(MatchState state, int[] field, GridPos from)
        {
            GridPos best = from;
            int bestSteps = field[Index(state.Arena, from.X, from.Y)];

            for (int i = 0; i < Cardinals.Length; i++)
            {
                GridPos next = from.Offset(Cardinals[i].X, Cardinals[i].Y);
                if (!state.Arena.Contains(next))
                {
                    continue;
                }

                int steps = field[Index(state.Arena, next.X, next.Y)];
                if (steps < bestSteps)
                {
                    best = next;
                    bestSteps = steps;
                }
            }

            return best;
        }

        private static void Step(Zombie zombie, GridPos next, int speed)
        {
            GridPos tile = zombie.Tile;
            if (next == tile)
            {
                return;
            }

            SubPos at = zombie.Position;
            if (next.X != tile.X)
            {
                int centreY = SubPos.CentreOf(tile.Y);
                if (at.Y != centreY)
                {
                    zombie.Position = at.WithY(Toward(at.Y, centreY, speed));
                    return;
                }
            }
            else
            {
                int centreX = SubPos.CentreOf(tile.X);
                if (at.X != centreX)
                {
                    zombie.Position = at.WithX(Toward(at.X, centreX, speed));
                    return;
                }
            }

            Approach(zombie, SubPos.AtTileCentre(next), speed);
        }

        private static void Approach(Zombie zombie, SubPos target, int speed)
        {
            SubPos at = zombie.Position;
            int dx = target.X - at.X;
            int dy = target.Y - at.Y;
            if (dx == 0 && dy == 0)
            {
                return;
            }

            zombie.Facing = Facing(dx, dy);
            zombie.Position = new SubPos(Toward(at.X, target.X, speed), Toward(at.Y, target.Y, speed));
        }

        private static SubPos Nearest(MatchState state, SubPos from)
        {
            SubPos best = from;
            long bestDistance = long.MaxValue;
            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                if (!player.Alive)
                {
                    continue;
                }

                long dx = player.Position.X - from.X;
                long dy = player.Position.Y - from.Y;
                long distance = (dx * dx) + (dy * dy);
                if (distance < bestDistance)
                {
                    best = player.Position;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static int Toward(int from, int to, int speed)
        {
            if (from < to)
            {
                return from + speed > to ? to : from + speed;
            }

            return from - speed < to ? to : from - speed;
        }

        private static Direction Facing(int dx, int dy)
        {
            if (System.Math.Abs(dx) >= System.Math.Abs(dy))
            {
                return dx >= 0 ? Direction.Right : Direction.Left;
            }

            return dy >= 0 ? Direction.Down : Direction.Up;
        }

        private static int Index(Arena arena, int x, int y)
        {
            return (y * arena.Width) + x;
        }
    }
}
