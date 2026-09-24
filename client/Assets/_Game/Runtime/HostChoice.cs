using Blastlands.Core;
using Blastlands.Core.Lobby;
using UnityEngine;

namespace Blastlands.Runtime
{
    public static class HostChoice
    {
        private const string SeatsKey = "blastlands.host.seats";
        private const string BotsKey = "blastlands.host.bots";

        public static int SeatCount
        {
            get { return Seats.Clamp(PlayerPrefs.GetInt(SeatsKey, Seats.Default)); }
        }

        public static BotSkill Bots
        {
            get
            {
                return BotSkills.TryRead(PlayerPrefs.GetString(BotsKey, string.Empty), out BotSkill skill)
                    ? skill
                    : BotSkill.Normal;
            }
        }

        public static void ChooseSeats(int seats)
        {
            PlayerPrefs.SetInt(SeatsKey, Seats.Clamp(seats));
            PlayerPrefs.Save();
        }

        public static void ChooseBots(BotSkill skill)
        {
            PlayerPrefs.SetString(BotsKey, BotSkills.Write(skill) ?? string.Empty);
            PlayerPrefs.Save();
        }
    }
}
