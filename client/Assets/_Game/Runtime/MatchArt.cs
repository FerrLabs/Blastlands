using UnityEngine;

namespace Blastlands.Runtime
{
    // The art the view instantiates. Every field may be left empty: the view falls
    // back to primitives so the game still runs with no art in the project.
    [CreateAssetMenu(fileName = "MatchArt", menuName = "Blastlands/Match Art")]
    public sealed class MatchArt : ScriptableObject
    {
        [SerializeField] private GameObject floorTile;
        [SerializeField] private GameObject hardBlock;
        [SerializeField] private GameObject softBlock;
        [SerializeField] private GameObject bomb;
        [SerializeField] private GameObject flame;
        [SerializeField] private GameObject explosionBurst;
        [SerializeField] private GameObject[] players;

        public GameObject FloorTile
        {
            get { return floorTile; }
        }

        public GameObject HardBlock
        {
            get { return hardBlock; }
        }

        public GameObject SoftBlock
        {
            get { return softBlock; }
        }

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

        public GameObject PlayerFor(int index)
        {
            if (players == null || players.Length == 0)
            {
                return null;
            }

            return players[((index % players.Length) + players.Length) % players.Length];
        }
    }
}
