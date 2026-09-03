using UnityEngine;

namespace Blastlands.Runtime
{
    // How hard one viewport is shaken, and by what.
    //
    // One of these per viewport rather than one for the camera rig. In split-screen four
    // people are watching four different parts of the board, and a blast that lands next
    // to one of them should not jolt the other three.
    public sealed class CameraShake
    {
        // Past this a blast is somebody else's problem. Roughly the radius of a follow
        // camera's view, so the rule is that you feel what you could have seen.
        public const float ReachTiles = 9f;

        // What the biggest blast in the game covers. Anything at or above this counts as
        // full strength rather than scaling on forever, or a long chain reaction would
        // shake harder than the screen can express.
        private const int FullBlastTiles = 12;

        // A blast that only just reaches you still registers. Zero here would make small
        // bombs silent to the camera, which reads as the shake being broken rather than
        // as the bomb being small.
        private const float SmallestShare = 0.35f;

        private const float DecaySeconds = 0.45f;

        // Two frequencies that do not divide into each other, so the offset never repeats
        // a straight line back and forth, which reads as a wobble rather than a jolt.
        private const float FastHertz = 31f;
        private const float SlowHertz = 23f;

        private readonly float phase;
        private float strength;
        private float elapsed;

        // The phase is what keeps two viewports from shaking in lockstep when the same
        // bomb reaches both of them.
        public CameraShake(float phase)
        {
            this.phase = phase;
        }

        public float Strength
        {
            get { return strength; }
        }

        // Takes the strongest rather than adding. Two bombs going off together are one
        // event to whoever is watching, and summing them throws the camera far enough to
        // read as a bug.
        public void Felt(float amount)
        {
            if (amount > strength)
            {
                strength = Mathf.Clamp01(amount);
                elapsed = 0f;
            }
        }

        public Vector2 Advance(float deltaSeconds, float maxOffset)
        {
            if (strength <= 0f)
            {
                return Vector2.zero;
            }

            elapsed += deltaSeconds;
            strength = Mathf.MoveTowards(strength, 0f, deltaSeconds / DecaySeconds);

            float reach = strength * maxOffset;
            return new Vector2(
                Mathf.Sin(((elapsed * FastHertz) + phase) * Mathf.PI * 2f) * reach,
                Mathf.Sin(((elapsed * SlowHertz) + phase + 0.37f) * Mathf.PI * 2f) * reach);
        }

        // Squared falloff rather than linear: a blast is felt sharply where it lands and
        // barely at the edge of the view, which is how the distance reads on screen.
        public static float StrengthOf(float distanceInTiles, int flameTiles, float reachTiles)
        {
            if (reachTiles <= 0f || distanceInTiles >= reachTiles || flameTiles <= 0)
            {
                return 0f;
            }

            float falloff = 1f - (Mathf.Max(distanceInTiles, 0f) / reachTiles);
            float size = Mathf.Clamp01(flameTiles / (float)FullBlastTiles);

            return falloff * falloff * Mathf.Lerp(SmallestShare, 1f, size);
        }
    }
}
