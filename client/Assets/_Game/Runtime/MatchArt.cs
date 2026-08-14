using UnityEngine;

namespace Blastlands.Runtime
{
    // The art the view instantiates. Every field may be left empty: the view falls
    // back to primitives so the game still runs with no art in the project.
    //
    // Blocks and floor are arrays rather than single prefabs. A grid built from one
    // repeated mesh reads as wallpaper, and the eye stops parsing it.
    [CreateAssetMenu(fileName = "MatchArt", menuName = "Blastlands/Match Art")]
    public sealed class MatchArt : ScriptableObject
    {
        [SerializeField] private GameObject[] floorTiles;
        [SerializeField] private GameObject[] hardBlocks;
        [SerializeField] private GameObject[] softBlocks;
        [SerializeField] private GameObject[] scenery;

        // Flat patches laid across the floor without regard for tile boundaries. They
        // must never read as obstacles: anything with height on a walkable tile makes
        // the player misjudge where they can go.
        [SerializeField] private GameObject[] groundDetail;

        // Left where a player died. Blood scattered at random is noise; blood that
        // marks a death tells you what happened while you were looking elsewhere.
        [SerializeField] private GameObject[] deathMarkers;
        [SerializeField] private GameObject bomb;
        [SerializeField] private GameObject flame;
        [SerializeField] private GameObject explosionBurst;
        [SerializeField] private GameObject[] players;

        public GameObject Bomb
        {
            get { return bomb; }
        }

        public GameObject Flame
        {
            get { return flame; }
        }

        public GameObject ExplosionBurst
        {
            get { return explosionBurst; }
        }

        public bool HasScenery
        {
            get { return scenery != null && scenery.Length > 0; }
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

        public GameObject Scenery(int variant)
        {
            return Pick(scenery, variant);
        }

        public bool HasGroundDetail
        {
            get { return groundDetail != null && groundDetail.Length > 0; }
        }

        public GameObject GroundDetail(int variant)
        {
            return Pick(groundDetail, variant);
        }

        public GameObject DeathMarker(int variant)
        {
            return Pick(deathMarkers, variant);
        }

        public GameObject PlayerFor(int index)
        {
            return Pick(players, index);
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
