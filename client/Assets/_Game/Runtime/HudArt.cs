using UnityEngine;

namespace Blastlands.Runtime
{
    // The Synty icon meshes the HUD reads its symbols from. Every field may be left
    // empty: the HUD draws its frames either way and simply shows nothing in the slot.
    [CreateAssetMenu(fileName = "HudArt", menuName = "Blastlands/Hud Art")]
    public sealed class HudArt : ScriptableObject
    {
        [SerializeField] private GameObject bombs;
        [SerializeField] private GameObject fire;
        [SerializeField] private GameObject speed;
        [SerializeField] private GameObject dead;

        // Indexed 0-9, in order.
        [SerializeField] private GameObject[] digits;

        public GameObject Bombs
        {
            get { return bombs; }
        }

        public GameObject Fire
        {
            get { return fire; }
        }

        public GameObject Speed
        {
            get { return speed; }
        }

        public GameObject Dead
        {
            get { return dead; }
        }

        public GameObject Digit(int value)
        {
            if (digits == null || value < 0 || value >= digits.Length)
            {
                return null;
            }

            return digits[value];
        }
    }
}
