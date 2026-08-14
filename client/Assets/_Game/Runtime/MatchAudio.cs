using UnityEngine;

namespace Blastlands.Runtime
{
    // Every clip may be left empty, in which case a synthesised placeholder stands in.
    // The view calls these methods; it never touches an AudioSource itself.
    [RequireComponent(typeof(AudioSource))]
    public sealed class MatchAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip bombDrop;
        [SerializeField] private AudioClip explosion;
        [SerializeField] private AudioClip pickup;
        [SerializeField] private AudioClip death;

        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

        private AudioSource source;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            if (bombDrop == null)
            {
                bombDrop = SfxSynth.BombDrop();
            }

            if (explosion == null)
            {
                explosion = SfxSynth.Explosion();
            }

            if (pickup == null)
            {
                pickup = SfxSynth.Pickup();
            }

            if (death == null)
            {
                death = SfxSynth.Death();
            }
        }

        public void BombDropped()
        {
            Play(bombDrop, 0.7f);
        }

        // Called once per tick that produced fire, not once per burning tile: a chain
        // across twenty tiles is one event to the ear, and twenty overlapping copies
        // of the same clip clip the mix and hurt.
        public void Exploded(int flameTiles)
        {
            Play(explosion, Mathf.Lerp(0.75f, 1f, Mathf.Clamp01(flameTiles / 18f)));
        }

        public void PickedUp()
        {
            Play(pickup, 0.85f);
        }

        public void Died()
        {
            Play(death, 1f);
        }

        private void Play(AudioClip clip, float scale)
        {
            if (clip != null && source != null)
            {
                source.PlayOneShot(clip, volume * scale);
            }
        }
    }
}
