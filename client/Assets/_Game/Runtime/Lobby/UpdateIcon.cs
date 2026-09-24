using UnityEngine;

namespace Blastlands.Runtime
{
    public static class UpdateIcon
    {
        private const int Size = 64;

        private static Sprite sprite;

        public static Sprite Sprite
        {
            get
            {
                if (sprite == null)
                {
                    sprite = Build();
                }

                return sprite;
            }
        }

        private static Sprite Build()
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "Update icon",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    pixels[(y * Size) + x] = Inside(x, y) ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static bool Inside(int x, int y)
        {
            bool tray = y >= 6 && y <= 10 && x >= 10 && x <= 54;
            bool shaft = x >= 27 && x <= 37 && y >= 30 && y <= 54;
            bool head = y >= 14 && y <= 30 && Mathf.Abs(x - 32) <= (y - 14) * 18f / 16f;
            return tray || shaft || head;
        }
    }
}
