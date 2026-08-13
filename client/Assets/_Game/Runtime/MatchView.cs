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
        [SerializeField] private float playerFootprint = 0.62f;
        [SerializeField] private float playerHeight = 1.1f;

        // Particle prefabs carry no useful renderer bounds, so they cannot be measured
        // like meshes: the Synty fire is built for a campfire and needs scaling down
        // by hand to sit inside one tile.
        [SerializeField] private float flameScale = 0.9f;
        [SerializeField] private float burstScale = 0.4f;

        private readonly Dictionary<GridPos, GameObject> blocks = new Dictionary<GridPos, GameObject>();
        private readonly List<GameObject> bombPool = new List<GameObject>();
        private readonly List<Vector3> bombBaseScales = new List<Vector3>();
        private readonly List<GameObject> flamePool = new List<GameObject>();
        private readonly List<GameObject> playerViews = new List<GameObject>();
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
            bombBaseScales.Clear();
            flamePool.Clear();
            playerViews.Clear();
            burningTiles.Clear();

            BuildFloor();
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
            SyncBombs();
            SyncFlames();
            SyncPlayers();
        }

        private void BuildFloor()
        {
            for (int y = 0; y < state.Arena.Height; y++)
            {
                for (int x = 0; x < state.Arena.Width; x++)
                {
                    GameObject tile = Spawn(art != null ? art.FloorTile : null, PrimitiveType.Cube, MatchPalette.Floor, "Floor");

                    if (art == null || art.FloorTile == null)
                    {
                        tile.transform.localScale = new Vector3(1f, 0.1f, 1f);
                        tile.transform.position = ToWorld(new GridPos(x, y), -0.05f);
                    }
                    else
                    {
                        TileFitter.FitInBox(tile, 1f);
                        TileFitter.PlaceAsGround(tile, ToWorld(new GridPos(x, y), 0f));
                    }
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
            GameObject prefab = art == null ? null : (hard ? art.HardBlock : art.SoftBlock);
            Color fallback = hard ? MatchPalette.HardBlock : MatchPalette.SoftBlock;

            GameObject block = Spawn(prefab, PrimitiveType.Cube, fallback, kind.ToString());

            if (prefab == null)
            {
                block.transform.localScale = new Vector3(blockFootprint, 1f, blockFootprint);
                block.transform.position = ToWorld(tile, 0.5f);
                return block;
            }

            block.transform.rotation = Quaternion.Euler(0f, QuarterTurn(tile), 0f);
            TileFitter.FitInBox(block, hard ? 1f : blockFootprint);
            TileFitter.PlaceOnTile(block, ToWorld(tile, 0f));

            return block;
        }

        // Rocks and crates repeated across a grid read as wallpaper; a deterministic
        // quarter turn per tile breaks the pattern without touching the simulation.
        private static float QuarterTurn(GridPos tile)
        {
            int index = (((tile.X * 7) + (tile.Y * 13)) % 4 + 4) % 4;
            return index * 90f;
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

        private void SyncBombs()
        {
            for (int i = 0; i < state.Bombs.Count; i++)
            {
                bool created = bombPool.Count <= i;
                GameObject view = TakeAt(bombPool, i, art != null ? art.Bomb : null, PrimitiveType.Sphere, MatchPalette.Bomb, "Bomb");

                // FitToTile multiplies the current scale, so it runs once per instance
                // and the fuse pulse is applied on top of the scale it settled on.
                if (created)
                {
                    TileFitter.FitInBox(view, 0.78f);
                    bombBaseScales.Add(view.transform.localScale);
                }

                ActiveBomb bomb = state.Bombs[i];
                float fuse = bomb.FuseRemaining / (float)state.Settings.FuseTicks;
                float pulse = 1f + (0.14f * Mathf.Sin((1f - fuse) * 34f));

                view.transform.localScale = bombBaseScales[i] * pulse;
                TileFitter.PlaceOnTile(view, ToWorld(bomb.Position, 0f));
            }

            HideFrom(bombPool, state.Bombs.Count);
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
                    view.transform.localScale = hasArt
                        ? Vector3.one * flameScale
                        : new Vector3(0.94f, 0.5f, 0.94f);

                    if (hasArt)
                    {
                        TuneFlameParticles(view);
                    }
                }

                ActiveFlame flame = state.Flames[i];
                view.transform.position = ToWorld(flame.Tile, hasArt ? 0f : 0.25f);
            }

            HideFrom(flamePool, state.Flames.Count);
            EmitBursts();
        }

        // The Synty fire is authored as a campfire: it simulates in world space, ignores
        // transform scale for particle size, and lives four seconds. Pooled flame objects
        // move between tiles, so world-space particles smear across the arena and the long
        // lifetime piles them into one plume. Retune the instance, never the source asset.
        private void TuneFlameParticles(GameObject instance)
        {
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.MainModule main = systems[i].main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.startLifetime = 0.45f;
            }
        }

        // One burst per tile that has just caught fire, so a blast reads as an event
        // rather than as flames quietly appearing.
        private void EmitBursts()
        {
            GameObject prefab = art == null ? null : art.ExplosionBurst;

            for (int i = 0; i < state.Flames.Count; i++)
            {
                GridPos tile = state.Flames[i].Tile;
                if (burningTiles.Contains(tile))
                {
                    continue;
                }

                if (prefab != null)
                {
                    GameObject burst = Instantiate(prefab, ToWorld(tile, 0.1f), Quaternion.identity, root);
                    burst.transform.localScale = Vector3.one * burstScale;
                    Destroy(burst, 2.5f);
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
                    view.transform.localScale = new Vector3(playerFootprint, 0.42f, playerFootprint);
                }
                else
                {
                    TileFitter.FitToHeight(view, playerHeight);
                }

                playerViews.Add(view);
            }
        }

        private void SyncPlayers()
        {
            for (int i = 0; i < playerViews.Count && i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                GameObject view = playerViews[i];

                if (!player.Alive)
                {
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
