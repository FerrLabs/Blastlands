using System.Collections.Generic;
using Blastlands.Core;
using Blastlands.Core.Net;
using UnityEngine;

namespace Blastlands.Runtime
{
    public sealed class BlastViews
    {
        private readonly ViewStage stage;
        private readonly float bombFootprint;
        private readonly float flameScale;
        private readonly float burstScale;
        private readonly float flameLifetime;
        private readonly float burstLifetime;
        private readonly List<GameObject> bombPool = new List<GameObject>();
        private readonly List<Vector3> bombBaseScales = new List<Vector3>();
        private readonly List<GameObject> flamePool = new List<GameObject>();
        private readonly Dictionary<int, GridPos> bombTiles = new Dictionary<int, GridPos>();
        private readonly HashSet<int> warnedFuses = new HashSet<int>();
        private readonly Dictionary<int, Vector3> shownBombs = new Dictionary<int, Vector3>();
        private readonly List<int> staleFuses = new List<int>();
        private readonly List<GridPos> detonated = new List<GridPos>();
        private readonly List<GridPos> freshFlames = new List<GridPos>();
        private readonly HashSet<GridPos> burningTiles = new HashSet<GridPos>();

        public BlastViews(ViewStage stage, float bombFootprint, float flameScale, float burstScale, float flameLifetime, float burstLifetime)
        {
            this.stage = stage;
            this.bombFootprint = bombFootprint;
            this.flameScale = flameScale;
            this.burstScale = burstScale;
            this.flameLifetime = flameLifetime;
            this.burstLifetime = burstLifetime;
        }

        public void Sync()
        {
            SyncBombs();
            SyncFlames();
        }

        private void SyncBombs()
        {
            MatchState state = stage.State;
            for (int i = 0; i < state.Bombs.Count; i++)
            {
                bool created = bombPool.Count <= i;
                GameObject view = stage.TakeAt(bombPool, i, stage.Art != null ? stage.Art.Bomb : null, PrimitiveType.Sphere, MatchPalette.Bomb, "Bomb");

                // FitInBox multiplies the current scale, so it runs once per instance
                // and the fuse pulse is applied on top of the scale it settled on.
                if (created)
                {
                    TileFitter.FitInBox(view, bombFootprint);
                    bombBaseScales.Add(view.transform.localScale);
                }

                ActiveBomb bomb = state.Bombs[i];
                float fuse = SnapshotAge.FuseAfter(bomb, stage.TicksPast) / (float)bomb.FuseTicks;
                float pulse = 1f + (0.14f * Mathf.Sin((1f - fuse) * 34f));

                view.transform.localScale = bombBaseScales[i] * pulse;
                TileFitter.PlaceOnTile(view, Slid(bomb));
            }

            ViewStage.HideFrom(bombPool, state.Bombs.Count);
            ReportNewBombs();
        }

        // Counting bombs would miss the tick where one detonates and another is dropped,
        // so the tiles are diffed instead. A bomb tile that is gone and now alight is a
        // bomb that went off, which is where the blast is staged from.
        private Vector3 Slid(ActiveBomb bomb)
        {
            Vector3 target = MatchView.ToWorld(bomb.Position, 0f);
            if (!shownBombs.TryGetValue(bomb.Id, out Vector3 shown))
            {
                shownBombs[bomb.Id] = target;
                return target;
            }

            float tile = (MatchView.ToWorld(new GridPos(1, 0), 0f) - MatchView.ToWorld(new GridPos(0, 0), 0f)).magnitude;
            float perSecond = tile * stage.State.Settings.TicksPerSecond / ClassicItems.SlideTicksPerTile;
            shown = Vector3.MoveTowards(shown, target, perSecond * Time.deltaTime);
            shownBombs[bomb.Id] = shown;
            return shown;
        }

