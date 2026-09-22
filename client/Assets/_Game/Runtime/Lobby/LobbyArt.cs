using UnityEngine;

namespace Blastlands.Runtime
{
    [CreateAssetMenu(fileName = "LobbyArt", menuName = "Blastlands/Lobby Art")]
    public sealed class LobbyArt : ScriptableObject
    {
        [SerializeField] private GameObject button;
        [SerializeField] private GameObject header;
        [SerializeField] private GameObject body;

        [SerializeField] private Sprite panel;
        [SerializeField] private Sprite row;
        [SerializeField] private Sprite field;

        public GameObject Button
        {
            get { return button; }
        }

        public GameObject Header
        {
            get { return header; }
        }

        public GameObject Body
        {
            get { return body; }
        }

        public Sprite Panel
        {
            get { return panel; }
        }

        public Sprite Row
        {
            get { return row; }
        }

        public Sprite Field
        {
            get { return field; }
        }

        public bool Complete
        {
            get
            {
                return button != null && header != null && body != null
                    && panel != null && row != null && field != null;
            }
        }
    }
}
