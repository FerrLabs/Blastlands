using Blastlands.Core;

namespace Blastlands.Runtime
{
    public static class HudText
    {
        public static string Clock(int ticksLeft, int ticksPerSecond)
        {
            if (ticksLeft <= 0 || ticksPerSecond <= 0)
            {
                return "0:00";
            }

            int seconds = (ticksLeft + ticksPerSecond - 1) / ticksPerSecond;
            return (seconds / 60) + ":" + (seconds % 60).ToString("00");
        }

        public static string Round(int roundsPlayed, int roundsToWin)
        {
            return "ROUND " + (roundsPlayed + 1) + "   FIRST TO " + roundsToWin;
        }

        public static string Wave(MatchState state)
        {
            SurvivalSettings survival = state.Settings.Survival;
            if (state.Zombies.Count == 0 && state.Wave < survival.Waves)
            {
                int seconds = (state.WaveCountdown + state.Settings.TicksPerSecond - 1) / state.Settings.TicksPerSecond;
                return "WAVE " + (state.Wave + 1) + " OF " + survival.Waves + " IN " + seconds;
            }

            return "WAVE " + state.Wave + " OF " + survival.Waves + "   " + state.Zombies.Count + " LEFT";
        }
    }
}
