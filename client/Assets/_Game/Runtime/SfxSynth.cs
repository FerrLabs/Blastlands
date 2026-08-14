using UnityEngine;

namespace Blastlands.Runtime
{
    // Synthesised placeholder effects. There is no audio in any of the Synty packs,
    // and shipping silence hides bugs that sound makes obvious, so the clips are
    // generated at load rather than authored.
    //
    // Replacing them means dropping authored clips onto MatchAudio; nothing else
    // in the game reaches for this class.
    public static class SfxSynth
    {
        private const int SampleRate = 44100;

        public static AudioClip BombDrop()
        {
            float[] samples = Buffer(0.14f);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = Seconds(i);
                float body = Mathf.Sin(2f * Mathf.PI * 150f * t);
                float click = Mathf.Sin(2f * Mathf.PI * 620f * t) * Decay(t, 55f) * 0.25f;
                samples[i] = (body * Decay(t, 26f)) + click;
            }

            return Finish("SfxBombDrop", samples, 0.6f);
        }

        // Noise for the debris, a downward sweep for the weight underneath it. Noise
        // alone reads as static; the sweep is what makes it land.
        public static AudioClip Explosion()
        {
            float[] samples = Buffer(0.85f);
            var random = new System.Random(20260814);
            float lowPassed = 0f;
            float phase = 0f;

            for (int i = 0; i < samples.Length; i++)
            {
                float t = Seconds(i);
                float noise = ((float)random.NextDouble() * 2f) - 1f;
                lowPassed = Mathf.Lerp(lowPassed, noise, 0.16f);

                float sweep = Mathf.Lerp(110f, 34f, Mathf.Clamp01(t / 0.5f));
                phase += 2f * Mathf.PI * sweep / SampleRate;

                float rumble = Mathf.Sin(phase) * Decay(t, 4.6f);
                samples[i] = (lowPassed * 2.2f * Decay(t, 5.6f)) + (rumble * 0.8f);
            }

            return Finish("SfxExplosion", samples, 0.85f);
        }

        public static AudioClip Pickup()
        {
            float[] samples = Buffer(0.22f);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = Seconds(i);
                float frequency = t < 0.07f ? 988f : 1319f;
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * Decay(t, 11f);
            }

            return Finish("SfxPickup", samples, 0.45f);
        }

        public static AudioClip Death()
        {
            float[] samples = Buffer(0.6f);
            float phase = 0f;

            for (int i = 0; i < samples.Length; i++)
            {
                float t = Seconds(i);
                float frequency = Mathf.Lerp(420f, 90f, Mathf.Clamp01(t / 0.6f));
                phase += 2f * Mathf.PI * frequency / SampleRate;
                samples[i] = Mathf.Sin(phase) * Decay(t, 5.2f);
            }

            return Finish("SfxDeath", samples, 0.55f);
        }

        private static float[] Buffer(float seconds)
        {
            return new float[Mathf.CeilToInt(SampleRate * seconds)];
        }

        private static float Seconds(int sample)
        {
            return sample / (float)SampleRate;
        }

        private static float Decay(float t, float rate)
        {
            return Mathf.Exp(-t * rate);
        }

        // A waveform that starts at full amplitude pops. Peak normalisation keeps the
        // relative loudness of the effects a decision made here rather than an
        // accident of how each one happens to be synthesised.
        private static AudioClip Finish(string name, float[] samples, float peak)
        {
            int fade = Mathf.Min(220, samples.Length);
            for (int i = 0; i < fade; i++)
            {
                samples[i] *= i / (float)fade;
                samples[samples.Length - 1 - i] *= i / (float)fade;
            }

            float loudest = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                loudest = Mathf.Max(loudest, Mathf.Abs(samples[i]));
            }

            if (loudest > 0.0001f)
            {
                float scale = peak / loudest;
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] *= scale;
                }
            }

            AudioClip clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
