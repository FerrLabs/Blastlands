using Blastlands.Core;
using TMPro;
using UnityEngine;

namespace Blastlands.Runtime
{
    public sealed class HudRound
    {
        private readonly TMP_Text text;

        private HudRound(TMP_Text text)
        {
            this.text = text;
        }

        public static HudRound Build(HudArt art, Transform area, float inset, float scale)
        {
            GameObject plate = HudPlacement.Spawn(art.Row, area, "Round");
            HudPlacement.Pin(plate, new Vector2(1f, 1f), new Vector2(inset, inset), scale * 0.7f);
            return new HudRound(HudPlacement.Text(plate));
        }

        public void Render(MatchSeries series, MatchState state)
        {
            if (state.Settings.Survival.Enabled)
            {
                HudPlacement.Write(text, HudText.Wave(state));
                return;
            }

            if (series != null)
            {
                HudPlacement.Write(text, HudText.Round(series.RoundsPlayed, series.RoundsToWin));
            }
        }
    }
}
