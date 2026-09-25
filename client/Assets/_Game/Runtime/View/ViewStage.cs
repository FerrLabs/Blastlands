using System.Collections.Generic;
using Blastlands.Core;
using Blastlands.Core.Net;
using UnityEngine;

namespace Blastlands.Runtime
{
    public sealed class ViewStage
    {
        public ViewStage(Transform root, MatchState state, MatchArt art, ArenaTheme theme, MatchAudio sfx, MatchCamera cameras)
        {
            Root = root;
            State = state;
            Art = art;
            Theme = theme;
            Sfx = sfx;
            Cameras = cameras;
        }

        public Transform Root { get; }

        public MatchState State { get; }

        public MatchArt Art { get; }

        public ArenaTheme Theme { get; }

        public MatchAudio Sfx { get; }

        public MatchCamera Cameras { get; }

        public InterpolationClock Clock { get; set; }

        public int TicksPast
        {
            get { return Clock == null || !Clock.Started ? 0 : Clock.TicksPast(State.Tick); }
        }

        public int Variant(GridPos tile, int salt)
        {
            return TileHash.At(tile.X + (salt * 977), tile.Y - (salt * 389), State.Seed);
        }

        public GameObject Spawn(GameObject prefab, PrimitiveType fallbackShape, Color fallbackColor, string label)
        {
            GameObject instance;

            if (prefab != null)
            {
                instance = Object.Instantiate(prefab, Root);
            }
            else
            {
                instance = GameObject.CreatePrimitive(fallbackShape);
                instance.transform.SetParent(Root, false);
                Collider collider = instance.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.Destroy(collider);
                }

                instance.GetComponent<Renderer>().sharedMaterial = MatchPalette.CreateMaterial(fallbackColor);
            }

            instance.name = label;
            return instance;
        }

        public GameObject TakeAt(
            List<GameObject> pool, int index, GameObject prefab, PrimitiveType fallbackShape, Color fallbackColor, string label)
        {
            while (pool.Count <= index)
            {
                pool.Add(Spawn(prefab, fallbackShape, fallbackColor, label + " " + pool.Count));
            }

            pool[index].SetActive(true);
            return pool[index];
        }

        public static void HideFrom(List<GameObject> pool, int from)
        {
            for (int i = from; i < pool.Count; i++)
            {
                pool[i].SetActive(false);
            }
        }

        public GridPos NearestToTheEar(List<GridPos> tiles)
        {
            if (tiles.Count == 1 || Cameras == null
                || !Cameras.SingleViewportIsListening(out Vector3 ear, out float _))
            {
                return tiles[0];
            }

            int best = 0;
            float shortest = float.MaxValue;

            for (int i = 0; i < tiles.Count; i++)
            {
                Vector3 at = MatchView.ToWorld(tiles[i], 0f);
                float sideways = at.x - ear.x;
                float depth = at.z - ear.z;
                float distance = (sideways * sideways) + (depth * depth);

                if (distance < shortest)
                {
                    shortest = distance;
                    best = i;
                }
            }

            return tiles[best];
        }

        public static float FacingAngle(Direction facing)
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
    }
}
