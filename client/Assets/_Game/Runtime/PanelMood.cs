using Blastlands.Core;

namespace Blastlands.Runtime
{
    // How brightly one player's panel is shown.
    //
    // Split out because it is the one part of the HUD that decides what a player is
    // looking at rather than where it sits, and getting a branch wrong here is quiet:
    // a corpse as bright as the living, or a winner dimmed at the moment the round is
    // being summed up, both read as the HUD being broken rather than as information.
    public static class PanelMood
    {
        public const float Full = 1f;

        // Still legible. A dead player keeps their corner and their numbers, because
        // what they were carrying when they died is worth seeing.
        public const float Dead = 0.45f;

        // Only while a finished round is being summed up, and only for whoever did not
        // win it. Lower than Dead on purpose: for that moment the winner's corner is
        // meant to be the only bright thing on screen.
        public const float Beaten = 0.25f;

        public static float AlphaFor(bool alive, RoundOutcome outcome, bool wonTheRound)
        {
            if (outcome == RoundOutcome.Running || outcome == RoundOutcome.Survived)
            {
                return alive ? Full : Dead;
            }

            if (wonTheRound)
            {
                return Full;
            }

            // A draw dims everybody, which is what a draw looks like: nobody's corner
            // is the answer.
            return Beaten;
        }
    }
}
