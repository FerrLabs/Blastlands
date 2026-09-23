using Blastlands.Core;
using UnityEngine;

namespace Blastlands.Runtime
{
    [CreateAssetMenu(fileName = "HudArt", menuName = "Blastlands/Hud Art")]
    public sealed class HudArt : ScriptableObject
    {
        [SerializeField] private GameObject clock;
        [SerializeField] private GameObject banner;
        [SerializeField] private GameObject badge;
        [SerializeField] private GameObject row;
        [SerializeField] private GameObject statBox;
        [SerializeField] private GameObject icon;
        [SerializeField] private GameObject label;
        [SerializeField] private GameObject caption;
        [SerializeField] private GameObject dial;
        [SerializeField] private GameObject padPrompt;
        [SerializeField] private GameObject keyPrompt;
        [SerializeField] private GameObject roundWon;

        [SerializeField] private Sprite bombs;
        [SerializeField] private Sprite fire;
        [SerializeField] private Sprite speed;
        [SerializeField] private Sprite dead;
        [SerializeField] private Sprite shove;
        [SerializeField] private Sprite mouseLeft;
        [SerializeField] private Sprite mouseRight;
        [SerializeField] private Sprite xboxSouth;
        [SerializeField] private Sprite xboxWest;
        [SerializeField] private Sprite xboxShoulder;
        [SerializeField] private Sprite xboxNorth;
        [SerializeField] private Sprite playStationSouth;
        [SerializeField] private Sprite playStationWest;
        [SerializeField] private Sprite playStationShoulder;
        [SerializeField] private Sprite playStationNorth;
        [SerializeField] private Sprite triggerAbility;
        [SerializeField] private Sprite vanishAbility;
        [SerializeField] private Sprite throwAbility;
        [SerializeField] private Sprite wallAbility;

        public GameObject Clock
        {
            get { return clock; }
        }

        public GameObject Banner
        {
            get { return banner; }
        }

        public GameObject Badge
        {
            get { return badge; }
        }

        public GameObject Row
        {
            get { return row; }
        }

        public GameObject StatBox
        {
            get { return statBox; }
        }

        public GameObject Icon
        {
            get { return icon; }
        }

        public GameObject Label
        {
            get { return label; }
        }

        public GameObject Caption
        {
            get { return caption; }
        }

        public GameObject Dial
        {
            get { return dial; }
        }

        public GameObject PadPrompt
        {
            get { return padPrompt; }
        }

        public GameObject KeyPrompt
        {
            get { return keyPrompt; }
        }

        public GameObject RoundWon
        {
            get { return roundWon; }
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

        public Sprite Shove
        {
            get { return shove; }
        }

        public Sprite Mouse(PromptGlyph glyph)
        {
            return glyph == PromptGlyph.MouseRight ? mouseRight : mouseLeft;
        }

        public Sprite AbilityOf(CharacterKind character)
        {
            switch (character)
            {
                case CharacterKind.Demolisher:
                    return triggerAbility;
                case CharacterKind.Runner:
                    return vanishAbility;
                case CharacterKind.Grenadier:
                    return throwAbility;
                case CharacterKind.Sapper:
                    return wallAbility;
                default:
                    return null;
            }
        }

        public Sprite PadGlyph(InputDeviceKind device, PromptGlyph glyph)
        {
            bool playStation = device == InputDeviceKind.PlayStation;

            switch (glyph)
            {
                case PromptGlyph.PadWest:
                    return playStation ? playStationWest : xboxWest;
                case PromptGlyph.PadShoulder:
                    return playStation ? playStationShoulder : xboxShoulder;
                case PromptGlyph.PadNorth:
                    return playStation ? playStationNorth : xboxNorth;
                default:
                    return playStation ? playStationSouth : xboxSouth;
            }
        }
    }
}
