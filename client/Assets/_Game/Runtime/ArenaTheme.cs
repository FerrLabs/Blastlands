using UnityEngine;

namespace Blastlands.Runtime
{
    // The terrain a match is dressed in, swapped per match. Every field may be left
    // empty: the view falls back to primitives so the game still runs with no art.
    //
    // What is here and what stayed in MatchArt is the whole point of the split. A theme
    // owns the ground, the blocks and the scenery. It owns nothing that kills you:
    // bombs, flames, the wall telegraph and the pickups look identical in every theme,
    // because a player who has learnt to read a fuse in the desert must not have to
    // learn it again in the forest. Decoration varies; the grammar does not.
    //
    // Arrays rather than single prefabs throughout. A grid built from one repeated mesh
    // reads as wallpaper and the eye stops parsing it.
    [CreateAssetMenu(fileName = "ArenaTheme", menuName = "Blastlands/Arena Theme")]
    public sealed class ArenaTheme : ScriptableObject
    {
        [SerializeField] private string label = "Untitled";
        [SerializeField] private GameObject[] floorTiles;
        [SerializeField] private GameObject[] hardBlocks;
        [SerializeField] private GameObject[] softBlocks;
        [SerializeField] private GameObject[] bushes;
        [SerializeField] private GameObject[] scenery;

        // Flat patches laid across the floor without regard for tile boundaries. They
        // must never read as obstacles: anything with height on a walkable tile makes
        // the player misjudge where they can go.
        [SerializeField] private GameObject[] groundDetail;

        public string Label
        {
            get { return label; }
        }

        public bool HasScenery
        {
            get { return scenery != null && scenery.Length > 0; }
        }

        public bool HasGroundDetail
        {
            get { return groundDetail != null && groundDetail.Length > 0; }
        }

        public GameObject FloorTile(int variant)
        {
            return Pick(floorTiles, variant);
        }

        public GameObject HardBlock(int variant)
        {
            return Pick(hardBlocks, variant);
        }

        public GameObject SoftBlock(int variant)
        {
            return Pick(softBlocks, variant);
        }

        public GameObject Bush(int variant)
        {
            return Pick(bushes, variant);
        }

        public GameObject Scenery(int variant)
        {
            return Pick(scenery, variant);
        }

        public GameObject GroundDetail(int variant)
        {
            return Pick(groundDetail, variant);
        }

        private static GameObject Pick(GameObject[] set, int variant)
        {
            if (set == null || set.Length == 0)
            {
                return null;
            }

            return set[((variant % set.Length) + set.Length) % set.Length];
        }
    }
}
