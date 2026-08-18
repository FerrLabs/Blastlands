using System.Collections.Generic;
using Blastlands.Core;
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
        [SerializeField] private float bombFootprint = 0.72f;
        [SerializeField] private float powerUpSize = 0.78f;
        [SerializeField] private Color telegraphColor = new Color(0.95f, 0.35f, 0.12f, 1f);
        [SerializeField] private MatchAudio sfx;

        // Particle prefabs carry no useful renderer bounds, so they cannot be measured
        // like meshes. The Synty FX are authored as set dressing and are far too large
        // for a single tile.
        [SerializeField] private float flameScale = 0.18f;
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

        // Rocks laid out at one uniform size make the lattice look manufactured. A
        // little variation reads as terrain without moving anything off its tile.
        [SerializeField] private float hardBlockSizeJitter = 0.16f;

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
        [SerializeField] private float skyDepth = 14f;

        private readonly Dictionary<GridPos, BlockView> blocks = new Dictionary<GridPos, BlockView>();
        private readonly List<GameObject> bombPool = new List<GameObject>();
        private readonly List<GameObject> looseBombPool = new List<GameObject>();
        private readonly List<GameObject> telegraphPool = new List<GameObject>();
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private readonly List<Vector3> bombBaseScales = new List<Vector3>();
        private readonly List<GameObject> flamePool = new List<GameObject>();
        private readonly List<GameObject> playerViews = new List<GameObject>();
        private readonly List<GameObject> powerUpViews = new List<GameObject>();
        private readonly List<PowerUpKind> powerUpKinds = new List<PowerUpKind>();
        private readonly HashSet<GridPos> bombTiles = new HashSet<GridPos>();
        private readonly List<GridPos> pickupTiles = new List<GridPos>();
        private readonly List<GridPos> detonated = new List<GridPos>();
        private readonly List<bool> wasAlive = new List<bool>();
        private readonly HashSet<GridPos> burningTiles = new HashSet<GridPos>();

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
            pickupTiles.Clear();
            detonated.Clear();
            wasAlive.Clear();
            burningTiles.Clear();

            BuildGround();
            BuildGroundDetail();
            BuildScenery();
            SyncBlocks();
            BuildPlayers();
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
            bool hard = kind == TileKind.HardBlock;
            bool bush = kind == TileKind.Bush;
            int variant = Variant(tile, hard ? 4 : 5);

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

            // Rocks are organic, so any angle suits them. Bushes are shapeless enough
            // that a quarter turn is only there to stop them repeating. A wall is a
            // panel, and which way it faces is the difference between a wall and a stick.
            block.transform.rotation = Quaternion.Euler(
                0f,
                hard ? variant % 360 : bush ? QuarterTurn(variant) : WallAngle(block, tile, variant),
                0f);

            if (hard)
            {
                TileFitter.FitInBox(block, 1f + ((((variant / 11) % 100) / 100f) - 0.5f) * 2f * hardBlockSizeJitter);
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
                    Tint(view, telegraphColor);
                }

                TileFitter.PlaceAsGround(view, ToWorld(wall.Tile, groundDetailLift * 2f));
                shown++;
            }

            HideFrom(telegraphPool, shown);
        }

        // Deliberately the same prefab as a live bomb, sat flat on the floor and left
        // still. A pickup that looked like something else would have players learning
        // two shapes for one object; what separates them is that this one is not ticking.
        // A property block rather than a material instance: the decal is Synty's, and
        // the warning colour is ours to put on top of it without editing the asset.
        private static void Tint(GameObject target, Color color)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, color);

            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                renderer.SetPropertyBlock(block);
            }
        }

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
                float fuse = bomb.FuseRemaining / (float)state.Settings.FuseTicks;
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
                if (!bombTiles.Contains(state.Bombs[i].Bomb.Position) && sfx != null)
                {
                    sfx.BombDropped();
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
        }

        private void SyncFlames()
        {
            bool hasArt = art != null && art.Flame != null;

            for (int i = 0; i < state.Flames.Count; i++)
            {
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

                ActiveFlame flame = state.Flames[i];
                view.transform.position = ToWorld(flame.Tile, hasArt ? 0f : 0.25f);
            }

            HideFrom(flamePool, state.Flames.Count);
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
        private void EmitBursts()
        {
            GameObject prefab = art == null ? null : art.ExplosionBurst;
            int caught = 0;

            for (int i = 0; i < state.Flames.Count; i++)
            {
                if (!burningTiles.Contains(state.Flames[i].Tile))
                {
                    caught++;
                }
            }

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
                    Animate(view);
                }

                playerViews.Add(view);
                wasAlive.Add(state.Players[i].Alive);
            }
        }

        // Only where the prefab brought no controller of its own. Overwriting one that
        // is already there would throw away whatever the pack author wired up.
        private void Animate(GameObject view)
        {
            if (art == null || art.PlayerAnimator == null)
            {
                return;
            }

            var animator = view.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.runtimeAnimatorController == null)
            {
                animator.runtimeAnimatorController = art.PlayerAnimator;
            }
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
                view.transform.position = ToWorld(player.Position, 0f);
                view.transform.rotation = Quaternion.Euler(0f, FacingAngle(player.Facing), 0f);
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
