using Blastlands.Core;
using UnityEngine;

namespace Blastlands.Runtime
{
    public static class ArenaDressing
    {
        private static readonly GridPos[] CoastSteps =
        {
            new GridPos(1, 0),
            new GridPos(-1, 0),
            new GridPos(0, 1),
            new GridPos(0, -1)
        };

        // One block per tile of ground, and none over the water.
        //
        // The rectangle got a single slab stretched across it, which kept the Synty
        // ground sections at a sane texture scale: they are 15x15 units, and squeezing
        // one into a single tile turns the floor into noise that camouflages the blocks
        // standing on it. An irregular coast cannot be covered by one quad, and nothing
        // can cut a hole in it either, so the top surface has to be built tile by tile.
        //
        // The tiles are plain blocks in the theme's ground colour rather than copies of
        // its ground prefab, which is the same trick from the other side: the colour
        // carries the theme, and the theme's actual ground sections go over the top as
        // detail patches at a size where their texture still reads.
        public static void BuildGround(ViewStage stage, float islandThickness)
        {
            MatchState state = stage.State;
            Color tint = stage.Theme == null ? MatchPalette.Floor : stage.Theme.GroundTint;

            for (int y = 0; y < state.Arena.Height; y++)
            {
                for (int x = 0; x < state.Arena.Width; x++)
                {
                    var tile = new GridPos(x, y);
                    if (state.Arena[tile] == TileKind.Void)
                    {
                        continue;
                    }

                    GameObject block = stage.Spawn(null, PrimitiveType.Cube, tint, "Ground");
                    block.transform.localScale = new Vector3(1f, islandThickness, 1f);
                    block.transform.position = MatchView.ToWorld(tile, -islandThickness * 0.5f);
                }
            }
        }

        // The coast, hung with rock rather than left as the side of a stack of tiles.
        //
        // Ground is built tile by tile, so the island's outline is a staircase of unit
        // squares and every angle above the board shows it. No amount of tinting fixes
        // that: the silhouette is the tell. A cliff face on each edge tile buries the
        // steps behind something with its own shape.
        //
        // Sized from the instance rather than from numbers written here, because these
        // pieces are between six and ten units tall against a tile of one, and every
        // variant differs. Measure, scale to the drop we want, then hang it so its top
        // meets the surface.
        public static void BuildCoast(ViewStage stage, float cliffSpread, float cliffSink)
        {
            MatchState state = stage.State;
            if (stage.Theme == null || !stage.Theme.HasCliffs)
            {
                return;
            }

            for (int y = 0; y < state.Arena.Height; y++)
            {
                for (int x = 0; x < state.Arena.Width; x++)
                {
                    var tile = new GridPos(x, y);
                    if (state.Arena[tile] == TileKind.Void)
                    {
                        continue;
                    }

                    for (int i = 0; i < CoastSteps.Length; i++)
                    {
                        GridPos step = CoastSteps[i];
                        GridPos beyond = tile.Offset(step.X, step.Y);
                        if (state.Arena.Contains(beyond) && state.Arena[beyond] != TileKind.Void)
                        {
                            continue;
                        }

                        HangCliff(stage, tile, step, TileHash.At((x * 4) + i, (y * 4) - i, state.Seed), cliffSpread, cliffSink);
                    }
                }
            }
        }

        private static void HangCliff(ViewStage stage, GridPos tile, GridPos step, int hash, float cliffSpread, float cliffSink)
        {
            GameObject cliff = stage.Spawn(stage.Theme.Cliff(hash), PrimitiveType.Cube, stage.Theme.GroundTint, "Coast");

            // Measured before it is turned, because TryMeasure reports a world-space box.
            // Turned first, a piece hung on an east or west edge is a quarter turn round,
            // so its world X is the mesh's depth rather than its width, and the same
            // cliffSpread buys a very different piece. Measured: 2.60 tiles of cliff on
            // the east edge against 2.03 on the south, a 28% difference decided by
            // nothing but which side of the island you were standing on.
            Bounds authored;
            if (!TileFitter.TryMeasure(cliff, out authored) || authored.size.x <= 0.0001f)
            {
                Object.Destroy(cliff);
                return;
            }

            float spread = 0.85f + (((hash / 11) % 30) / 100f);
            cliff.transform.localScale = Vector3.one * (cliffSpread * spread / authored.size.x);

            var outward = new Vector3(step.X, 0f, -step.Y);
            float jitter = ((hash / 7) % 25) - 12f;
            cliff.transform.rotation = Quaternion.Euler(0f, Quaternion.LookRotation(outward).eulerAngles.y + jitter, 0f);

            Bounds measured;
            if (!TileFitter.TryMeasure(cliff, out measured))
            {
                Object.Destroy(cliff);
                return;
            }

            Vector3 rim = MatchView.ToWorld(tile, 0f) + (outward * 0.75f);
            cliff.transform.position += new Vector3(
                rim.x - measured.center.x,
                -measured.max.y - cliffSink,
                rim.z - measured.center.z);
        }

