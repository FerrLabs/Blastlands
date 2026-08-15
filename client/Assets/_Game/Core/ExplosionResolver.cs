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

            // A radius rather than a cross. The cross was legible on a checkerboard,
            // which is the only reason it existed; once a player can stand between two
            // tiles it stops answering the question "am I in it".
            private void Detonate(Bomb bomb)
            {
                int range = bomb.FireRange;
                AddFlame(bomb.Position);

                for (int dy = -range; dy <= range; dy++)
                {
                    for (int dx = -range; dx <= range; dx++)
                    {
                        if ((dx * dx) + (dy * dy) > range * range)
                        {
                            continue;
                        }

                        Reach(bomb, bomb.Position.Offset(dx, dy));
                    }
                }

                if (bomb.Kind == BombKind.Cluster)
                {
                    foreach (GridPos direction in Cardinals)
                    {
                        GridPos tip = FurthestBurning(bomb.Position, direction, range);
                        if (tip != bomb.Position)
                        {
                            Scatter(tip);
                        }
                    }
                }
            }

            private void Reach(Bomb bomb, GridPos tile)
            {
                if (!arena.Contains(tile))
                {
                    return;
                }

                TileKind kind = arena[tile];
                if (kind == TileKind.HardBlock)
                {
                    return;
                }

                // Pierce sees past the crates but not through the structure, which is
                // what it meant on a grid too.
                if (!LineOfSight.Between(arena, bomb.Position, tile, bomb.Kind != BombKind.Pierce))
                {
                    return;
                }

                AddFlame(tile);

                if (kind == TileKind.SoftBlock)
                {
                    Destroy(tile);
                    return;
                }

                int chained;
                if (bombsByTile.TryGetValue(tile, out chained))
                {
                    Enqueue(chained);
                }
            }

            // A radius has no tip, so the cluster's flare hangs off the outermost tile
            // the blast actually reached along each cardinal.
            private GridPos FurthestBurning(GridPos origin, GridPos direction, int range)
            {
                GridPos furthest = origin;

                for (int step = 1; step <= range; step++)
                {
                    GridPos tile = origin.Offset(direction.X * step, direction.Y * step);
                    if (!flameTiles.Contains(tile))
                    {
                        break;
                    }

                    furthest = tile;
                }

                return furthest;
            }

            // Cluster arms flare one tile around where they stopped. The flare is
            // deliberately not itself a cluster, which is what bounds the recursion.
            private void Scatter(GridPos origin)
            {
                foreach (GridPos direction in Cardinals)
                {
                    GridPos tile = origin.Offset(direction.X, direction.Y);
                    if (!arena.Contains(tile))
                    {
                        continue;
                    }

                    TileKind kind = arena[tile];
                    if (kind == TileKind.HardBlock)
                    {
                        continue;
                    }

                    AddFlame(tile);

                    if (kind == TileKind.SoftBlock)
                    {
                        Destroy(tile);
                        continue;
                    }

                    int chained;
                    if (bombsByTile.TryGetValue(tile, out chained))
                    {
                        Enqueue(chained);
                    }
                }
            }

            private void Destroy(GridPos tile)
            {
                if (destroyedTiles.Add(tile))
                {
                    destroyed.Add(tile);
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
