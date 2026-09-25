using System.Collections.Generic;
using Blastlands.Core;
using UnityEngine;

namespace Blastlands.Runtime
{
    public sealed class BlockViews
    {
        private readonly ViewStage stage;
        private readonly float footprint;
        private readonly float wallHeight;
        private readonly float bushHeight;
        private readonly float wallThickness;
        private readonly Dictionary<GridPos, BlockView> blocks = new Dictionary<GridPos, BlockView>();
        private readonly List<GridPos> brokenTiles = new List<GridPos>();

        public BlockViews(ViewStage stage, float footprint, float wallHeight, float bushHeight, float wallThickness)
        {
            this.stage = stage;
            this.footprint = footprint;
            this.wallHeight = wallHeight;
            this.bushHeight = bushHeight;
            this.wallThickness = wallThickness;
        }

        private readonly struct BlockView
        {
            public BlockView(GameObject instance, TileKind kind)
            {
                Instance = instance;
                Kind = kind;
            }

            public GameObject Instance { get; }

            public TileKind Kind { get; }
        }

        // Two-way, and keyed on the kind rather than on "is it still an obstacle". The
        // old version only ever destroyed views, which was invisible while walls stayed
        // destroyed: once they started growing back, the arena filled with tiles you
        // walked into and could not see. Comparing kinds also covers the case a tile
        // changes what it is, which regrowth does every time a bush burns.
        public void Sync()
        {
            MatchState state = stage.State;
            brokenTiles.Clear();

            for (int y = 0; y < state.Arena.Height; y++)
            {
                for (int x = 0; x < state.Arena.Width; x++)
                {
                    var tile = new GridPos(x, y);
                    TileKind kind = state.Arena[tile];
                    bool shown = blocks.TryGetValue(tile, out BlockView view);

                    if (shown && view.Kind == kind)
                    {
                        continue;
                    }

                    if (shown)
                    {
                        Object.Destroy(view.Instance);
                        blocks.Remove(tile);

                        // Only where something stood and now nothing does. A block
                        // turning into another block is the walls growing back, and a
                        // block turning into void is the coast falling away in sudden
                        // death: neither of those is a blast taking a wall apart.
                        if (kind == TileKind.Floor)
                        {
                            brokenTiles.Add(tile);
                        }
                    }

                    // Asked as "is there something to draw here", not as "is this not
                    // floor". The two were the same question until the island arrived,
                    // and the difference put a wall on every tile of open sky: void is
                    // not floor, and CreateBlock treats anything that is neither rock
                    // nor bush as a wall.
                    if (kind != TileKind.Floor && kind != TileKind.Void)
                    {
                        blocks[tile] = new BlockView(CreateBlock(tile, kind), kind);
                    }
                }
            }

            // One cue for the frame, not one per wall. A blast that opens four tiles at
            // once is one collapse to whoever is watching, and four overlapping copies
            // of the same clip is just clipping.
            if (brokenTiles.Count > 0 && stage.Sfx != null)
            {
                stage.Sfx.BlockBroken(brokenTiles.Count, MatchView.ToWorld(stage.NearestToTheEar(brokenTiles), 0f));
            }
        }

