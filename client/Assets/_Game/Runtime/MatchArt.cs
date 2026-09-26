using Blastlands.Core;
using UnityEngine;

namespace Blastlands.Runtime
{
    // The art that means something rather than the art that decorates. Everything here
    // is identical whatever arena theme a match rolls: a fuse, a blast, a pickup and a
    // wall about to close have to read the same way every round, or the player is
    // relearning the rules each time the scenery changes.
    //
    // The terrain lives in ArenaTheme. Every field may be left empty: the view falls
    // back to primitives so the game still runs with no art in the project.
    [CreateAssetMenu(fileName = "MatchArt", menuName = "Blastlands/Match Art")]
    public sealed class MatchArt : ScriptableObject
    {
        // Left where a player died. Blood scattered at random is noise; blood that
        // marks a death tells you what happened while you were looking elsewhere.
        [SerializeField] private GameObject[] deathMarkers;

        // Laid on a tile a wall is about to close over. Flat, like everything else that
        // sits on walkable ground: the tile is still crossable while it is showing.
        [SerializeField] private GameObject wallTelegraph;

        // One per PowerUpKind, in enum order. They hover and spin in the view: a pickup
        // that sits still like a block gets read as a block.
        [SerializeField] private GameObject[] powerUps;
        [SerializeField] private GameObject bomb;
        [SerializeField] private GameObject flame;
        [SerializeField] private GameObject explosionBurst;
        [SerializeField] private GameObject[] players;
        [SerializeField] private GameObject[] zombies;

        // One per character, in roster order. A character looks the same in the lobby
        // and in the match, so this wins over the seat's model whenever one is known.
        [SerializeField] private GameObject[] characters;

        // Drives the character prefabs. The POLYGON prefabs ship an avatar but no
        // controller, where the SIMPLE ones carried one, so without this every player
        // stands in the T-pose the rig was authored in. Held here rather than assigned
        // onto the vendored prefabs, which stay untouched so the next pack update does
        // not quietly revert it.
        [SerializeField] private RuntimeAnimatorController playerAnimator;

        // The same for the zombies, which are POLYGON characters too and come without a
        // controller. A shambling walk of their own rather than the players' run, so a
        // zombie never moves like somebody you could be playing against.
        [SerializeField] private RuntimeAnimatorController zombieAnimator;

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

        public GameObject DeathMarker(int variant)
        {
            return Pick(deathMarkers, variant);
        }

        public GameObject WallTelegraph
        {
            get { return wallTelegraph; }
        }

        public GameObject PowerUp(PowerUpKind kind)
        {
            return Pick(powerUps, (int)kind);
        }

        public GameObject Zombie(int variant)
        {
            return Pick(zombies, variant);
        }

        public GameObject PlayerFor(int index)
        {
            return Pick(players, index);
        }

        public GameObject ForCharacter(CharacterKind character)
        {
            int at = CharacterKits.IndexOf(character);
            return at < 0 || characters == null || at >= characters.Length ? null : characters[at];
        }

        public GameObject PlayerFor(int seat, CharacterKind character)
        {
            GameObject model = ForCharacter(character);
            return model != null ? model : PlayerFor(seat);
        }

        public RuntimeAnimatorController PlayerAnimator
        {
            get { return playerAnimator; }
        }

        public RuntimeAnimatorController ZombieAnimator
        {
            get { return zombieAnimator; }
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
