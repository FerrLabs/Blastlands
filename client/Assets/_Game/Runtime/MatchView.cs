using System.Collections.Generic;
using Blastlands.Core;
using Blastlands.Core.Net;
using UnityEngine;

namespace Blastlands.Runtime
{
    // Reads MatchState and draws it. Owns no game state of its own: that separation
    // is what lets the same project build as a headless server.
    public sealed class MatchView : MonoBehaviour
    {
        [SerializeField] private MatchArt art;

        // Swapped per match by the driver. The theme decides what the arena is made of;
        // MatchArt decides what the rules look like.
        [SerializeField] private ArenaTheme theme;
        [SerializeField] private float blockFootprint = 0.92f;
        [SerializeField] private float playerHeight = 1.15f;
        [SerializeField] private float ringRadius = 0.46f;
        [SerializeField] private float ringWidth = 0.1f;

        // How much ground the Run clip covers per second when it is played back at its
        // authored rate, measured off SimpleCharacter_5.0's own root motion. It is what
        // the cadence is a ratio against, so a different animation pack means a different
        // number here rather than characters that skate.
        [SerializeField] private float runClipSpeed = 4.08f;
        [SerializeField] private float bombFootprint = 0.72f;
        [SerializeField] private float powerUpSize = 0.78f;
        [SerializeField] private Color telegraphColor = new Color(0.95f, 0.35f, 0.12f, 1f);
        [SerializeField] private Color telegraphFlashColor = new Color(1f, 0.85f, 0.4f, 1f);
        [SerializeField] private float telegraphPulseHz = 3f;
        [SerializeField] private float telegraphStartSize = 0.3f;
        [SerializeField] private MatchAudio sfx;

        // Told about blasts so it can shake the viewport each one is near. The view is
        // where detonations are noticed, the same place the audio is fired from.
        [SerializeField] private MatchCamera cameras;

        // Particle prefabs carry no useful renderer bounds, so they cannot be measured
        // like meshes. The Synty FX are authored as set dressing and are far too large
        // for a single tile.
        [SerializeField] private float flameScale = 0.55f;
        [SerializeField] private float burstScale = 0.22f;
        [SerializeField] private float flameLifetime = 0.4f;
        [SerializeField] private float burstLifetime = 0.7f;

        [SerializeField] private int sceneryRing = 5;
        [SerializeField] private int sceneryDensityPercent = 26;
        [SerializeField] private float sceneryMinSize = 0.8f;
        [SerializeField] private float sceneryMaxSize = 2.6f;

        [SerializeField] private int groundDetailPercent = 22;
        [SerializeField] private float groundDetailMinSize = 0.9f;
        [SerializeField] private float groundDetailMaxSize = 2.4f;

        // Several of the Synty ground sections are perfectly flat: SA_Env_Concrete has
        // a height of exactly 0. Laid at ground level they end up coplanar with the
        // floor slab and the two fight for depth, which is the striped moiré that looks
        // like a texture bug. Each patch is lifted by a slightly different amount so
        // they do not fight each other either.
        [SerializeField] private float groundDetailLift = 0.02f;

        // Both kinds of cover are sized against the player rather than against the tile.
        // They hide what is behind them in the simulation now, and a knee-high wall that
        // blocks the view of a whole corridor reads as a bug.
        [SerializeField] private float wallHeight = 1.1f;
        [SerializeField] private float bushHeight = 1f;

        // How thin a wall is allowed to get across its short side. The packs' fence and
        // panel meshes are a tenth of their length, and a run of them going away from
        // the camera collapses into a row of pencil lines: correct geometry, unreadable
        // board. The tile blocks completely, so the art may as well look like it does.
        [SerializeField] private float wallThickness = 0.78f;

        // How deep the island is, and how far below it the world sits. The thickness is
        // what turns a flat cutout into something with an edge you can see over.
        [SerializeField] private float islandThickness = 0.9f;

        // How wide one piece of the rock skirt is, in tiles.
        //
        // Scaled from width rather than from height, which was the first attempt and
        // was wrong by a factor of three: these meshes are around twelve units across
        // and nine tall, so sizing them by the drop made each piece three tiles wide
        // and the skirt ate the outer ring of the board.
        [SerializeField] private float cliffSpread = 1.7f;

