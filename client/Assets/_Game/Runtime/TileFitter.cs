using UnityEngine;

namespace Blastlands.Runtime
{
    // Synty prefabs come in their own world scale, with pivots that are often not
    // centred. Rather than hard-code a guessed factor per prefab, measure the renderer
    // bounds and normalise: it stays correct when the art is swapped.
    public static class TileFitter
    {
        // Scales so the whole prefab fits inside a cube of the given side. Constraining
        // every axis, not just the footprint, is what stops a tall prop from towering
        // over the arena and hiding the tiles behind it.
        public static void FitInBox(GameObject instance, float side)
        {
            Scale(instance, side, true);
        }

        // Scales by height instead, for characters: they should read as roughly one
        // tile tall regardless of how wide the model happens to be.
        public static void FitToHeight(GameObject instance, float height)
        {
            if (instance == null || !TryMeasure(instance, out Bounds bounds))
            {
                return;
            }

            if (bounds.size.y <= 0.0001f)
            {
                return;
            }

            instance.transform.localScale *= height / bounds.size.y;
        }

        // Fits the footprint to the tile, then stretches the height on its own. Bushes
        // need this: every piece of foliage in the packs is knee-high ground cover, and
        // one has to hide whoever walks into it. Scaling it up uniformly until it did
        // would spill it across the neighbouring tiles.
        public static void FitToTile(GameObject instance, float side, float height)
        {
            Scale(instance, side, false);

            if (instance == null || !TryMeasure(instance, out Bounds bounds) || bounds.size.y <= 0.0001f)
            {
                return;
            }

            Vector3 scale = instance.transform.localScale;
            scale.y *= height / bounds.size.y;
            instance.transform.localScale = scale;
        }

        // Places the prefab so it is centred on the tile horizontally and resting on
        // the ground, whatever its pivot happens to be.
        public static void PlaceOnTile(GameObject instance, Vector3 tileCentre)
        {
            if (instance == null)
            {
                return;
            }

            instance.transform.position = tileCentre;

            if (!TryMeasure(instance, out Bounds bounds))
            {
                return;
            }

            Vector3 correction = new Vector3(
                tileCentre.x - bounds.center.x,
                tileCentre.y - bounds.min.y,
                tileCentre.z - bounds.center.z);

            instance.transform.position += correction;
        }

        // Same centring, but the prefab's top face lands on the given height instead
        // of its base: that is what a ground tile needs so props rest on top of it.
        public static void PlaceAsGround(GameObject instance, Vector3 tileCentre)
        {
            if (instance == null)
            {
                return;
            }

            instance.transform.position = tileCentre;

            if (!TryMeasure(instance, out Bounds bounds))
            {
                return;
            }

            instance.transform.position += new Vector3(
                tileCentre.x - bounds.center.x,
                tileCentre.y - bounds.max.y,
                tileCentre.z - bounds.center.z);
        }

        private static void Scale(GameObject instance, float target, bool includeHeight)
        {
            if (instance == null || !TryMeasure(instance, out Bounds bounds))
            {
                return;
            }

            float largest = includeHeight
                ? Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z))
                : Mathf.Max(bounds.size.x, bounds.size.z);

            if (largest <= 0.0001f)
            {
                return;
            }

            instance.transform.localScale *= target / largest;
        }

        public static bool TryMeasure(GameObject instance, out Bounds bounds)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return true;
        }
    }
}
