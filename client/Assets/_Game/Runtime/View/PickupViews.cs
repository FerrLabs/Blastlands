using System.Collections.Generic;
using Blastlands.Core;
using UnityEngine;

namespace Blastlands.Runtime
{
    public sealed class PickupViews
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColor = Shader.PropertyToID("_Color");

        private readonly ViewStage stage;
        private readonly float powerUpSize;
        private readonly float looseBombSize;
        private readonly List<GameObject> powerUpViews = new List<GameObject>();
        private readonly List<PowerUpKind> powerUpKinds = new List<PowerUpKind>();
        private readonly List<GridPos> pickupTiles = new List<GridPos>();
        private readonly List<GameObject> looseBombPool = new List<GameObject>();

        public PickupViews(ViewStage stage, float powerUpSize, float looseBombSize)
        {
            this.stage = stage;
            this.powerUpSize = powerUpSize;
            this.looseBombSize = looseBombSize;
        }

        public void Sync()
        {
            SyncPowerUps();
            SyncLooseBombs();
        }

        // Pickups are pooled per kind rather than in one list: a pool entry keeps the
        // prefab it was built from, so a bomb-up view cannot end up standing in for a
        // fire-up when the list shifts.
        private void SyncPowerUps()
        {
            MatchState state = stage.State;
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
                view.transform.position = MatchView.ToWorld(pickup.Tile, 0.42f + bob);
                view.transform.rotation = Quaternion.Euler(0f, Time.time * 70f, 0f);
                view.SetActive(true);
            }

            ReportCollectedPickups();
        }

        // A pickup leaves the state either because someone walked onto it or because a
        // blast took it. The flame still burning on the tile is what tells them apart.
        private void ReportCollectedPickups()
        {
            MatchState state = stage.State;
            for (int i = 0; i < pickupTiles.Count; i++)
            {
                GridPos tile = pickupTiles[i];
                if (state.PowerUpIndexAt(tile) < 0 && !state.HasFlameAt(tile) && stage.Sfx != null)
                {
                    stage.Sfx.PickedUp(MatchView.ToWorld(tile, 0f));
                }
            }

            pickupTiles.Clear();
            for (int i = 0; i < state.PowerUps.Count; i++)
            {
                pickupTiles.Add(state.PowerUps[i].Tile);
            }
        }

        private static void Tint(GameObject view, PowerUpKind kind)
        {
            if (!MatchPalette.TryTintFor(kind, out Color tint))
            {
                return;
            }

            var block = new MaterialPropertyBlock();
            foreach (Renderer part in view.GetComponentsInChildren<Renderer>(true))
            {
                part.GetPropertyBlock(block);
                block.SetColor(BaseColor, tint);
                block.SetColor(LegacyColor, tint);
                part.SetPropertyBlock(block);
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

            GameObject prefab = stage.Art == null ? null : stage.Art.PowerUp(kind);
            GameObject created = stage.Spawn(prefab, PrimitiveType.Capsule, MatchPalette.ForPlayer((int)kind), "PowerUp " + kind);

            if (prefab == null)
            {
                created.transform.localScale = Vector3.one * 0.34f;
            }
            else
            {
                TileFitter.FitInBox(created, powerUpSize);
                Tint(created, kind);
            }

            powerUpViews.Add(created);
            powerUpKinds.Add(kind);
            return created;
        }

        // Deliberately the same prefab as a live bomb, sat flat on the floor and left
        // still. A pickup that looked like something else would have players learning
        // two shapes for one object; what separates them is that this one is not ticking.
        private void SyncLooseBombs()
        {
            MatchState state = stage.State;
            for (int i = 0; i < state.LooseBombs.Count; i++)
            {
                bool created = looseBombPool.Count <= i;
                GameObject view = stage.TakeAt(
                    looseBombPool, i, stage.Art != null ? stage.Art.Bomb : null, PrimitiveType.Sphere, MatchPalette.Bomb, "Loose bomb");

                if (created)
                {
                    TileFitter.FitInBox(view, looseBombSize);
                }

                TileFitter.PlaceOnTile(view, MatchView.ToWorld(state.LooseBombs[i], 0f));
            }

            ViewStage.HideFrom(looseBombPool, state.LooseBombs.Count);
        }
    }
}
