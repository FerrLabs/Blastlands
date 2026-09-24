using System;
using System.Collections.Generic;

namespace Blastlands.Core
{
    public static class CharacterKits
    {
        private static readonly CharacterKind[] Roster =
        {
            CharacterKind.Demolisher,
            CharacterKind.Runner,
            CharacterKind.Grenadier,
            CharacterKind.Sapper
        };

        public static IReadOnlyList<CharacterKind> All
        {
            get { return Roster; }
        }

        public static CharacterKind Shown(CharacterKind pick)
        {
            return pick == CharacterKind.None ? Roster[0] : pick;
        }

        public static int IndexOf(CharacterKind character)
        {
            return Array.IndexOf(Roster, character);
        }

        public static CharacterKind Step(CharacterKind from, int delta)
        {
            int at = IndexOf(Shown(from));
            int next = (at + delta) % Roster.Length;
            return Roster[next < 0 ? next + Roster.Length : next];
        }

        // Round the roster by seat, so a full match of four has one of each and nobody
        // can end up with the only strong kit by chance.
        public static CharacterKind ForSeat(int seat)
        {
            int index = seat % Roster.Length;
            return Roster[index < 0 ? index + Roster.Length : index];
        }

        public static Func<int, CharacterKind> Seating(CharacterKind pick, int turn)
        {
            return seat => seat == 0 && pick != CharacterKind.None ? pick : ForSeat(seat + turn);
        }

        // Applied once, at spawn. Capped at the same ceilings the pickups respect, so a
        // kit is a head start on a power-up and never a way past what one can reach.
        //
        // Carry capacity is not on the list, and that is deliberate. Starting at one is
        // what keeps a player from having two blasts live at once, which is how a player
        // walls themselves into their own fire: MatchSettings.Default withholds it on
        // purpose and makes BombUp the way to earn it. A kit that handed it out would
        // undo that guard for one seat in every match from the first tick.
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
                case CharacterKind.Grenadier:
                case CharacterKind.Sapper:
                    player.NextBombKind = BombKindFor(kind);
                    break;
            }
        }

        public static BombKind BombKindFor(CharacterKind kind)
        {
            switch (kind)
            {
                case CharacterKind.Grenadier:
                    return BombKind.Cluster;
                case CharacterKind.Sapper:
                    return BombKind.Pierce;
                default:
                    return BombKind.Standard;
            }
        }
    }
}