        private void ReportNewBombs()
        {
            MatchState state = stage.State;
            MatchAudio sfx = stage.Sfx;
            for (int i = 0; i < state.Bombs.Count; i++)
            {
                ActiveBomb bomb = state.Bombs[i];
                if (sfx == null)
                {
                    continue;
                }

                if (!bombTiles.ContainsKey(bomb.Id))
                {
                    sfx.BombDropped(MatchView.ToWorld(bomb.Bomb.Position, 0f));
                }
                else if (BombFuse.IsWarning(SnapshotAge.FuseAfter(bomb, stage.TicksPast), bomb.FuseTicks, state.Settings.TicksPerSecond)
                         && warnedFuses.Add(bomb.Id))
                {
                    // Once per bomb rather than once per frame, which is what the set is
                    // for. The warning is the moment it enters its last second, and a
                    // clip restarted sixty times over that second is a buzz.
                    sfx.FuseBurningDown(MatchView.ToWorld(bomb.Bomb.Position, 0f));
                }
            }

            detonated.Clear();
            foreach (GridPos tile in bombTiles.Values)
            {
                if (!state.HasBombAt(tile) && state.HasFlameAt(tile))
                {
                    detonated.Add(tile);
                }
            }

            bombTiles.Clear();
            for (int i = 0; i < state.Bombs.Count; i++)
            {
                bombTiles[state.Bombs[i].Id] = state.Bombs[i].Bomb.Position;
            }

            // Collected first and removed after, because a set cannot be written to
            // while it is being read. Spelled out rather than handed to RemoveWhere: the
            // predicate would capture this and allocate a delegate on every frame, and
            // nothing else on this path allocates.
            staleFuses.Clear();
            foreach (int id in warnedFuses)
            {
                if (!bombTiles.ContainsKey(id))
                {
                    staleFuses.Add(id);
                }
            }

            foreach (int id in shownBombs.Keys)
            {
                if (!bombTiles.ContainsKey(id))
                {
                    staleFuses.Add(id);
                }
            }

            for (int i = 0; i < staleFuses.Count; i++)
            {
                warnedFuses.Remove(staleFuses[i]);
                shownBombs.Remove(staleFuses[i]);
            }
        }

        private void SyncFlames()
        {
            MatchState state = stage.State;
            bool hasArt = stage.Art != null && stage.Art.Flame != null;

            int ticksPast = stage.TicksPast;
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
                GameObject view = stage.TakeAt(flamePool, i, stage.Art != null ? stage.Art.Flame : null, PrimitiveType.Cube, MatchPalette.Flame, "Flame");

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

                view.transform.position = MatchView.ToWorld(flame.Tile, hasArt ? 0f : 0.25f);
            }

            ViewStage.HideFrom(flamePool, shown);
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

        private static float Expiry(GameObject instance, float lifetime)
        {
            float emitting = 0f;
            foreach (ParticleSystem system in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                emitting = Mathf.Max(emitting, system.main.duration);
            }

            return emitting + lifetime;
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
            MatchState state = stage.State;
            GameObject prefab = stage.Art == null ? null : stage.Art.ExplosionBurst;

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
                    GameObject burst = Object.Instantiate(prefab, MatchView.ToWorld(detonated[i], 0.05f), Quaternion.identity, stage.Root);
                    burst.transform.localScale = Vector3.one * burstScale;
                    TuneParticles(burst, burstLifetime);
                    Object.Destroy(burst, Expiry(burst, burstLifetime));
                }
            }

            if (caught > 0 && stage.Sfx != null)
            {
                stage.Sfx.Exploded(caught, MatchView.ToWorld(detonated.Count > 0 ? stage.NearestToTheEar(detonated) : freshFlames[0], 0f));
            }

            if (caught > 0 && stage.Cameras != null)
            {
                for (int i = 0; i < detonated.Count; i++)
                {
                    stage.Cameras.Felt(MatchView.ToWorld(detonated[i], 0f), FlamesOf(i));
                }
            }

            burningTiles.Clear();
            for (int i = 0; i < state.Flames.Count; i++)
            {
                burningTiles.Add(state.Flames[i].Tile);
            }
        }
    }
}
