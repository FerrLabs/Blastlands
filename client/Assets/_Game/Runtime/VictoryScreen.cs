using System;
using Blastlands.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Blastlands.Runtime
{
    public sealed class VictoryScreen : MonoBehaviour
    {
        private const int Layer = 400;

        private static readonly Vector2 Reference = new Vector2(1920f, 1080f);
        private static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);
        private static readonly Color Scrim = new Color(0.03f, 0.03f, 0.04f, 0.82f);
        private static readonly Color Card = new Color(0.07f, 0.08f, 0.10f, 0.95f);
        private static readonly Color Won = new Color(0.98f, 0.80f, 0.30f);
        private static readonly Color Lost = new Color(0.90f, 0.35f, 0.25f);
        private static readonly Color Drawn = new Color(0.85f, 0.85f, 0.88f);
        private static readonly Color ButtonFace = new Color(0.20f, 0.22f, 0.26f, 1f);
        private static readonly Color ButtonText = new Color(0.95f, 0.93f, 0.88f);

        private const float NoPortraitShrink = 380f;

        private CharacterStage stage;

        public static VictoryScreen Open(
            Transform parent,
            MatchArt art,
            VictoryCard card,
            string primary,
            Action onPrimary,
            string secondary,
            Action onSecondary)
        {
            var host = new GameObject("Victory screen");
            host.transform.SetParent(parent, false);

            VictoryScreen screen = host.AddComponent<VictoryScreen>();
            screen.Build(art, card, primary, onPrimary, secondary, onSecondary);
            return screen;
        }

        public void Close()
        {
            Destroy(gameObject);
        }

        private void Build(MatchArt art, VictoryCard card, string primary, Action onPrimary, string secondary, Action onSecondary)
        {
            EnsureEventSystem();

            var canvasHost = new GameObject("Victory canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasHost.transform.SetParent(transform, false);

            Canvas canvas = canvasHost.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = Layer;

            CanvasScaler scaler = canvasHost.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.matchWidthOrHeight = 0.5f;

            bool showsWinner = card.HasWinner && art != null;
            float lift = showsWinner ? 0f : NoPortraitShrink / 2f;

            Fill(canvasHost.transform, "Scrim", Scrim, Vector2.zero, Vector2.zero, true);
            RectTransform panel = Fill(
                canvasHost.transform, "Card", Card, Vector2.zero, new Vector2(1100f, 760f - (lift * 2f)), false);

            Write(panel, card.Heading, 96f, FontStyles.Bold, ToneColour(card.Tone), new Vector2(0f, 290f - lift), new Vector2(1000f, 120f));

            if (showsWinner)
            {
                stage = new GameObject("Victory stage").AddComponent<CharacterStage>();
                stage.transform.SetParent(transform, false);
                stage.ShowModel(art.PlayerFor(card.Winner, card.Character), art.PlayerAnimator);

                var portrait = new GameObject("Winner", typeof(RectTransform), typeof(RawImage));
                portrait.transform.SetParent(panel, false);
                Centre((RectTransform)portrait.transform, new Vector2(0f, 40f), new Vector2(300f, 420f));

                RawImage image = portrait.GetComponent<RawImage>();
                image.texture = stage.Texture;
                image.raycastTarget = false;
            }

            string detail = card.Detail;
            if (card.Character != CharacterKind.None)
            {
                detail += " Played as the " + card.Character + ".";
            }

            Write(panel, detail, 34f, FontStyles.Normal, Drawn, new Vector2(0f, -210f + lift), new Vector2(1000f, 90f));

            Button first = Press(panel, primary, new Vector2(secondary == null ? 0f : -200f, -310f + lift), onPrimary);
            if (secondary != null)
            {
                Press(panel, secondary, new Vector2(200f, -310f + lift), onSecondary);
            }

            EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var host = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            host.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private static Color ToneColour(VictoryTone tone)
        {
            switch (tone)
            {
                case VictoryTone.Won:
                    return Won;
                case VictoryTone.Lost:
                    return Lost;
                default:
                    return Drawn;
            }
        }

        private static RectTransform Fill(Transform parent, string name, Color colour, Vector2 offset, Vector2 size, bool stretch)
        {
            var area = new GameObject(name, typeof(RectTransform), typeof(Image));
            area.transform.SetParent(parent, false);

            var frame = (RectTransform)area.transform;
            if (stretch)
            {
                frame.anchorMin = Vector2.zero;
                frame.anchorMax = Vector2.one;
                frame.offsetMin = Vector2.zero;
                frame.offsetMax = Vector2.zero;
            }
            else
            {
                Centre(frame, offset, size);
            }

            Image fill = area.GetComponent<Image>();
            fill.color = colour;
            fill.raycastTarget = stretch;
            return frame;
        }

        private static void Centre(RectTransform frame, Vector2 offset, Vector2 size)
        {
            frame.anchorMin = Middle;
            frame.anchorMax = Middle;
            frame.pivot = Middle;
            frame.anchoredPosition = offset;
            frame.sizeDelta = size;
        }

        private static TMP_Text Write(
            RectTransform parent, string content, float size, FontStyles style, Color colour, Vector2 offset, Vector2 box)
        {
            var area = new GameObject("Text", typeof(RectTransform));
            area.transform.SetParent(parent, false);

            var frame = (RectTransform)area.transform;
            Centre(frame, offset, box);

            TMP_Text text = area.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = colour;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static Button Press(RectTransform parent, string label, Vector2 offset, Action clicked)
        {
            var host = new GameObject("Button " + label, typeof(RectTransform), typeof(Image), typeof(Button));
            host.transform.SetParent(parent, false);

            var frame = (RectTransform)host.transform;
            Centre(frame, offset, new Vector2(340f, 84f));

            Image face = host.GetComponent<Image>();
            face.color = ButtonFace;

            Button press = host.GetComponent<Button>();
            press.targetGraphic = face;
            press.onClick.AddListener(() => clicked?.Invoke());

            TMP_Text text = Write(frame, label, 36f, FontStyles.Bold, ButtonText, Vector2.zero, frame.sizeDelta);
            text.raycastTarget = false;
            return press;
        }
    }
}
