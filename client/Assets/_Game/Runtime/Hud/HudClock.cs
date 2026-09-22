using Blastlands.Core;
using TMPro;
using UnityEngine;

namespace Blastlands.Runtime
{
    public sealed class HudClock
    {
        private readonly CanvasGroup group;
        private readonly TMP_Text time;
        private readonly GameObject banner;

        private HudClock(CanvasGroup group, TMP_Text time, GameObject banner)
        {
            this.group = group;
            this.time = time;
            this.banner = banner;
        }

        public static HudClock Build(HudArt art, Transform area, Vector2 anchor, float inset, float scale)
        {
            RectTransform root = HudPlacement.Area(area, "Clock", new Rect(0f, 0f, 1f, 1f));

            GameObject clock = HudPlacement.Spawn(art.Clock, root, "Time");
            HudPlacement.Pin(clock, anchor, new Vector2(0f, inset), scale * 0.8f);

            GameObject banner = HudPlacement.Spawn(art.Banner, root, "Sudden death");
            HudPlacement.Pin(banner, anchor, new Vector2(0f, inset + 96f * scale), scale * 0.28f);
            HudPlacement.Write(HudPlacement.Text(banner), "SUDDEN DEATH");

            return new HudClock(root.GetComponent<CanvasGroup>(), HudPlacement.Text(clock), banner);
        }

        public void Render(MatchState state)
        {
            bool deadline = MatchClock.HasDeadline(state);
            group.alpha = deadline ? 1f : 0f;
            if (!deadline)
            {
                return;
            }

            bool closing = MatchClock.Closing(state);
            int ticksLeft = state.Settings.SuddenDeath.StartTicks - state.Tick;
            HudPlacement.Write(time, HudText.Clock(ticksLeft, state.Settings.TicksPerSecond));

            if (time != null)
            {
                time.color = closing
                    ? MatchPalette.ClockClosing
                    : MatchClock.Warning(state) ? MatchPalette.ClockWarning : Color.white;
            }

            if (banner != null && banner.activeSelf != closing)
            {
                banner.SetActive(closing);
            }
        }
    }
}
