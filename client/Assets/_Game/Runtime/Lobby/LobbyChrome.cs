using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blastlands.Runtime
{
    // Builds the lobby out of the same Synty pieces the match HUD uses. Nothing here
    // draws: a panel is a pack sprite stretched, a button is the pack's button prefab,
    // and text is the pack's label prefab, which carries its font and its colour.
    public static class LobbyChrome
    {
        public static RectTransform Panel(Transform parent, LobbyArt art, string name, Vector2 size)
        {
            var host = new GameObject(name, typeof(RectTransform), typeof(Image));
            host.transform.SetParent(parent, false);

            var rect = (RectTransform)host.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;

            Image image = host.GetComponent<Image>();
            image.sprite = art.Panel;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;

            return rect;
        }

        public static TMP_Text Label(Transform parent, LobbyArt art, string text, bool heading, Vector2 offset, float width)
        {
            GameObject instance = UnityEngine.Object.Instantiate(heading ? art.Header : art.Body, parent, false);
            instance.name = heading ? "Heading" : "Line";

            var rect = (RectTransform)instance.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);

            TMP_Text label = instance.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = text;
                label.alignment = TextAlignmentOptions.Center;
            }

            return label;
        }

        public static Button Press(Transform parent, LobbyArt art, string text, Vector2 offset, Vector2 size, Action clicked)
        {
            GameObject instance = UnityEngine.Object.Instantiate(art.Button, parent, false);
            instance.name = "Button " + text;

            var rect = (RectTransform)instance.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;

            TMP_Text label = instance.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = text;
                label.alignment = TextAlignmentOptions.Center;
            }

            Button press = instance.GetComponentInChildren<Button>(true);
            if (press == null)
            {
                press = instance.AddComponent<Button>();
                press.targetGraphic = instance.GetComponentInChildren<Image>(true);
            }

            press.onClick.AddListener(() => clicked());
            return press;
        }

        // Built rather than instantiated: the pack has no text field, and a field is an
        // image from the pack with the pack's label inside it, which is the same thing
        // the rest of the screen is made of.
        public static TMP_InputField Field(Transform parent, LobbyArt art, string placeholder, Vector2 offset, Vector2 size)
        {
            var host = new GameObject("Field", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            host.transform.SetParent(parent, false);

            var rect = (RectTransform)host.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;

            Image background = host.GetComponent<Image>();
            background.sprite = art.Field;
            background.type = Image.Type.Sliced;

            var viewport = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(host.transform, false);
            RectTransform area = Stretch(viewport, 16f);

            TMP_Text typed = Label(viewport.transform, art, string.Empty, false, Vector2.zero, size.x - 32f);
            TMP_Text hint = Label(viewport.transform, art, placeholder, false, Vector2.zero, size.x - 32f);
            Stretch(typed.gameObject, 0f);
            Stretch(hint.gameObject, 0f);
            hint.color = new Color(hint.color.r, hint.color.g, hint.color.b, 0.4f);

            TMP_InputField field = host.GetComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = typed;
            field.placeholder = hint;
            field.characterLimit = Core.Lobby.DisplayName.MaxLength;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.targetGraphic = background;
            field.onFocusSelectAll = false;

            return field;
        }

        public static RectTransform Stretch(GameObject instance, float inset)
        {
            var rect = (RectTransform)instance.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }
    }
}