        // How far the top of the skirt sits below the walkable surface. Under it rather
        // than level with it, because these pieces are grassed on top and that grass has
        // nothing to do with the theme standing on the island.
        [SerializeField] private float cliffSink = 0.12f;
        [SerializeField] private float skyDepth = 14f;

        private readonly Dictionary<GridPos, BlockView> blocks = new Dictionary<GridPos, BlockView>();
        private readonly List<GameObject> bombPool = new List<GameObject>();
        private readonly List<GameObject> looseBombPool = new List<GameObject>();
        private readonly List<GameObject> telegraphPool = new List<GameObject>();
        private MaterialPropertyBlock telegraphBlock;
        private Vector3 telegraphFullScale = Vector3.one;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Speed = Animator.StringToHash("Speed_f");
        private static readonly int Static = Animator.StringToHash("Static_b");
        private readonly List<Vector3> bombBaseScales = new List<Vector3>();
        private readonly List<GameObject> flamePool = new List<GameObject>();
        private readonly List<GameObject> playerViews = new List<GameObject>();
        private readonly List<GameObject> powerUpViews = new List<GameObject>();
        private readonly List<PowerUpKind> powerUpKinds = new List<PowerUpKind>();
        private readonly HashSet<GridPos> bombTiles = new HashSet<GridPos>();
        private readonly HashSet<GridPos> warnedFuses = new HashSet<GridPos>();
        private readonly List<GridPos> staleFuses = new List<GridPos>();
        private readonly List<GridPos> pickupTiles = new List<GridPos>();
        private readonly List<GridPos> detonated = new List<GridPos>();
        private readonly List<GridPos> freshFlames = new List<GridPos>();
        private readonly List<bool> wasAlive = new List<bool>();
        private readonly List<Animator> playerAnimators = new List<Animator>();
        private readonly List<SubPos> lastSampled = new List<SubPos>();
        private int lastSampledTick = -1;
        private readonly HashSet<GridPos> burningTiles = new HashSet<GridPos>();
        private PlayerTrail trail;
        private InterpolationClock clock;
        private int ownSeat = -1;

        private MatchState state;
        private Transform root;

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

        private static readonly GridPos[] CoastSteps =
        {
            new GridPos(1, 0),
            new GridPos(-1, 0),
            new GridPos(0, 1),
            new GridPos(0, -1)
        };

        public static Vector3 ToWorld(GridPos tile, float height)
        {
            return new Vector3(tile.X, height, -tile.Y);
        }

        public static Vector3 ToWorld(SubPos position, float height)
        {
            return new Vector3(
                (position.X / (float)SubPos.UnitsPerTile) - 0.5f,
                height,
                -((position.Y / (float)SubPos.UnitsPerTile) - 0.5f));
        }

        // Set before Bind. Changing it later would leave an arena half built out of
        // two themes, which is worse than either of them.
        public void UseTheme(ArenaTheme next)
        {
            theme = next;
        }

        public void Bind(MatchState matchState)
        {
            state = matchState;

            if (root != null)
            {
                Destroy(root.gameObject);
            }

            var holder = new GameObject("MatchView");
            root = holder.transform;
            root.SetParent(transform, false);

            blocks.Clear();
            bombPool.Clear();
            looseBombPool.Clear();
            telegraphPool.Clear();
            bombBaseScales.Clear();
            flamePool.Clear();
            playerViews.Clear();
            powerUpViews.Clear();
            powerUpKinds.Clear();
            bombTiles.Clear();
            warnedFuses.Clear();
            staleFuses.Clear();
            pickupTiles.Clear();
            detonated.Clear();
            wasAlive.Clear();
            playerAnimators.Clear();
            lastSampled.Clear();
            lastSampledTick = -1;
            burningTiles.Clear();

            BuildGround();
            BuildCoast();
            BuildGroundDetail();
            BuildScenery();
            SyncBlocks();
            BuildPlayers();
        }

        public void Interpolate(PlayerTrail playerTrail, InterpolationClock serverClock, int seat)
        {
            trail = playerTrail;
            clock = serverClock;
            ownSeat = seat;
        }

        private int TicksPast
        {
            get { return clock == null || !clock.Started ? 0 : clock.TicksPast(state.Tick); }
        }

