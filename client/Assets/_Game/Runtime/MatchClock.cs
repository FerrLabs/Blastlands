using Blastlands.Core;
using UnityEngine;

namespace Blastlands.Runtime
{
    // How long is left before the coast starts closing, as a fraction the HUD can drain
    // a bar with.
    //
    // Deliberately not a mm:ss readout. The rest of the HUD is bars and colour because
    // the project has no TextMeshPro and does not want it, and a number is the wrong
    // shape anyway: what a player needs mid-fight is a glance that says how much room is
    // left, not a figure to read.
    public static class MatchClock
    {
        // The last stretch, where the bar changes colour rather than starts blinking.
        // Status is colour only here, so this is the whole warning.
        public const float WarningShare = 0.25f;

        // Sudden death can be switched off, and a bar that sat permanently full would be
        // claiming there is a deadline when there is none.
        public static bool HasDeadline(MatchState state)
        {
            return state != null && state.Settings.SuddenDeath.Enabled;
        }

        // One at the start of the match, zero the moment the first ring lands, and zero
        // for the whole of the closing that follows.
        public static float Remaining(MatchState state)
        {
            if (!HasDeadline(state))
            {
                return 1f;
            }

            int start = state.Settings.SuddenDeath.StartTicks;
            if (start <= 0)
            {
                return 0f;
            }

            return Mathf.Clamp01((start - state.Tick) / (float)start);
        }

        public static bool Closing(MatchState state)
        {
            return HasDeadline(state) && state.Tick >= state.Settings.SuddenDeath.StartTicks;
        }

        public static bool Warning(MatchState state)
        {
            return HasDeadline(state) && !Closing(state) && Remaining(state) <= WarningShare;
        }
    }
}
