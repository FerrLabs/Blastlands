using System.Collections.Generic;
using Blastlands.Core;
using UnityEngine;

namespace Blastlands.Runtime
{
    public sealed class RegrowthTelegraphs
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private readonly ViewStage stage;
        private readonly Color color;
        private readonly Color flashColor;
        private readonly float pulseHz;
        private readonly float startSize;
        private readonly float lift;
        private readonly List<GameObject> pool = new List<GameObject>();
        private MaterialPropertyBlock block;
        private Vector3 fullScale = Vector3.one;

        public RegrowthTelegraphs(ViewStage stage, Color color, Color flashColor, float pulseHz, float startSize, float lift)
        {
            this.stage = stage;
            this.color = color;
            this.flashColor = flashColor;
            this.pulseHz = pulseHz;
            this.startSize = startSize;
            this.lift = lift;
        }

        // Shown for the last stretch of a wall's countdown and not before. Binary rather
        // than a creeping fill: the player only needs to learn one thing, that tape
        // means this tile is about to stop being one.
        public void Sync()
        {
            MatchState state = stage.State;
            GameObject prefab = stage.Art == null ? null : stage.Art.WallTelegraph;
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

                bool created = pool.Count <= shown;
                GameObject view = stage.TakeAt(pool, shown, prefab, PrimitiveType.Quad, MatchPalette.Flame, "Closing");

                if (created)
                {
                    TileFitter.FitInBox(view, 0.92f);
                    fullScale = view.transform.localScale;
                }

                float closing = 1f - ((float)wall.TicksRemaining / Mathf.Max(1, state.Settings.WallTelegraphTicks));
                view.transform.localScale = fullScale * Mathf.Lerp(startSize, 1f, closing);
                Pulse(view);

                TileFitter.PlaceAsGround(view, MatchView.ToWorld(wall.Tile, lift));
                shown++;
            }

            ViewStage.HideFrom(pool, shown);
        }

        // A property block rather than a material instance: the decal is Synty's, and
        // the warning colour is ours to put on top of it without editing the asset.
        private void Pulse(GameObject view)
        {
            block ??= new MaterialPropertyBlock();
            float pulse = 0.5f + (0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * pulseHz));
            block.SetColor(BaseColor, Color.Lerp(color, flashColor, pulse));

            foreach (Renderer renderer in view.GetComponentsInChildren<Renderer>(true))
            {
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