        private SubPos Placement(int index, PlayerState player)
        {
            if (trail == null || clock == null || index == ownSeat
                || !trail.TrySample(index, clock.RenderTime, out SubPos sampled))
            {
                return player.Position;
            }

            return sampled;
        }

        public void Render()
        {
            if (state == null)
            {
                return;
            }

            SyncBlocks();
            SyncTelegraphs();
            SyncPowerUps();
            SyncLooseBombs();
            SyncBombs();
            SyncFlames();
            SyncPlayers();
        }

        private int Variant(GridPos tile, int salt)
        {
            return TileHash.At(tile.X + (salt * 977), tile.Y - (salt * 389), state.Seed);
        }

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
        private void BuildGround()
        {
            Color tint = theme == null ? MatchPalette.Floor : theme.GroundTint;

            for (int y = 0; y < state.Arena.Height; y++)
            {
                for (int x = 0; x < state.Arena.Width; x++)
                {
                    var tile = new GridPos(x, y);
                    if (state.Arena[tile] == TileKind.Void)
                    {
                        continue;
                    }

                    GameObject block = Spawn(null, PrimitiveType.Cube, tint, "Ground");
                    block.transform.localScale = new Vector3(1f, islandThickness, 1f);
                    block.transform.position = ToWorld(tile, -islandThickness * 0.5f);
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
        private void BuildCoast()
        {
            if (theme == null || !theme.HasCliffs)
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

                        HangCliff(tile, step, TileHash.At((x * 4) + i, (y * 4) - i, state.Seed));
                    }
                }
            }
        }

