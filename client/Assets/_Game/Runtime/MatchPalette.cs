using UnityEngine;

namespace Blastlands.Runtime
{
    public static class MatchPalette
    {
        public static readonly Color Floor = new Color(0.16f, 0.18f, 0.15f);
        public static readonly Color HardBlock = new Color(0.28f, 0.30f, 0.34f);
        public static readonly Color SoftBlock = new Color(0.55f, 0.36f, 0.20f);
        public static readonly Color Bush = new Color(0.22f, 0.42f, 0.20f);
        public static readonly Color Bomb = new Color(0.09f, 0.09f, 0.11f);
        public static readonly Color Flame = new Color(0.95f, 0.45f, 0.12f);

        // The FerrLabs product accents, which happen to be four highly separable hues.
        public static readonly Color[] Players =
        {
            new Color(0.91f, 0.45f, 0.23f),
            new Color(0.06f, 0.73f, 0.51f),
            new Color(0.39f, 0.40f, 0.95f),
            new Color(0.49f, 0.23f, 0.93f),
            new Color(0.96f, 0.62f, 0.04f),
            new Color(0.08f, 0.72f, 0.65f),
            new Color(0.93f, 0.28f, 0.44f),
            new Color(0.85f, 0.85f, 0.90f)
        };

        public static Color ForPlayer(int id)
        {
            return Players[((id % Players.Length) + Players.Length) % Players.Length];
        }

        public static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader);
            material.color = color;
            material.SetFloat("_Smoothness", 0.12f);
            material.hideFlags = HideFlags.DontSave;
            return material;
        }

        public static Material CreateEmissive(Color color)
        {
            Material material = CreateMaterial(color);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 2.2f);
            return material;
        }
    }
}
