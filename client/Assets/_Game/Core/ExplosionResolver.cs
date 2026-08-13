using System;
using System.Collections.Generic;

namespace Blastlands.Core
{
    public static class ExplosionResolver
    {
        private static readonly GridPos[] Cardinals =
        {
            new GridPos(1, 0),
            new GridPos(-1, 0),
            new GridPos(0, 1),
            new GridPos(0, -1)
        };

        public static ExplosionResult Resolve(Arena arena, IReadOnlyList<Bomb> bombs, IReadOnlyList<int> triggeredBombs)
        {
            if (arena == null)
            {
                throw new ArgumentNullException(nameof(arena));
            }

            if (bombs == null)
            {
                throw new ArgumentNullException(nameof(bombs));
            }

            if (triggeredBombs == null)
            {
                throw new ArgumentNullException(nameof(triggeredBombs));
            }

            return new Pass(arena, bombs).Run(triggeredBombs);
        }

        private sealed class Pass
        {
            private readonly Arena arena;
            private readonly IReadOnlyList<Bomb> bombs;
            private readonly Dictionary<GridPos, int> bombsByTile = new Dictionary<GridPos, int>();
            private readonly Queue<int> pending = new Queue<int>();
            private readonly HashSet<int> queued = new HashSet<int>();
            private readonly List<int> detonated = new List<int>();
            private readonly List<GridPos> flames = new List<GridPos>();
            private readonly HashSet<GridPos> flameTiles = new HashSet<GridPos>();
            private readonly List<GridPos> destroyed = new List<GridPos>();
            private readonly HashSet<GridPos> destroyedTiles = new HashSet<GridPos>();

            public Pass(Arena arena, IReadOnlyList<Bomb> bombs)
            {
                this.arena = arena;
                this.bombs = bombs;

                for (int index = 0; index < bombs.Count; index++)
                {
                    GridPos tile = bombs[index].Position;
                    if (!bombsByTile.ContainsKey(tile))
                    {
                        bombsByTile.Add(tile, index);
                    }
                }
            }

            public ExplosionResult Run(IReadOnlyList<int> triggeredBombs)
            {
                for (int i = 0; i < triggeredBombs.Count; i++)
                {
                    int index = triggeredBombs[i];
                    if (index < 0 || index >= bombs.Count)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(triggeredBombs), index, "Bomb index is outside the bomb list.");
                    }

                    Enqueue(index);
                }

                while (pending.Count > 0)
                {
                    int index = pending.Dequeue();
                    detonated.Add(index);
                    Detonate(bombs[index]);
                }

                return new ExplosionResult(flames, destroyed, detonated);
            }

            private void Detonate(Bomb bomb)
            {
                AddFlame(bomb.Position);

                foreach (GridPos direction in Cardinals)
                {
                    Spread(bomb, direction);
                }
            }

            private void Spread(Bomb bomb, GridPos direction)
            {
                for (int step = 1; step <= bomb.FireRange; step++)
                {
                    GridPos tile = bomb.Position.Offset(direction.X * step, direction.Y * step);
                    if (!arena.Contains(tile))
                    {
                        return;
                    }

                    TileKind kind = arena[tile];
                    if (kind == TileKind.HardBlock)
                    {
                        return;
                    }

                    AddFlame(tile);

                    if (kind == TileKind.SoftBlock)
                    {
                        if (destroyedTiles.Add(tile))
                        {
                            destroyed.Add(tile);
                        }

                        return;
                    }

                    int chained;
                    if (bombsByTile.TryGetValue(tile, out chained))
                    {
                        Enqueue(chained);
                    }
                }
            }

            private void Enqueue(int index)
            {
                if (queued.Add(index))
                {
                    pending.Enqueue(index);
                }
            }

            private void AddFlame(GridPos tile)
            {
                if (flameTiles.Add(tile))
                {
                    flames.Add(tile);
                }
            }
        }
    }
}
