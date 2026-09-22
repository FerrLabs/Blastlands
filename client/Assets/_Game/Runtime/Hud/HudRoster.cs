using System.Collections.Generic;
using Blastlands.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blastlands.Runtime
{
    public sealed class HudRoster
    {
        private const float RowHeight = 84f;

        private readonly List<Row> rows = new List<Row>();

        private sealed class Row
        {
            public int PlayerIndex;
            public CanvasGroup Group;
            public TMP_Text[] Values;
            public GameObject Skull;
            public TMP_Text Number;
            public TMP_Text Name;
            public List<Image> Wins;
        }

        public static HudRoster Build(
            HudArt art, Transform area, IReadOnlyList<int> players, MatchSeries series, bool withStats, float inset, float scale)
        {
            var roster = new HudRoster();

            for (int i = 0; i < players.Count; i++)
            {
                var offset = new Vector2(inset, inset + i * RowHeight * scale);
                roster.rows.Add(BuildRow(art, area, players[i], series, withStats, offset, scale));
            }

            return roster;
        }

        public void Render(MatchState state, MatchSeries series)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                PlayerState player = state.Players[row.PlayerIndex];

                if (row.Values != null)
                {
                    HudPlacement.Write(row.Values[0], player.BombsHeld.ToString());
                    HudPlacement.Write(row.Values[1], player.FireRange.ToString());
                    HudPlacement.Write(row.Values[2], (player.SpeedSteps + 1).ToString());
                }

                HudPlacement.Write(row.Name, NameFor(player));

                if (row.Skull != null)
                {
                    row.Skull.SetActive(!player.Alive);
                }

                if (row.Number != null)
                {
                    row.Number.enabled = player.Alive;
                }

                bool won = state.Outcome == RoundOutcome.Winner && state.WinnerId == player.Id;
                row.Group.alpha = PanelMood.AlphaFor(player.Alive, state.Outcome, won);

                RenderWins(row, series);
            }
        }

        private static string NameFor(PlayerState player)
        {
            return (player.IsBot ? "BOT " : "PLAYER ") + (player.Id + 1);
        }

        private static void RenderWins(Row row, MatchSeries series)
        {
            if (row.Wins == null || series == null)
            {
                return;
            }

            int won = series.Wins(row.PlayerIndex);
            Color accent = MatchPalette.ForPlayer(row.PlayerIndex);

            for (int i = 0; i < row.Wins.Count; i++)
            {
                HudPlacement.Tint(row.Wins[i], i < won ? accent : MatchPalette.PipUnwon);
            }
        }

        private static Row BuildRow(
            HudArt art, Transform area, int playerIndex, MatchSeries series, bool withStats, Vector2 offset, float scale)
        {
            var corner = new Vector2(0f, 1f);
            Color accent = MatchPalette.ForPlayer(playerIndex);
            RectTransform root = HudPlacement.Area(area, "Player " + (playerIndex + 1), new Rect(0f, 0f, 1f, 1f));

            GameObject badge = HudPlacement.Spawn(art.Badge, root, "Badge");
            HudPlacement.Pin(badge, corner, offset, scale * 0.8f);
            TMP_Text number = HudPlacement.Text(badge);
            if (number != null)
            {
                number.text = (playerIndex + 1).ToString();
                number.color = accent;
            }

            GameObject plate = HudPlacement.Spawn(art.Row, root, "Name");
            HorizontalLayoutGroup layout = plate == null ? null : plate.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.childAlignment = TextAnchor.MiddleLeft;
            }

            HudPlacement.Pin(plate, corner, offset + new Vector2(76f * scale, 4f * scale), scale * 0.8f);
            TMP_Text name = HudPlacement.Text(plate);
            if (name != null)
            {
                name.text = "PLAYER " + (playerIndex + 1);
                name.color = accent;
                name.alignment = TextAlignmentOptions.Left;
            }

            var row = new Row
            {
                PlayerIndex = playerIndex,
                Group = root.GetComponent<CanvasGroup>(),
                Number = number,
                Name = name
            };

            if (withStats)
            {
                row.Values = BuildStats(art, root, corner, offset + new Vector2(262f * scale, 14f * scale), scale);
            }

            row.Skull = BuildIcon(art, root, art.Dead, "Dead", corner, offset + new Vector2(12f * scale, 12f * scale), scale * 1.5f);
            row.Wins = BuildWins(art, root, series, corner, offset + new Vector2(486f * scale, 16f * scale), scale);
            return row;
        }

        private static TMP_Text[] BuildStats(HudArt art, Transform root, Vector2 corner, Vector2 offset, float scale)
        {
            Sprite[] icons = { art.Bombs, art.Fire, art.Speed };
            var values = new TMP_Text[icons.Length];

            for (int i = 0; i < icons.Length; i++)
            {
                Vector2 at = offset + new Vector2(i * 70f * scale, 0f);
                BuildIcon(art, root, icons[i], "Stat " + i, corner, at, scale);

                GameObject label = HudPlacement.Spawn(art.Caption, root, "Stat " + i + " value");
                HudPlacement.Pin(label, corner, at + new Vector2(32f * scale, -6f * scale), scale * 0.5f);
                values[i] = HudPlacement.Text(label);
                if (values[i] != null)
                {
                    values[i].alignment = TextAlignmentOptions.Left;
                }
            }

            return values;
        }

        private static GameObject BuildIcon(
            HudArt art, Transform root, Sprite sprite, string name, Vector2 corner, Vector2 offset, float scale)
        {
            GameObject icon = HudPlacement.Spawn(art.Icon, root, name);
            HudPlacement.Pin(icon, corner, offset, 1f);
            if (icon == null)
            {
                return null;
            }

            ((RectTransform)icon.transform).sizeDelta = new Vector2(28f, 28f) * scale;
            Image image = icon.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = sprite;
                image.preserveAspect = true;
            }

            return icon;
        }

        private static List<Image> BuildWins(
            HudArt art, Transform root, MatchSeries series, Vector2 corner, Vector2 offset, float scale)
        {
            if (series == null)
            {
                return null;
            }

            var wins = new List<Image>();
            for (int i = 0; i < series.RoundsToWin; i++)
            {
                GameObject pip = HudPlacement.Spawn(art.RoundWon, root, "Round " + (i + 1));
                HudPlacement.Pin(pip, corner, offset + new Vector2(i * 32f * scale, 0f), scale * 0.36f);
                wins.Add(HudPlacement.Part<Image>(pip, "Icon/ICON"));
            }

            return wins;
        }
    }
}
