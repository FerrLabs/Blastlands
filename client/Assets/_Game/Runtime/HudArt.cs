using UnityEngine;

namespace Blastlands.Runtime
{
    // The Synty interface art the HUD is assembled from. Every field may be left empty:
    // the HUD still lays its panels out and simply shows nothing in the slot.
    [CreateAssetMenu(fileName = "HudArt", menuName = "Blastlands/Hud Art")]
    public sealed class HudArt : ScriptableObject
    {
        // One framed stat from the Apocalypse HUD pack: plate, icon and fill in a single
        // prefab. Three of them are the whole panel, which is why there is no plate here
        // any more. The old panel was a bare sprite stretched over a hand-built Image and
        // then tinted, because the pack's metal plate left white icons with no contrast
        // on it. Recolouring an asset to make it work is the tell that it was the wrong
        // asset.
        [SerializeField] private GameObject statBox;

        // The pack's own icon component, and the indicator light it uses to mark state.
        // Nothing here is assembled by hand: the HUD instantiates these and pushes
        // values into them.
        [SerializeField] private GameObject icon;
        [SerializeField] private GameObject diode;

        [SerializeField] private Sprite bombs;
        [SerializeField] private Sprite fire;
        [SerializeField] private Sprite speed;
        [SerializeField] private Sprite dead;

        public GameObject StatBox
        {
            get { return statBox; }
        }

        public GameObject Icon
        {
            get { return icon; }
        }

        public GameObject Diode
        {
            get { return diode; }
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