        // What makes an arena read as a grid is not the texture, it is that everything
        // sits dead centre on a tile. These patches deliberately ignore tile boundaries:
        // placed on a finer lattice, freely rotated and scaled, they cut across the
        // squares and break the eye's habit of reading rows and columns first.
        //
        // They stay flat on purpose. Anything with height on a walkable tile would make
        // the player misjudge where they can walk, and readability outranks decoration.
        public static void BuildGroundDetail(ViewStage stage, int percent, float minSize, float maxSize, float lift)
        {
            MatchState state = stage.State;
            if (stage.Theme == null || !stage.Theme.HasGroundDetail || percent <= 0)
            {
                return;
            }

            const int SubSteps = 2;

            for (int y = 0; y < state.Arena.Height * SubSteps; y++)
            {
                for (int x = 0; x < state.Arena.Width * SubSteps; x++)
                {
                    int hash = TileHash.At(x + 6151, y - 2749, state.Seed);
                    if (hash % 100 >= percent)
                    {
                        continue;
                    }

                    // Patches are laid on a finer lattice than the tiles, so one can
                    // straddle the coast. Anchoring on the tile it starts in keeps them
                    // off the water without clipping any of them.
                    if (state.Arena[new GridPos(x / SubSteps, y / SubSteps)] == TileKind.Void)
                    {
                        continue;
                    }

                    GameObject patch = stage.Spawn(stage.Theme.GroundDetail(hash / 100), PrimitiveType.Quad, MatchPalette.Floor, "GroundDetail");

                    float offsetX = ((hash / 13) % 100) / 100f;
                    float offsetZ = ((hash / 29) % 100) / 100f;
                    var centre = new Vector3(
                        ((x + offsetX) / (float)SubSteps) - 0.5f,
                        lift * (1f + ((hash / 17) % 40) / 40f),
                        -(((y + offsetZ) / (float)SubSteps) - 0.5f));

                    patch.transform.rotation = Quaternion.Euler(0f, hash % 360, 0f);
                    TileFitter.FitInBox(patch, minSize + (((hash / 7) % 100) / 100f * (maxSize - minSize)));
                    TileFitter.PlaceAsGround(patch, centre);
                }
            }
        }

        // Decoration lives off the island and far below it, which is where the arena's
        // surroundings went when the arena started floating.
        //
        // It cannot sit beside the board any more: an island has no outside at this
        // height, only sky. Dropping it gives the sense of a world the island is
        // floating over, and keeps the rule it was written for: nothing decorative
        // inside the playfield, because it would compete with the blocks for attention.
        public static void BuildScenery(ViewStage stage, int ring, int densityPercent, float minSize, float maxSize, float skyDepth)
        {
            MatchState state = stage.State;
            if (stage.Theme == null || !stage.Theme.HasScenery || ring <= 0)
            {
                return;
            }

            for (int y = -ring; y < state.Arena.Height + ring; y++)
            {
                for (int x = -ring; x < state.Arena.Width + ring; x++)
                {
                    // Strictly outside the bounds, not merely off the island. The water
                    // between the coast and the bounds has to stay empty sky: filling it
                    // puts a wall of scenery directly behind the island and the silhouette
                    // the whole thing depends on disappears into it.
                    bool insideBounds = x >= 0 && x < state.Arena.Width && y >= 0 && y < state.Arena.Height;
                    if (insideBounds)
                    {
                        continue;
                    }

                    var tile = new GridPos(x, y);
                    int hash = stage.Variant(tile, 3);
                    if (hash % 100 >= densityPercent)
                    {
                        continue;
                    }

                    GameObject prop = stage.Spawn(stage.Theme.Scenery(hash / 100), PrimitiveType.Cube, MatchPalette.HardBlock, "Scenery");
                    prop.transform.rotation = Quaternion.Euler(0f, hash % 360, 0f);

                    // Wide range on purpose: a building ruin capped at one tile reads as
                    // a pebble, and the surroundings are meant to have a sense of depth.
                    TileFitter.FitInBox(prop, minSize + (((hash / 7) % 100) / 100f * (maxSize - minSize)));
                    TileFitter.PlaceOnTile(prop, MatchView.ToWorld(tile, -skyDepth));
                }
            }
        }
    }
}
