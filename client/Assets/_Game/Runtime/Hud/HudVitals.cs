using Blastlands.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blastlands.Runtime
{
    public sealed class HudVitals
    {
        private const int Stats = 3;

        private readonly int playerIndex;
        private readonly CanvasGroup group;
        private readonly Slider[] bars;
        private readonly TMP_Text[] values;

        private HudVitals(int playerIndex, CanvasGroup group, Slider[] bars, TMP_Text[] values)
        {
            this.playerIndex = playerIndex;
            this.group = group;
            this.bars = bars;
            this.values = values;
        }

        public static HudVitals Build(HudArt art, Transform area, int playerIndex, float inset, float scale)
        {
            RectTransform root = HudPlacement.Area(area, "Vitals", new Rect(0f, 0f, 1f, 1f));
            var corner = new Vector2(0f, 0f);
            Color accent = MatchPalette.ForPlayer(playerIndex);

            GameObject badge = HudPlacement.Spawn(art.Badge, root, "Badge");
            HudPlacement.Pin(badge, corner, new Vector2(inset + 26f * scale, inset + 26f * scale), scale * 1.1f);
            TMP_Text number = HudPlacement.Text(badge);
            if (number != null)
            {
                number.text = "P" + (playerIndex + 1);
                number.color = accent;
            }

            Sprite[] icons = { art.Bombs, art.Fire, art.Speed };
            var bars = new Slider[Stats];
            var values = new TMP_Text[Stats];

            for (int i = 0; i < Stats; i++)
            {
                var offset = new Vector2(inset + (170f + i * 190f) * scale, inset + 8f * scale);

                GameObject box = HudPlacement.Spawn(art.StatBox, root, "Stat " + i);
                HudPlacement.Pin(box, corner, offset, scale * 0.8f);
                bars[i] = Meter(box, accent);

                Image icon = HudPlacement.Part<Image>(box, "Icon");
                if (icon != null)
                {
                    icon.sprite = icons[i];
                    icon.preserveAspect = true;
                }

                GameObject label = HudPlacement.Spawn(art.Label, root, "Stat " + i + " value");
                HudPlacement.Pin(label, corner, offset + new Vector2(100f * scale, 12f * scale), scale * 0.9f);
                values[i] = HudPlacement.Text(label);
                if (values[i] != null)
                {
                    values[i].alignment = TextAlignmentOptions.Left;
                }
            }

            return new HudVitals(playerIndex, root.GetComponent<CanvasGroup>(), bars, values);
        }

        public void Render(MatchState state)
        {
            PlayerState player = state.Players[playerIndex];
            MatchSettings settings = state.Settings;

            Show(0, player.BombsHeld, player.CarryCapacity, player.BombsHeld + "/" + player.CarryCapacity);
            Show(1, player.FireRange, settings.MaxFireRange, player.FireRange.ToString());
            Show(2, player.SpeedSteps + 1, settings.MaxSpeedSteps + 1, (player.SpeedSteps + 1).ToString());

            bool won = state.Outcome == RoundOutcome.Winner && state.WinnerId == player.Id;
            group.alpha = PanelMood.AlphaFor(player.Alive, state.Outcome, won);
        }

        private void Show(int index, int value, int max, string text)
        {
            if (bars[index] != null)
            {
                bars[index].value = max <= 0 ? 0f : Mathf.Clamp01(value / (float)max);
            }

            HudPlacement.Write(values[index], text);
        }

        private static Slider Meter(GameObject box, Color accent)
        {
            Slider bar = box == null ? null : box.GetComponentInChildren<Slider>(true);
            if (bar == null)
            {
                return null;
            }

            bar.minValue = 0f;
            bar.maxValue = 1f;
            bar.interactable = false;
            bar.transition = Selectable.Transition.None;

            if (bar.handleRect != null)
            {
                bar.handleRect.gameObject.SetActive(false);
                bar.handleRect = null;
            }

            if (bar.fillRect != null)
            {
                HudPlacement.Tint(bar.fillRect.GetComponent<Image>(), accent);
            }

            return bar;
        }
    }
}
