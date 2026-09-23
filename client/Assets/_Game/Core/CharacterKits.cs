using System.Collections.Generic;

namespace Blastlands.Core
{
    public static class CharacterKits
    {
        private static readonly CharacterKind[] Roster =
        {
            CharacterKind.Demolisher,
            CharacterKind.Runner,
            CharacterKind.Hoarder,
            CharacterKind.Sapper
        };

        public static IReadOnlyList<CharacterKind> All
        {
            get { return Roster; }
        }

        // Round the roster by seat, so a full match of four has one of each and nobody
        // can end up with the only strong kit by chance. The same rule on the server and
        // on every client, which is what keeps them agreeing without the choice having
        // to travel.
        public static CharacterKind ForSeat(int seat)
        {
            int index = seat % Roster.Length;
            return Roster[index < 0 ? index + Roster.Length : index];
        }

        // Applied once, at spawn. Capped at the same ceilings the pickups respect, so a
        // kit is a head start on a power-up and never a way past what one can reach.
        public static void Apply(PlayerState player, CharacterKind kind, MatchSettings settings)
        {
            switch (kind)
            {
                case CharacterKind.Demolisher:
                    player.FireRange = System.Math.Min(player.FireRange + 1, settings.MaxFireRange);
                    break;
                case CharacterKind.Runner:
                    player.SpeedSteps = System.Math.Min(player.SpeedSteps + 2, settings.MaxSpeedSteps);
                    break;
                case CharacterKind.Hoarder:
                    player.CarryCapacity = System.Math.Min(player.CarryCapacity + 1, settings.MaxCarryCapacity);
                    player.BombsHeld = System.Math.Min(player.BombsHeld + 1, player.CarryCapacity);
                    break;
                case CharacterKind.Sapper:
                    player.NextBombKind = BombKind.Pierce;
                    break;
            }
        }
    }
}
