using UnityEngine;

namespace Blastlands.Runtime
{
    public static class SoundStage
    {
        public const float FadeTiles = 8f;
        public const float FarShare = 0.2f;
        public const float HardestPan = 0.75f;

        private const float PanReachInScreenWidths = 2f;

        public static float PanOf(float sidewaysTiles, float halfWidthTiles)
        {
            if (halfWidthTiles <= 0f)
            {
                return 0f;
            }

            float share = sidewaysTiles / (halfWidthTiles * PanReachInScreenWidths);
            return Mathf.Clamp(share, -1f, 1f) * HardestPan;
        }

        public static float LoudnessAt(float distanceInTiles, float halfWidthTiles)
        {
            float edge = Mathf.Max(halfWidthTiles, 0f);
            float beyondTheEdge = Mathf.Max(distanceInTiles - edge, 0f);
            float nearness = 1f - Mathf.Clamp01(beyondTheEdge / FadeTiles);

            return Mathf.Lerp(FarShare, 1f, nearness * nearness);
        }
    }
}
