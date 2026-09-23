using System.Collections.Generic;

namespace Blastlands.Core
{
    public static class Abilities
    {
        public const int NoBomb = -1;

        public static bool Has(CharacterKind character)
        {
            return character == CharacterKind.Demolisher || character == CharacterKind.Runner;
        }

        public static void Resolve(MatchState state, IReadOnlyList<PlayerInput> inputs)
        {
            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                if (!player.Alive)
                {
                    continue;
                }

                if (player.AbilityCooldownRemaining > 0)
                {
                    player.AbilityCooldownRemaining--;
                }

                if (player.VanishTicksRemaining > 0)
                {
                    player.VanishTicksRemaining--;
                }

                bool wants = i < inputs.Count && inputs[i].Ability;
                if (!wants || !player.CanUseAbility || !state.Settings.Rules.AllowsCharacters || !Use(state, player))
                {
                    continue;
                }

                player.AbilityCooldownRemaining = state.Settings.Abilities.CooldownFor(player.Character);
                if (player.Character != CharacterKind.Runner)
                {
                    player.RevealTicksRemaining = state.Settings.Vision.RevealTicks;
                }
            }
        }

        public static int OldestLiveBomb(MatchState state, int playerId)
        {
            for (int i = 0; i < state.Bombs.Count; i++)
            {
                ActiveBomb bomb = state.Bombs[i];
                if (bomb.Bomb.OwnerId == playerId && bomb.FuseRemaining > 1)
                {
                    return i;
                }
            }

            return NoBomb;
        }

        private static bool Use(MatchState state, PlayerState player)
        {
            switch (player.Character)
            {
                case CharacterKind.Demolisher:
                    return Trigger(state, player);
                case CharacterKind.Runner:
                    return Vanish(state, player);
                default:
                    return false;
            }
        }

        private static bool Vanish(MatchState state, PlayerState player)
        {
            player.VanishTicksRemaining = state.Settings.Abilities.VanishTicks;
            player.RevealTicksRemaining = 0;
            return true;
        }

        private static bool Trigger(MatchState state, PlayerState player)
        {
            int oldest = OldestLiveBomb(state, player.Id);
            if (oldest == NoBomb)
            {
                return false;
            }

            state.Bombs[oldest].FuseRemaining = 1;
            return true;
        }
    }
}