        private GameObject CreateBlock(GridPos tile, TileKind kind)
        {
            // Every hard block in the game is a lattice pillar: FillClassic is the only
            // thing that lays one, and it only runs for a Classic board. So they are not
            // dressed as terrain but as structure, all identical, all square to the axes,
            // all exactly one tile. That is the whole readability trick a checkerboard
            // game rests on. Varied meshes on free angles leave the grid intact in the
            // simulation and invisible on screen, and the corridors stop reading as
            // corridors.
            //
            // Wallpaper is the goal here rather than the failure mode the theme warns
            // about: the eye gives up on the pillars and starts reading the gaps, which
            // is where the game happens.
            bool hard = kind == TileKind.HardBlock;
            bool bush = kind == TileKind.Bush;
            int variant = hard ? 0 : stage.Variant(tile, 5);

            GameObject prefab = null;
            if (stage.Theme != null)
            {
                prefab = hard ? stage.Theme.HardBlock(variant) : bush ? stage.Theme.Bush(variant) : stage.Theme.SoftBlock(variant);
            }

            Color fallback = hard ? MatchPalette.HardBlock : bush ? MatchPalette.Bush : MatchPalette.SoftBlock;

            GameObject block = stage.Spawn(prefab, PrimitiveType.Cube, fallback, kind.ToString());

            float height = bush ? bushHeight : wallHeight;

            if (prefab == null)
            {
                block.transform.localScale = new Vector3(footprint, hard ? 1f : height, footprint);
                block.transform.position = MatchView.ToWorld(tile, (hard ? 1f : height) * 0.5f);
                return block;
            }

            // Bushes are shapeless enough that a quarter turn is only there to stop them
            // repeating. A wall is a panel, and which way it faces is the difference
            // between a wall and a stick. Pillars stay square, per the note above.
            block.transform.rotation = Quaternion.Euler(
                0f,
                hard ? 0f : bush ? QuarterTurn(variant) : WallAngle(block, tile, variant),
                0f);

            if (hard)
            {
                TileFitter.FitInBox(block, 1f);
            }
            else
            {
                TileFitter.FitToTile(block, footprint, height);

                if (!bush)
                {
                    Thicken(block, wallThickness);
                }
            }

            TileFitter.PlaceOnTile(block, MatchView.ToWorld(tile, 0f));

            return block;
        }

        private static float QuarterTurn(int variant)
        {
            return (variant % 4) * 90f;
        }

        // Lines a wall panel up with the run of walls it belongs to. Turned at random,
        // the panels that happen to face the camera edge-on read as thin posts rather
        // than as anything you could hide behind, and a row of them reads as a picket
        // fence with gaps that are not there: the tiles block completely.
        //
        // Which way the mesh is long is measured rather than assumed, because the packs
        // disagree. The concrete piece runs along its X, the brick one along its Z, and
        // hard-coding either would be right for exactly one theme.
        private float WallAngle(GameObject block, GridPos tile, int variant)
        {
            bool alongX = IsWall(tile.Offset(1, 0)) || IsWall(tile.Offset(-1, 0));
            bool alongZ = IsWall(tile.Offset(0, 1)) || IsWall(tile.Offset(0, -1));

            // A lone tile, or a junction, has no run to follow. Falling back to the hash
            // keeps those from all facing the same way.
            if (alongX == alongZ)
            {
                return QuarterTurn(variant);
            }

            if (!TileFitter.TryMeasure(block, out Bounds bounds))
            {
                return QuarterTurn(variant);
            }

            bool meshRunsAlongX = bounds.size.x >= bounds.size.z;
            return meshRunsAlongX == alongX ? 0f : 90f;
        }

        private bool IsWall(GridPos tile)
        {
            return stage.State.Arena.Contains(tile) && stage.State.Arena[tile] == TileKind.SoftBlock;
        }

        // Pads the short horizontal side out to a minimum. Which local axis that is
        // depends on the quarter turn the panel was just given, so it is derived from
        // the yaw rather than assumed: getting it backwards stretches the wall along its
        // length and leaves it exactly as thin as before.
        private static void Thicken(GameObject block, float minimum)
        {
            if (!TileFitter.TryMeasure(block, out Bounds bounds))
            {
                return;
            }

            float thin = Mathf.Min(bounds.size.x, bounds.size.z);
            if (thin <= 0.0001f || thin >= minimum)
            {
                return;
            }

            float yaw = block.transform.eulerAngles.y;
            bool quarterTurned = Mathf.Abs(Mathf.DeltaAngle(yaw, 90f)) < 45f
                || Mathf.Abs(Mathf.DeltaAngle(yaw, 270f)) < 45f;

            Vector3 scale = block.transform.localScale;
            if ((bounds.size.x <= bounds.size.z) != quarterTurned)
            {
                scale.x *= minimum / thin;
            }
            else
            {
                scale.z *= minimum / thin;
            }

            block.transform.localScale = scale;
        }
    }
}
