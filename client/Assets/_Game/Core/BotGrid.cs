using System.Collections.Generic;

namespace Blastlands.Core
{
    internal static class BotGrid
    {
        internal static readonly Direction[] Order =
        {
            Direction.Right,
            Direction.Down,
            Direction.Left,
            Direction.Up
        };

        internal static bool Walkable(MatchState state, GridPos tile)
        {
            return state.Arena.Contains(tile)
                && Tiles.CanBeStoodOn(state.Arena[tile])
                && !state.HasBombAt(tile)
                && !IsClosing(state, tile)
                && !BotZombies.NextToAZombie(state, tile);
        }

        internal static bool TouchesSoftBlock(MatchState state, GridPos tile)
        {
            for (int i = 0; i < Order.Length; i++)
            {
                GridPos delta = Directions.Delta(Order[i]);
                GridPos next = tile.Offset(delta.X, delta.Y);
                if (state.Arena.Contains(next) && Tiles.CanBeDestroyed(state.Arena[next]))
                {
                    return true;
                }
            }

            return false;
        }

        internal static List<ActiveBomb> WithBombAt(MatchState state, PlayerState player, GridPos tile)
        {
            var bombs = new List<ActiveBomb>(state.Bombs.Count + 1);
            for (int i = 0; i < state.Bombs.Count; i++)
            {
                bombs.Add(state.Bombs[i]);
            }

            bombs.Add(new ActiveBomb(
                new Bomb(tile, player.Id, player.FireRange, player.NextBombKind), state.Settings.FuseTicks));

            return bombs;
        }

        // Whether a bomb at `from` would cover `target`.
        //
        // What this replaces, EnemyInBlastLine, walked the four cardinals. That was right
        // while a blast was a cross and has been wrong since it became a disc: a target
        // standing diagonally beside the bot did not count, according to a bot whose bomb
        // would have covered them. Radius and line of sight, the way ExplosionResolver
        // reads it, so the two cannot drift apart again.
        internal static bool Reaches(Arena arena, GridPos from, GridPos target, int range)
        {
            int dx = target.X - from.X;
            int dy = target.Y - from.Y;

            if ((dx * dx) + (dy * dy) > range * range)
            {
                return false;
            }

            return LineOfSight.Between(arena, from, target, true);
        }

        // A gap that shuts on the way through is the same death as a blast, and the
        // blast map knows nothing about walls. The bot avoids tiles once they are
        // telegraphed, which is exactly the warning a player gets to act on.
        private static bool IsClosing(MatchState state, GridPos tile)
        {
            for (int i = 0; i < state.RegrowingWalls.Count; i++)
            {
                WallRegrowth wall = state.RegrowingWalls[i];
                if (wall.Tile == tile && wall.TicksRemaining <= state.Settings.WallTelegraphTicks)
                {
                    return true;
                }
            }

            return false;
        }

        private struct Step
        {
            public GridPos Tile;
            public int Depth;
            public Direction First;
        }

        // Breadth-first from `from`, returning the first move of the shortest route to
        // a tile satisfying `accept`. `canPass` keeps the route out of tiles that will
        // already be burning by the time the bot walks through them.
        internal static Direction FirstStepToward(
            MatchState state,
            GridPos from,
            System.Func<GridPos, int, bool> accept,
            System.Func<GridPos, int, bool> canPass)
        {
            var seen = new HashSet<GridPos> { from };
            var queue = new Queue<Step>();

            for (int i = 0; i < Order.Length; i++)
            {
                GridPos delta = Directions.Delta(Order[i]);
                GridPos next = from.Offset(delta.X, delta.Y);

                if (Walkable(state, next) && seen.Add(next) && canPass(next, 1))
                {
                    queue.Enqueue(new Step { Tile = next, Depth = 1, First = Order[i] });
                }
            }

            while (queue.Count > 0)
            {
                Step current = queue.Dequeue();

                if (accept(current.Tile, current.Depth))
                {
                    return current.First;
                }

                for (int i = 0; i < Order.Length; i++)
                {
                    GridPos delta = Directions.Delta(Order[i]);
                    GridPos next = current.Tile.Offset(delta.X, delta.Y);

                    if (Walkable(state, next) && seen.Add(next) && canPass(next, current.Depth + 1))
                    {
                        queue.Enqueue(new Step { Tile = next, Depth = current.Depth + 1, First = current.First });
                    }
                }
            }

            return Direction.None;
        }
    }
}
