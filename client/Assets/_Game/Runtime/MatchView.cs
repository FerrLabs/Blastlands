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

        private readonly Dictionary<GridPos, GameObject> blocks = new Dictionary<GridPos, GameObject>();
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
            BuildBlocks();
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

        // One slab stretched over the whole area rather than a prefab per tile. The
        // Synty ground sections are 15x15 units: shrinking one into a single tile
        // squeezes its entire texture into that tile, which turns the floor into
        // noise and camouflages the blocks standing on it.
        private void BuildGround()
        {
            int reach = art != null && art.HasScenery ? sceneryRing : 0;
            float width = state.Arena.Width + (reach * 2f);
            float depth = state.Arena.Height + (reach * 2f);
            var centre = new Vector3((state.Arena.Width - 1) * 0.5f, 0f, -(state.Arena.Height - 1) * 0.5f);

            GameObject prefab = art == null ? null : art.FloorTile(0);
            GameObject ground = Spawn(prefab, PrimitiveType.Cube, MatchPalette.Floor, "Ground");

            if (prefab == null)
            {
                ground.transform.localScale = new Vector3(width, 0.2f, depth);
                ground.transform.position = centre + new Vector3(0f, -0.1f, 0f);
                return;
            }

            if (!TileFitter.TryMeasure(ground, out Bounds bounds) || bounds.size.x <= 0.001f || bounds.size.z <= 0.001f)
            {
                return;
            }

            Vector3 scale = ground.transform.localScale;
            ground.transform.localScale = new Vector3(
                scale.x * (width / bounds.size.x),
                scale.y,
                scale.z * (depth / bounds.size.z));

            TileFitter.PlaceAsGround(ground, centre);
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
            if (art == null || !art.HasGroundDetail || groundDetailPercent <= 0)
            {
                return;
            }

            int reach = art.HasScenery ? sceneryRing : 0;
            const int SubSteps = 2;

            for (int y = -reach * SubSteps; y < (state.Arena.Height + reach) * SubSteps; y++)
            {
                for (int x = -reach * SubSteps; x < (state.Arena.Width + reach) * SubSteps; x++)
                {
                    int hash = TileHash.At(x + 6151, y - 2749, state.Seed);
                    if (hash % 100 >= groundDetailPercent)
                    {
                        continue;
                    }

                    GameObject patch = Spawn(art.GroundDetail(hash / 100), PrimitiveType.Quad, MatchPalette.Floor, "GroundDetail");

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

        // Decoration lives strictly outside the arena walls. Anything inside would
        // compete with the blocks for the player's attention, and the whole point of
        // the fixed camera is that the playfield reads at a glance.
        private void BuildScenery()
        {
            if (art == null || !art.HasScenery || sceneryRing <= 0)
            {
                return;
            }

            for (int y = -sceneryRing; y < state.Arena.Height + sceneryRing; y++)
            {
                for (int x = -sceneryRing; x < state.Arena.Width + sceneryRing; x++)
                {
                    bool insideArena = x >= 0 && x < state.Arena.Width && y >= 0 && y < state.Arena.Height;
                    if (insideArena)
                    {
                        continue;
                    }

                    var tile = new GridPos(x, y);
                    int hash = Variant(tile, 3);
                    if (hash % 100 >= sceneryDensityPercent)
                    {
                        continue;
                    }

                    GameObject prop = Spawn(art.Scenery(hash / 100), PrimitiveType.Cube, MatchPalette.HardBlock, "Scenery");
                    prop.transform.rotation = Quaternion.Euler(0f, hash % 360, 0f);

                    // Wide range on purpose: a building ruin capped at one tile reads as
                    // a pebble, and the surroundings are meant to have a sense of depth.
                    TileFitter.FitInBox(prop, sceneryMinSize + (((hash / 7) % 100) / 100f * (sceneryMaxSize - sceneryMinSize)));
                    TileFitter.PlaceOnTile(prop, ToWorld(tile, 0f));
                }
            }
        }

        private void BuildBlocks()
        {
            for (int y = 0; y < state.Arena.Height; y++)
            {
                for (int x = 0; x < state.Arena.Width; x++)
                {
                    var tile = new GridPos(x, y);
                    TileKind kind = state.Arena[tile];
                    if (kind != TileKind.Floor)
                    {
                        blocks[tile] = CreateBlock(tile, kind);
                    }
                }
            }
        }

        private GameObject CreateBlock(GridPos tile, TileKind kind)
        {
            bool hard = kind == TileKind.HardBlock;
            int variant = Variant(tile, hard ? 4 : 5);

            GameObject prefab = art == null ? null : (hard ? art.HardBlock(variant) : art.SoftBlock(variant));
            Color fallback = hard ? MatchPalette.HardBlock : MatchPalette.SoftBlock;

            GameObject block = Spawn(prefab, PrimitiveType.Cube, fallback, kind.ToString());

            if (prefab == null)
            {
                block.transform.localScale = new Vector3(blockFootprint, 1f, blockFootprint);
                block.transform.position = ToWorld(tile, 0.5f);
                return block;
            }

            // Rocks are organic, so any angle suits them; crates and barrels only look
            // right on a quarter turn.
            block.transform.rotation = Quaternion.Euler(0f, hard ? variant % 360 : QuarterTurn(variant), 0f);

            float size = hard
                ? 1f + ((((variant / 11) % 100) / 100f) - 0.5f) * 2f * hardBlockSizeJitter
                : blockFootprint;

            TileFitter.FitInBox(block, size);
            TileFitter.PlaceOnTile(block, ToWorld(tile, 0f));

            return block;
        }

        private static float QuarterTurn(int variant)
        {
            return (variant % 4) * 90f;
        }

        private void SyncBlocks()
        {
            List<GridPos> cleared = null;

            foreach (KeyValuePair<GridPos, GameObject> entry in blocks)
            {
                if (state.Arena[entry.Key] == TileKind.Floor)
                {
                    if (cleared == null)
                    {
                        cleared = new List<GridPos>();
                    }

                    cleared.Add(entry.Key);
                }
            }

            if (cleared == null)
            {
                return;
            }

            for (int i = 0; i < cleared.Count; i++)
            {
                Destroy(blocks[cleared[i]]);
                blocks.Remove(cleared[i]);
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
                }

                playerViews.Add(view);
                wasAlive.Add(state.Players[i].Alive);
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
