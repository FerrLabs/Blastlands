using System.Collections.Generic;
using Blastlands.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Blastlands.Runtime
{
    // One panel per player, pinned to a corner, built from the Synty Apocalypse HUD
    // pack. Like MatchView it renders state and owns none of it, so it can be switched
    // off without changing a match.
    //
    // Stats are bars rather than numbers because each one is small and capped, so what
    // matters is how close to the cap it is. It also keeps the HUD clear of TextMeshPro.
    public sealed class MatchHud : MonoBehaviour
    {
        private static readonly Vector2[] Corners =
        {
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 0f),
            new Vector2(1f, 0f)
        };

        [SerializeField] private HudArt art;
        [SerializeField] private Vector2 panelSize = new Vector2(340f, 156f);
        [SerializeField] private float margin = 26f;

        // The pack's plate is light metal, which leaves the white icons and the bars
        // sitting on top of it with almost no contrast.
        [SerializeField] private Color plateTint = new Color(0.24f, 0.23f, 0.21f, 0.95f);

        // The pack authors the diode large enough to headline a panel of its own.
        [SerializeField] private float diodeSize = 26f;

        private readonly List<Panel> panels = new List<Panel>();
        private MatchState state;
        private Canvas canvas;

        private sealed class Panel
        {
            public CanvasGroup Group;
            public Slider[] Bars;
            public Image[] Fills;
            public GameObject Skull;
        }

        public void Bind(MatchState matchState)
        {
            state = matchState;
            Rebuild();
        }

        public void Render()
        {
            if (state == null)
            {
                return;
            }

            for (int i = 0; i < panels.Count && i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                Panel panel = panels[i];

                // What is carried, against what could be carried. The bar empties as
                // bombs are spent, which is the number that decides what you can do.
                Show(panel, 0, player.BombsHeld, player.CarryCapacity);
                Show(panel, 1, player.FireRange, state.Settings.MaxFireRange);
                Show(panel, 2, player.SpeedSteps + 1, state.Settings.MaxSpeedSteps + 1);

                // A dead player keeps their corner. Who is left is the state of the
                // round, and a panel that vanished would reshuffle the others.
                if (panel.Skull != null)
                {
                    panel.Skull.SetActive(!player.Alive);
                }

                if (panel.Group != null)
                {
                    panel.Group.alpha = player.Alive ? 1f : 0.45f;
                }
            }
        }

        private static void Show(Panel panel, int index, int value, int max)
        {
            if (index >= panel.Bars.Length || panel.Bars[index] == null)
            {
                return;
            }

            panel.Bars[index].value = max <= 0 ? 0f : Mathf.Clamp01(value / (float)max);
        }

        private void Rebuild()
        {
            for (int i = 0; i < panels.Count; i++)
            {
                if (panels[i].Group != null)
                {
                    Destroy(panels[i].Group.gameObject);
                }
            }

            panels.Clear();

            if (state == null)
            {
                return;
            }

            EnsureCanvas();

            for (int i = 0; i < state.Players.Count; i++)
            {
                panels.Add(BuildPanel(i));
            }

            Render();
        }

        private void EnsureCanvas()
        {
            if (canvas != null)
            {
                return;
            }

            var host = new GameObject("MatchHud Canvas", typeof(Canvas), typeof(CanvasScaler));
            host.transform.SetParent(transform, false);

            canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = host.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        private Panel BuildPanel(int index)
        {
            Vector2 corner = Corners[index % Corners.Length];

            // Past four players the corners are taken, so the extras stack inwards along
            // the same edge rather than landing on top of each other.
            int row = index / Corners.Length;

            var root = new GameObject("Player " + (index + 1), typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            root.transform.SetParent(canvas.transform, false);

            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = corner;
            rect.anchorMax = corner;
            rect.pivot = corner;
            rect.sizeDelta = panelSize;
            rect.anchoredPosition = new Vector2(
                corner.x > 0.5f ? -margin : margin,
                (corner.y > 0.5f ? -1f : 1f) * (margin + (row * (panelSize.y + 10f))));

            Color accent = MatchPalette.ForPlayer(index);

            Image background = root.GetComponent<Image>();
            background.color = plateTint;

            if (art != null && art.Panel != null)
            {
                background.sprite = art.Panel;
                background.type = Image.Type.Sliced;
            }

            BuildDiode(root.transform, accent);

            var bars = new Slider[3];
            var fills = new Image[3];
            GameObject skull = BuildSkull(root.transform);

            if (art != null && art.StatsList != null)
            {
                GameObject list = Instantiate(art.StatsList, root.transform);
                var listRect = list.GetComponent<RectTransform>();
                listRect.anchorMin = Vector2.zero;
                listRect.anchorMax = Vector2.one;
                listRect.offsetMin = new Vector2(12f, 10f);
                listRect.offsetMax = new Vector2(-12f, -34f);

                Sprite[] icons = { art.Bombs, art.Fire, art.Speed };

                for (int i = 0; i < list.transform.childCount; i++)
                {
                    Transform child = list.transform.GetChild(i);

                    if (i >= bars.Length)
                    {
                        child.gameObject.SetActive(false);
                        continue;
                    }

                    bars[i] = child.GetComponentInChildren<Slider>(true);
                    if (bars[i] != null)
                    {
                        bars[i].minValue = 0f;
                        bars[i].maxValue = 1f;
                        bars[i].interactable = false;
                        bars[i].transition = Selectable.Transition.None;

                        // The handle is for dragging, which these never are.
                        if (bars[i].handleRect != null)
                        {
                            bars[i].handleRect.gameObject.SetActive(false);
                            bars[i].handleRect = null;
                        }

                        if (bars[i].fillRect != null)
                        {
                            fills[i] = bars[i].fillRect.GetComponent<Image>();
                            if (fills[i] != null)
                            {
                                fills[i].color = accent;
                            }
                        }
                    }

                    Transform icon = child.Find("Icon");
                    if (icon != null && icons[i] != null)
                    {
                        Image image = icon.GetComponent<Image>();
                        image.sprite = icons[i];

                        // The slot is wider than it is tall, and these icons are not.
                        image.preserveAspect = true;
                    }
                }
            }

            return new Panel
            {
                Group = root.GetComponent<CanvasGroup>(),
                Bars = bars,
                Fills = fills,
                Skull = skull
            };
        }

        private GameObject BuildSkull(Transform parent)
        {
            if (art == null || art.Icon == null || art.Dead == null)
            {
                return null;
            }

            GameObject skull = Instantiate(art.Icon, parent);
            skull.name = "Dead";

            var rect = skull.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(40f, 40f);
            rect.anchoredPosition = new Vector2(-14f, -14f);

            Image image = skull.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = art.Dead;
                image.preserveAspect = true;
            }

            skull.transform.SetAsLastSibling();
            skull.SetActive(false);
            return skull;
        }

        // The pack's indicator light, tinted to the player. A HUD diode is already the
        // thing that says "this panel is yours" without any text.
        private void BuildDiode(Transform parent, Color accent)
        {
            if (art == null || art.Diode == null)
            {
                return;
            }

            GameObject diode = Instantiate(art.Diode, parent);
            diode.name = "Identity";

            // The glow is a particle system, which a RectTransform cannot size: it keeps
            // its authored scale and spills across the arena. The lit sprite alone is
            // what carries the colour anyway.
            foreach (ParticleSystem system in diode.GetComponentsInChildren<ParticleSystem>(true))
            {
                system.gameObject.SetActive(false);
            }

            var rect = diode.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);

            // Sizing the root leaves the children at their authored size, so the whole
            // thing is scaled instead.
            float scale = rect.rect.width > 0f ? diodeSize / rect.rect.width : 1f;
            diode.transform.localScale = Vector3.one * scale;
            rect.anchoredPosition = new Vector2(12f, -6f);

            foreach (Image image in diode.GetComponentsInChildren<Image>(true))
            {
                image.color = accent;
            }
        }
    }
}
