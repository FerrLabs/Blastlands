using System;
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
        [SerializeField] private Color zombieFallbackColor = new Color(0.36f, 0.52f, 0.30f);
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

        private ViewStage stage;
        private BlockViews blocks;
        private RegrowthTelegraphs telegraphs;
        private PickupViews pickups;
        private BlastViews blasts;
        private PlayerViews players;
        private ZombieViews zombies;
        private PlayerTrail trail;
        private InterpolationClock clock;
        private int ownSeat = -1;
        private Func<SubPos> predictedOwn;
        private int ticksThisFrame;
        private float tickFraction = 1f;
        private bool blending;
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

        // Set before Bind. Changing it later would leave an arena half built out of
        // two themes, which is worse than either of them.
        public void UseTheme(ArenaTheme next)
        {
            theme = next;
        }

        public MatchArt Art
        {
            get { return art; }
        }

        public void Dress(MatchArt matchArt)
        {
            art = matchArt;
        }

        public void Bind(MatchState matchState)
        {
            if (sfx != null)
            {
                sfx.ListenThrough(cameras);
            }

            if (root != null)
            {
                Destroy(root.gameObject);
            }

            var holder = new GameObject("MatchView");
            root = holder.transform;
            root.SetParent(transform, false);

            stage = new ViewStage(root, matchState, art, theme, sfx, cameras) { Clock = clock };
            blocks = new BlockViews(stage, blockFootprint, wallHeight, bushHeight, wallThickness);
            telegraphs = new RegrowthTelegraphs(
                stage, telegraphColor, telegraphFlashColor, telegraphPulseHz, telegraphStartSize, groundDetailLift * 2f);
            pickups = new PickupViews(stage, powerUpSize, bombFootprint * 0.72f);
            blasts = new BlastViews(stage, bombFootprint, flameScale, burstScale, flameLifetime, burstLifetime);
            players = new PlayerViews(stage, playerHeight, ringRadius, ringWidth, groundDetailLift, runClipSpeed);
            players.Follow(trail, clock, ownSeat, predictedOwn);
            zombies = new ZombieViews(stage, playerHeight, zombieFallbackColor);

            ArenaDressing.BuildGround(stage, islandThickness);
            ArenaDressing.BuildCoast(stage, cliffSpread, cliffSink);
            ArenaDressing.BuildGroundDetail(stage, groundDetailPercent, groundDetailMinSize, groundDetailMaxSize, groundDetailLift);
            ArenaDressing.BuildScenery(stage, sceneryRing, sceneryDensityPercent, sceneryMinSize, sceneryMaxSize, skyDepth);
            blocks.Sync();
            players.Build();
        }

        public void Blend(int ticksAdvanced, float fraction)
        {
            blending = true;
            ticksThisFrame += ticksAdvanced;
            tickFraction = fraction;
        }

        public bool TryShown(int seat, out Vector3 position)
        {
            if (players == null)
            {
                position = default;
                return false;
            }

            return players.TryShown(seat, out position);
        }

        public void Interpolate(PlayerTrail playerTrail, InterpolationClock serverClock, int seat, Func<SubPos> ownPosition)
        {
            trail = playerTrail;
            clock = serverClock;
            ownSeat = seat;
            predictedOwn = ownPosition;

            if (stage != null)
            {
                stage.Clock = clock;
                players.Follow(trail, clock, ownSeat, predictedOwn);
            }
        }

        public void Render()
        {
            if (stage == null)
            {
                return;
            }

            blocks.Sync();
            telegraphs.Sync();
            pickups.Sync();
            blasts.Sync();
            players.Sync(blending, ticksThisFrame, tickFraction);
            ticksThisFrame = 0;
            zombies.Sync();
        }

        public int PlayerViewCount
        {
            get { return players == null ? 0 : players.Count; }
        }

        public GameObject PlayerViewAt(int index)
        {
            return players == null ? null : players.At(index);
        }
    }
}
