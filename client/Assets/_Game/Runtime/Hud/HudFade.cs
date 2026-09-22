using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Blastlands.Runtime
{
    public sealed class HudFade
    {
        public const float Faded = 0.25f;
        private const float Speed = 6f;
        private const float Padding = 24f;

        private readonly CanvasGroup group;
        private readonly Graphic[] graphics;
        private readonly Vector3[] corners = new Vector3[4];

        public HudFade(RectTransform block)
        {
            group = block.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = block.gameObject.AddComponent<CanvasGroup>();
            }

            graphics = block.GetComponentsInChildren<Graphic>(true);
        }

        public void Update(IReadOnlyList<Vector2> players, float deltaTime)
        {
            float target = Covers(Bounds(), players, Padding) ? Faded : 1f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, Speed * deltaTime);
        }

        public static bool Covers(Rect bounds, IReadOnlyList<Vector2> points, float padding)
        {
            if (bounds.width <= 0f || bounds.height <= 0f)
            {
                return false;
            }

            Rect padded = new Rect(bounds.x - padding, bounds.y - padding, bounds.width + 2f * padding, bounds.height + 2f * padding);
            for (int i = 0; i < points.Count; i++)
            {
                if (padded.Contains(points[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private Rect Bounds()
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            foreach (Graphic graphic in graphics)
            {
                if (graphic == null || !graphic.isActiveAndEnabled)
                {
                    continue;
                }

                graphic.rectTransform.GetWorldCorners(corners);
                min = Vector2.Min(min, Vector2.Min(corners[0], corners[2]));
                max = Vector2.Max(max, Vector2.Max(corners[0], corners[2]));
            }

            return min.x > max.x ? Rect.zero : Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