        private void HangCliff(GridPos tile, GridPos step, int hash)
        {
            GameObject cliff = Spawn(theme.Cliff(hash), PrimitiveType.Cube, theme.GroundTint, "Coast");

            // Measured before it is turned, because TryMeasure reports a world-space box.
            // Turned first, a piece hung on an east or west edge is a quarter turn round,
            // so its world X is the mesh's depth rather than its width, and the same
            // cliffSpread buys a very different piece. Measured: 2.60 tiles of cliff on
            // the east edge against 2.03 on the south, a 28% difference decided by
            // nothing but which side of the island you were standing on.
            Bounds authored;
            if (!TileFitter.TryMeasure(cliff, out authored) || authored.size.x <= 0.0001f)
            {
                Destroy(cliff);
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
                Destroy(cliff);
                return;
            }

            Vector3 rim = ToWorld(tile, 0f) + (outward * 0.75f);
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
        private void BuildGroundDetail()
        {
            if (theme == null || !theme.HasGroundDetail || groundDetailPercent <= 0)
            {
                return;
            }

            const int SubSteps = 2;

            for (int y = 0; y < state.Arena.Height * SubSteps; y++)
            {
                for (int x = 0; x < state.Arena.Width * SubSteps; x++)
                {
                    int hash = TileHash.At(x + 6151, y - 2749, state.Seed);
                    if (hash % 100 >= groundDetailPercent)
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

                    GameObject patch = Spawn(theme.GroundDetail(hash / 100), PrimitiveType.Quad, MatchPalette.Floor, "GroundDetail");

                    float offsetX = ((hash / 13) % 100) / 100f;
                    float offsetZ = ((hash / 29) % 100) / 100f;
                    var centre = new Vector3(
                        ((x + offsetX) / (float)SubSteps) - 0.5f,
                        groundDetailLift * (1f + ((hash / 17) % 40) / 40f),
                        -(((y + offsetZ) / (float)SubSteps) - 0.5f));

                    patch.transform.rotation = Quaternion.Euler(0f, hash % 360, 0f);
                    TileFitter.FitInBox(patch, groundDetailMinSize
                        + (((hash / 7) % 100) / 100f * (groundDetailMaxSize - groundDetailMinSize)));
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
        private void BuildScenery()
        {
            if (theme == null || !theme.HasScenery || sceneryRing <= 0)
            {
                return;
            }

            for (int y = -sceneryRing; y < state.Arena.Height + sceneryRing; y++)
            {
                for (int x = -sceneryRing; x < state.Arena.Width + sceneryRing; x++)
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
                    int hash = Variant(tile, 3);
                    if (hash % 100 >= sceneryDensityPercent)
                    {
                        continue;
                    }

                    GameObject prop = Spawn(theme.Scenery(hash / 100), PrimitiveType.Cube, MatchPalette.HardBlock, "Scenery");
                    prop.transform.rotation = Quaternion.Euler(0f, hash % 360, 0f);

                    // Wide range on purpose: a building ruin capped at one tile reads as
                    // a pebble, and the surroundings are meant to have a sense of depth.
                    TileFitter.FitInBox(prop, sceneryMinSize + (((hash / 7) % 100) / 100f * (sceneryMaxSize - sceneryMinSize)));
                    TileFitter.PlaceOnTile(prop, ToWorld(tile, -skyDepth));
                }
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
            int variant = hard ? 0 : Variant(tile, 5);

            GameObject prefab = null;
            if (theme != null)
            {
                prefab = hard ? theme.HardBlock(variant) : bush ? theme.Bush(variant) : theme.SoftBlock(variant);
            }

            Color fallback = hard ? MatchPalette.HardBlock : bush ? MatchPalette.Bush : MatchPalette.SoftBlock;

            GameObject block = Spawn(prefab, PrimitiveType.Cube, fallback, kind.ToString());

            float height = bush ? bushHeight : wallHeight;

            if (prefab == null)
            {
                block.transform.localScale = new Vector3(blockFootprint, hard ? 1f : height, blockFootprint);
                block.transform.position = ToWorld(tile, (hard ? 1f : height) * 0.5f);
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
                TileFitter.FitToTile(block, blockFootprint, height);

                if (!bush)
                {
                    Thicken(block, wallThickness);
                }
            }

            TileFitter.PlaceOnTile(block, ToWorld(tile, 0f));

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
            return state.Arena.Contains(tile) && state.Arena[tile] == TileKind.SoftBlock;
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

        // Two-way, and keyed on the kind rather than on "is it still an obstacle". The
        // old version only ever destroyed views, which was invisible while walls stayed
        // destroyed: once they started growing back, the arena filled with tiles you
        // walked into and could not see. Comparing kinds also covers the case a tile
        // changes what it is, which regrowth does every time a bush burns.
        private void SyncBlocks()
        {
            int broken = 0;

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
                        Destroy(view.Instance);
                        blocks.Remove(tile);

                        // Only where something stood and now nothing does. A block
                        // turning into another block is the walls growing back, and a
                        // block turning into void is the coast falling away in sudden
                        // death: neither of those is a blast taking a wall apart.
                        if (kind == TileKind.Floor)
                        {
                            broken++;
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
            if (broken > 0 && sfx != null)
            {
                sfx.BlockBroken(broken);
            }
        }

        // Pickups are pooled per kind rather than in one list: a pool entry keeps the
        // prefab it was built from, so a bomb-up view cannot end up standing in for a
        // fire-up when the list shifts.
        private void SyncPowerUps()
        {
            for (int i = 0; i < powerUpViews.Count; i++)
            {
                powerUpViews[i].SetActive(false);
            }

            for (int i = 0; i < state.PowerUps.Count; i++)
            {
                PowerUp pickup = state.PowerUps[i];
                GameObject view = TakePowerUpView(pickup.Kind);
                if (view == null)
                {
                    continue;
                }

                float bob = Mathf.Sin((Time.time * 2.6f) + (pickup.Tile.X + pickup.Tile.Y)) * 0.08f;
                view.transform.position = ToWorld(pickup.Tile, 0.42f + bob);
                view.transform.rotation = Quaternion.Euler(0f, Time.time * 70f, 0f);
                view.SetActive(true);
            }

            ReportCollectedPickups();
        }

        // A pickup leaves the state either because someone walked onto it or because a
        // blast took it. The flame still burning on the tile is what tells them apart.
        private void ReportCollectedPickups()
        {
            for (int i = 0; i < pickupTiles.Count; i++)
            {
                GridPos tile = pickupTiles[i];
                if (state.PowerUpIndexAt(tile) < 0 && !state.HasFlameAt(tile) && sfx != null)
                {
                    sfx.PickedUp();
                }
            }

            pickupTiles.Clear();
            for (int i = 0; i < state.PowerUps.Count; i++)
            {
                pickupTiles.Add(state.PowerUps[i].Tile);
            }
        }

        private GameObject TakePowerUpView(PowerUpKind kind)
        {
            for (int i = 0; i < powerUpViews.Count; i++)
            {
                if (!powerUpViews[i].activeSelf && powerUpKinds[i] == kind)
                {
                    return powerUpViews[i];
                }
            }

            GameObject prefab = art == null ? null : art.PowerUp(kind);
            GameObject created = Spawn(prefab, PrimitiveType.Capsule, MatchPalette.ForPlayer((int)kind), "PowerUp " + kind);

            if (prefab == null)
            {
                created.transform.localScale = Vector3.one * 0.34f;
            }
            else
            {
                TileFitter.FitInBox(created, powerUpSize);
            }

            powerUpViews.Add(created);
            powerUpKinds.Add(kind);
            return created;
        }

        // Shown for the last stretch of a wall's countdown and not before. Binary rather
        // than a creeping fill: the player only needs to learn one thing, that tape
        // means this tile is about to stop being one.
        private void SyncTelegraphs()
        {
            GameObject prefab = art == null ? null : art.WallTelegraph;
            if (prefab == null)
            {
                return;
            }

            int shown = 0;
            for (int i = 0; i < state.RegrowingWalls.Count; i++)
            {
                WallRegrowth wall = state.RegrowingWalls[i];
                if (wall.TicksRemaining > state.Settings.WallTelegraphTicks)
                {
                    continue;
                }

                bool created = telegraphPool.Count <= shown;
                GameObject view = TakeAt(telegraphPool, shown, prefab, PrimitiveType.Quad, MatchPalette.Flame, "Closing");

                if (created)
                {
                    TileFitter.FitInBox(view, 0.92f);
                    telegraphFullScale = view.transform.localScale;
                }

                float closing = 1f - ((float)wall.TicksRemaining / Mathf.Max(1, state.Settings.WallTelegraphTicks));
                view.transform.localScale = telegraphFullScale * Mathf.Lerp(telegraphStartSize, 1f, closing);
                PulseTelegraph(view);

                TileFitter.PlaceAsGround(view, ToWorld(wall.Tile, groundDetailLift * 2f));
                shown++;
            }

            HideFrom(telegraphPool, shown);
        }

        // A property block rather than a material instance: the decal is Synty's, and
        // the warning colour is ours to put on top of it without editing the asset.
        private void PulseTelegraph(GameObject view)
        {
            telegraphBlock ??= new MaterialPropertyBlock();
            float pulse = 0.5f + (0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * telegraphPulseHz));
            telegraphBlock.SetColor(BaseColor, Color.Lerp(telegraphColor, telegraphFlashColor, pulse));

            foreach (Renderer renderer in view.GetComponentsInChildren<Renderer>(true))
            {
                renderer.SetPropertyBlock(telegraphBlock);
            }
        }

        // Deliberately the same prefab as a live bomb, sat flat on the floor and left
        // still. A pickup that looked like something else would have players learning
        // two shapes for one object; what separates them is that this one is not ticking.
        private void SyncLooseBombs()
        {
            for (int i = 0; i < state.LooseBombs.Count; i++)
            {
                bool created = looseBombPool.Count <= i;
                GameObject view = TakeAt(
                    looseBombPool, i, art != null ? art.Bomb : null, PrimitiveType.Sphere, MatchPalette.Bomb, "Loose bomb");

                if (created)
                {
                    TileFitter.FitInBox(view, bombFootprint * 0.72f);
                }

                TileFitter.PlaceOnTile(view, ToWorld(state.LooseBombs[i], 0f));
            }

            HideFrom(looseBombPool, state.LooseBombs.Count);
        }

        private void SyncBombs()
        {
            for (int i = 0; i < state.Bombs.Count; i++)
            {
                bool created = bombPool.Count <= i;
                GameObject view = TakeAt(bombPool, i, art != null ? art.Bomb : null, PrimitiveType.Sphere, MatchPalette.Bomb, "Bomb");

                // FitInBox multiplies the current scale, so it runs once per instance
                // and the fuse pulse is applied on top of the scale it settled on.
                if (created)
                {
                    TileFitter.FitInBox(view, bombFootprint);
                    bombBaseScales.Add(view.transform.localScale);
                }

                ActiveBomb bomb = state.Bombs[i];
                float fuse = SnapshotAge.FuseAfter(bomb, TicksPast) / (float)bomb.FuseTicks;
                float pulse = 1f + (0.14f * Mathf.Sin((1f - fuse) * 34f));

                view.transform.localScale = bombBaseScales[i] * pulse;
                TileFitter.PlaceOnTile(view, ToWorld(bomb.Position, 0f));
            }

            HideFrom(bombPool, state.Bombs.Count);
            ReportNewBombs();
        }

        // Counting bombs would miss the tick where one detonates and another is dropped,
        // so the tiles are diffed instead. A bomb tile that is gone and now alight is a
        // bomb that went off, which is where the blast is staged from.
        private void ReportNewBombs()
        {
            for (int i = 0; i < state.Bombs.Count; i++)
            {
                ActiveBomb bomb = state.Bombs[i];
                if (sfx == null)
                {
                    continue;
                }

                if (!bombTiles.Contains(bomb.Bomb.Position))
                {
                    sfx.BombDropped();
                }
                else if (BombFuse.IsWarning(SnapshotAge.FuseAfter(bomb, TicksPast), bomb.FuseTicks, state.Settings.TicksPerSecond)
                         && warnedFuses.Add(bomb.Bomb.Position))
                {
                    // Once per bomb rather than once per frame, which is what the set is
                    // for. The warning is the moment it enters its last second, and a
                    // clip restarted sixty times over that second is a buzz.
                    sfx.FuseBurningDown();
                }
            }

            detonated.Clear();
            foreach (GridPos tile in bombTiles)
            {
                if (!state.HasBombAt(tile) && state.HasFlameAt(tile))
                {
                    detonated.Add(tile);
                }
            }

            bombTiles.Clear();
            for (int i = 0; i < state.Bombs.Count; i++)
            {
                bombTiles.Add(state.Bombs[i].Bomb.Position);
            }

            // Collected first and removed after, because a set cannot be written to
            // while it is being read. Spelled out rather than handed to RemoveWhere: the
            // predicate would capture this and allocate a delegate on every frame, and
            // nothing else on this path allocates.
            staleFuses.Clear();
            foreach (GridPos tile in warnedFuses)
            {
                if (!bombTiles.Contains(tile))
                {
                    staleFuses.Add(tile);
                }
            }

            for (int i = 0; i < staleFuses.Count; i++)
            {
                warnedFuses.Remove(staleFuses[i]);
            }
        }

        private void SyncFlames()
        {
            bool hasArt = art != null && art.Flame != null;

            int ticksPast = TicksPast;
            int shown = 0;

            for (int f = 0; f < state.Flames.Count; f++)
            {
                ActiveFlame flame = state.Flames[f];
                if (!SnapshotAge.BurnsAfter(flame, ticksPast))
                {
                    continue;
                }

                int i = shown++;
                bool created = flamePool.Count <= i;
                GameObject view = TakeAt(flamePool, i, art != null ? art.Flame : null, PrimitiveType.Cube, MatchPalette.Flame, "Flame");

                if (created)
                {
                    if (hasArt)
                    {
                        view.transform.localScale = Vector3.one * flameScale;
                        TuneParticles(view, flameLifetime);
                    }
                    else
                    {
                        view.transform.localScale = new Vector3(0.94f, 0.5f, 0.94f);
                    }
                }

                view.transform.position = ToWorld(flame.Tile, hasArt ? 0f : 0.25f);
            }

            HideFrom(flamePool, shown);
            EmitBursts();
        }

        // The Synty FX are authored as scenery: they simulate in world space, ignore
        // transform scale for particle size, and run for seconds. Pooled objects move
        // between tiles, so world-space particles smear across the arena and the long
        // lifetimes pile up into one plume. Retune the instance, never the source asset.
        private static void TuneParticles(GameObject instance, float lifetime)
        {
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.MainModule main = systems[i].main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.startLifetime = lifetime;

                // The smoke layer is authored to start a full second in, which reads as
                // the explosion smoking a beat after it went off. A blast is one moment.
                main.startDelay = 0f;
            }
        }

        // One burst per bomb that went off, staged where the bomb was. Firing one per
        // burning tile meant a single range-three blast stacked nine smoke plumes on
        // top of each other and the arena stayed fogged in. The flames along the arms
        // already draw the shape of the blast.
        // How much of this frame's new fire belongs to one detonation, by giving every
        // fresh tile to the blast it is nearest to.
        //
        // The frame total is the wrong number to hand a camera. A single bomb going off
        // in the same frame as a chain reaction across the board would be described by
        // the chain's size and shake as hard as it did, which is a small pop throwing the
        // view like a nine-tile blast.
        private int FlamesOf(int detonation)
        {
            if (detonated.Count == 1)
            {
                return freshFlames.Count;
            }

            GridPos at = detonated[detonation];
            int mine = 0;

            for (int i = 0; i < freshFlames.Count; i++)
            {
                if (Nearest(freshFlames[i]) == detonation)
                {
                    mine++;
                }
            }

            return mine;
        }

        // Ties go to the first, which only decides which of two equally close blasts
        // counts a tile it is equally entitled to.
        private int Nearest(GridPos flame)
        {
            int best = 0;
            int shortest = int.MaxValue;

            for (int i = 0; i < detonated.Count; i++)
            {
                int dx = flame.X - detonated[i].X;
                int dy = flame.Y - detonated[i].Y;
                int distance = (dx * dx) + (dy * dy);

                if (distance < shortest)
                {
                    shortest = distance;
                    best = i;
                }
            }

            return best;
        }

        private void EmitBursts()
        {
            GameObject prefab = art == null ? null : art.ExplosionBurst;

            freshFlames.Clear();
            for (int i = 0; i < state.Flames.Count; i++)
            {
                if (!burningTiles.Contains(state.Flames[i].Tile))
                {
                    freshFlames.Add(state.Flames[i].Tile);
                }
            }

            int caught = freshFlames.Count;

            if (prefab != null)
            {
                for (int i = 0; i < detonated.Count; i++)
                {
                    GameObject burst = Instantiate(prefab, ToWorld(detonated[i], 0.05f), Quaternion.identity, root);
                    burst.transform.localScale = Vector3.one * burstScale;
                    TuneParticles(burst, burstLifetime);
                    Destroy(burst, 2f);
                }
            }

            if (caught > 0 && sfx != null)
            {
                sfx.Exploded(caught);
            }

            if (caught > 0 && cameras != null)
            {
                for (int i = 0; i < detonated.Count; i++)
                {
                    cameras.Felt(ToWorld(detonated[i], 0f), FlamesOf(i));
                }
            }

            burningTiles.Clear();
            for (int i = 0; i < state.Flames.Count; i++)
            {
                burningTiles.Add(state.Flames[i].Tile);
            }
        }

        private void BuildPlayers()
        {
            for (int i = 0; i < state.Players.Count; i++)
            {
                GameObject prefab = art == null ? null : art.PlayerFor(i);
                GameObject view = Spawn(prefab, PrimitiveType.Capsule, MatchPalette.ForPlayer(i), "Player " + i);

                if (prefab == null)
                {
                    view.transform.localScale = new Vector3(0.62f, 0.42f, 0.62f);
                }
                else
                {
                    TileFitter.FitToHeight(view, playerHeight);
                }

                PlayerRing.Attach(view, MatchPalette.ForPlayer(i), ringRadius, ringWidth, groundDetailLift * 4f);
                playerViews.Add(view);
                playerAnimators.Add(Rig(view));
                wasAlive.Add(state.Players[i].Alive);
                lastSampled.Add(state.Players[i].Position);
            }
        }

        // Hands back the animator this view will be driven through, or null for the
        // primitive fallback, which has no rig to drive.
        //
        // The controller is only supplied where the prefab brought none of its own.
        // Overwriting one that is already there would throw away whatever the pack
        // author wired up.
        private Animator Rig(GameObject view)
        {
            var animator = view.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                return null;
            }

            if (animator.runtimeAnimatorController == null && art != null && art.PlayerAnimator != null)
            {
                animator.runtimeAnimatorController = art.PlayerAnimator;
            }

            if (animator.runtimeAnimatorController == null)
            {
                return null;
            }

            // The simulation owns where a player is, down to the sub-tile unit, and it
            // has to stay that way: it is what makes a replay reproduce and what the
            // netcode will reconcile against. Root motion would let the clip push the
            // transform around on top of that, so the character would drift off its own
            // position by however much the animator felt like.
            animator.applyRootMotion = false;

            // Which is why the static variants: Walk and Run travel, Walk_Static and
            // Run_Static are the same strides authored on the spot. Discarding root
            // motion from a travelling clip leaves the feet skating, playing the clip
            // that was drawn for this case does not.
            animator.SetBool(Static, true);

            return animator;
        }

        // The stain stays for the round: in a four-way match you often miss the moment
        // someone dies, and where it happened is worth knowing.
        private void MarkDeath(PlayerState player)
        {
            if (sfx != null)
            {
                sfx.Died();
            }

            GameObject prefab = art == null ? null : art.DeathMarker(player.Id);
            if (prefab == null)
            {
                return;
            }

            int hash = TileHash.At(player.Tile.X, player.Tile.Y, state.Seed + (uint)player.Id);

            GameObject stain = Instantiate(prefab, root);
            stain.name = "Death " + player.Id;
            stain.transform.rotation = Quaternion.Euler(0f, hash % 360, 0f);
            TileFitter.FitInBox(stain, 0.8f + (((hash / 7) % 40) / 100f));

            // Lifted above both the floor and the ground detail, or it z-fights with them.
            Vector3 where = ToWorld(player.Position, groundDetailLift * 3f);
            TileFitter.PlaceAsGround(stain, where);
        }

        private void SyncPlayers()
        {
            PaceAnimators();

            for (int i = 0; i < playerViews.Count && i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                GameObject view = playerViews[i];

                if (!player.Alive)
                {
                    if (i < wasAlive.Count && wasAlive[i])
                    {
                        wasAlive[i] = false;
                        MarkDeath(player);
                    }

                    view.SetActive(false);
                    continue;
                }

                view.SetActive(true);
                view.transform.position = ToWorld(Placement(i, player), 0f);
                view.transform.rotation = Quaternion.Euler(0f, FacingAngle(player.Facing), 0f);
            }
        }

        // Sampled per tick rather than per frame. Render runs every frame and a position
        // only moves on a tick boundary, so a frame-to-frame delta is the true step on
        // the frames a tick landed on and zero on all the others, which at a few hundred
        // frames a second reads as a player who is standing still almost all the time.
        private void PaceAnimators()
        {
            if (state.Tick == lastSampledTick)
            {
                return;
            }

            lastSampledTick = state.Tick;

            for (int i = 0; i < playerAnimators.Count && i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                Animator animator = playerAnimators[i];

                if (animator != null)
                {
                    Gait gait = PlayerPace.For(player, lastSampled[i], state.Settings, runClipSpeed);
                    animator.SetFloat(Speed, gait.Speed);
                    animator.speed = gait.Cadence;
                }

                lastSampled[i] = player.Position;
            }
        }

        public int PlayerViewCount
        {
            get { return playerViews.Count; }
        }

        public GameObject PlayerViewAt(int index)
        {
            return index >= 0 && index < playerViews.Count ? playerViews[index] : null;
        }

        private static float FacingAngle(Direction facing)
        {
            switch (facing)
            {
                case Direction.Right:
                    return 90f;
                case Direction.Left:
                    return 270f;
                case Direction.Up:
                    return 0f;
                default:
                    return 180f;
            }
        }

        private GameObject Spawn(GameObject prefab, PrimitiveType fallbackShape, Color fallbackColor, string label)
        {
            GameObject instance;

            if (prefab != null)
            {
                instance = Instantiate(prefab, root);
            }
            else
            {
                instance = GameObject.CreatePrimitive(fallbackShape);
                instance.transform.SetParent(root, false);
                Collider collider = instance.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                instance.GetComponent<Renderer>().sharedMaterial = MatchPalette.CreateMaterial(fallbackColor);
            }

            instance.name = label;
            return instance;
        }

        private GameObject TakeAt(
            List<GameObject> pool, int index, GameObject prefab, PrimitiveType fallbackShape, Color fallbackColor, string label)
        {
            while (pool.Count <= index)
            {
                pool.Add(Spawn(prefab, fallbackShape, fallbackColor, label + " " + pool.Count));
            }

            pool[index].SetActive(true);
            return pool[index];
        }

        private static void HideFrom(List<GameObject> pool, int from)
        {
            for (int i = from; i < pool.Count; i++)
            {
                pool[i].SetActive(false);
            }
        }
    }
}
