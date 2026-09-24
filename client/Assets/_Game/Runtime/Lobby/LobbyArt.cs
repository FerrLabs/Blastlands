using UnityEngine;

namespace Blastlands.Runtime
{
    [CreateAssetMenu(fileName = "LobbyArt", menuName = "Blastlands/Lobby Art")]
    public sealed class LobbyArt : ScriptableObject
    {
        [SerializeField] private GameObject header;
        [SerializeField] private GameObject body;

        [SerializeField] private Sprite panel;
        [SerializeField] private Sprite row;
        [SerializeField] private Sprite field;
        [SerializeField] private MatchArt models;
        [SerializeField] private ArenaTheme[] themes;

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

        public MatchArt Models
        {
            get { return models; }
        }

        public ArenaTheme ThemeFor(uint seed)
        {
            return themes == null || themes.Length == 0 ? null : themes[(int)(seed % (uint)themes.Length)];
        }

        public bool Complete
        {
            get
            {
                return header != null && body != null
                    && panel != null && row != null && field != null && models != null;
            }
        }
    }
}
