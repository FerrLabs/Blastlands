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
        [SerializeField] private AudioClip blockBreak;
        [SerializeField] private AudioClip fuse;

        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

        [SerializeField, Range(2, 16)] private int voices = 8;

        private AudioSource[] sources;
        private int nextVoice;
        private MatchCamera cameras;

        private void Awake()
        {
#if UNITY_SERVER
            // A server build has no audio device, so synthesising these clips ends in a
            // `AudioClip.SetData failed` line each and nothing to play them through.
            // #13 says the server skips audio entirely; without this it only skipped
            // hearing it.
            enabled = false;
            return;
#else
            sources = new AudioSource[Mathf.Max(voices, 1)];
            sources[0] = GetComponent<AudioSource>();

            for (int i = 1; i < sources.Length; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
            }

            for (int i = 0; i < sources.Length; i++)
            {
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
            }

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

            if (blockBreak == null)
            {
                blockBreak = SfxSynth.BlockBreak();
            }

            if (fuse == null)
            {
                fuse = SfxSynth.Fuse();
            }
#endif
        }

        public void ListenThrough(MatchCamera through)
        {
            cameras = through;
        }

        public void BombDropped(Vector3 at)
        {
            Play(bombDrop, 0.7f, at);
        }

        // Under the explosion rather than over it: the blast is the event, this is the
        // texture of it, and a rubble cue as loud as the bang would just smear both.
        public void BlockBroken(int blocks, Vector3 at)
        {
            Play(blockBreak, blocks > 2 ? 0.6f : 0.45f, at);
        }

        // Quiet on purpose. It fires for every bomb entering its last second, including
        // ones across the board, so it has to be audible without being the loudest thing
        // in a four-player fight.
        public void FuseBurningDown(Vector3 at)
        {
            Play(fuse, 0.35f, at);
        }

        // Called once per tick that produced fire, not once per burning tile: a chain
        // across twenty tiles is one event to the ear, and twenty overlapping copies
        // of the same clip clip the mix and hurt.
        public void Exploded(int flameTiles, Vector3 at)
        {
            Play(explosion, Mathf.Lerp(0.75f, 1f, Mathf.Clamp01(flameTiles / 18f)), at);
        }

        public void PickedUp(Vector3 at)
        {
            Play(pickup, 0.85f, at);
        }

        public void Died(Vector3 at)
        {
            Play(death, 1f, at);
        }

        private void Play(AudioClip clip, float scale, Vector3 at)
        {
            if (clip == null || sources == null || sources.Length == 0)
            {
                return;
            }

            AudioSource voice = sources[nextVoice];
            nextVoice = (nextVoice + 1) % sources.Length;

            if (voice == null)
            {
                return;
            }

            float loudness = 1f;
            float pan = 0f;

            if (cameras != null && cameras.SingleViewportIsListening(out Vector3 ear, out float halfWidth))
            {
                float sideways = at.x - ear.x;
                float depth = at.z - ear.z;
                float distance = Mathf.Sqrt((sideways * sideways) + (depth * depth));

                loudness = SoundStage.LoudnessAt(distance, halfWidth);
                pan = SoundStage.PanOf(sideways, halfWidth);
            }

            voice.panStereo = pan;
            voice.PlayOneShot(clip, volume * scale * loudness);
        }
    }
}
