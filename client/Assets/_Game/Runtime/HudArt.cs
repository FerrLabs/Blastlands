using UnityEngine;

namespace Blastlands.Runtime
{
    // The Synty interface art the HUD is assembled from. Every field may be left empty:
    // the HUD still lays its panels out and simply shows nothing in the slot.
    [CreateAssetMenu(fileName = "HudArt", menuName = "Blastlands/Hud Art")]
    public sealed class HudArt : ScriptableObject
    {
        // A row of icon-and-bar entries from the Apocalypse HUD pack. The panel drives
        // the first three rows and hides the rest.
        [SerializeField] private GameObject statsList;

        // The pack's own icon component, and the indicator light it uses to mark state.
        // Nothing here is assembled by hand: the HUD instantiates these and pushes
        // values into them.
        [SerializeField] private GameObject icon;
        [SerializeField] private GameObject diode;

        [SerializeField] private Sprite panel;
        [SerializeField] private Sprite bombs;
        [SerializeField] private Sprite fire;
        [SerializeField] private Sprite speed;
        [SerializeField] private Sprite dead;

        public GameObject StatsList
        {
            get { return statsList; }
        }

        public GameObject Icon
        {
            get { return icon; }
        }

        public GameObject Diode
        {
            get { return diode; }
        }

        public Sprite Panel
        {
            get { return panel; }
        }

        public Sprite Bombs
        {
            get { return bombs; }
        }

        public Sprite Fire
        {
            get { return fire; }
        }

        public Sprite Speed
        {
            get { return speed; }
        }

        public Sprite Dead
        {
            get { return dead; }
        }
    }
}
