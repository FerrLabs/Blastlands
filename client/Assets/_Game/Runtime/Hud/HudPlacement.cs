using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blastlands.Runtime
{
    public static class HudPlacement
    {
        public static GameObject Spawn(GameObject prefab, Transform parent, string name)
        {
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, parent);
            instance.name = name;

            foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
            {
                animator.enabled = false;
            }

            return instance;
        }

        public static void Pin(GameObject instance, Vector2 corner, Vector2 offset, float scale)
        {
            if (instance == null)
            {
                return;
            }

            var rect = (RectTransform)instance.transform;
            rect.anchorMin = corner;
            rect.anchorMax = corner;
            rect.pivot = corner;
            rect.localScale = Vector3.one * scale;
            rect.anchoredPosition = Inward(corner, offset);
        }

        public static Vector2 Inward(Vector2 corner, Vector2 offset)
        {
            return new Vector2(
                corner.x > 0.5f ? -offset.x : offset.x,
                corner.y > 0.5f ? -offset.y : offset.y);
        }

        public static RectTransform Area(Transform parent, string name, Rect viewport)
        {
            var area = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)area.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = viewport.min;
            rect.anchorMax = viewport.max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static T Part<T>(GameObject instance, string path) where T : Component
        {
            if (instance == null)
            {
                return null;
            }

            Transform part = instance.transform.Find(path);
            return part == null ? null : part.GetComponent<T>();
        }

        public static void Hide(GameObject instance, string path)
        {
            Transform part = instance == null ? null : instance.transform.Find(path);
            if (part != null)
            {
                part.gameObject.SetActive(false);
            }
        }

        public static TMP_Text Text(GameObject instance)
        {
            return instance == null ? null : instance.GetComponentInChildren<TMP_Text>(true);
        }

        public static void Write(TMP_Text text, string value)
        {
            if (text != null && text.text != value)
            {
                text.text = value;
            }
        }

        public static void Tint(Image image, Color colour)
        {
            if (image != null)
            {
                image.color = colour;
            }
        }
    }
}
