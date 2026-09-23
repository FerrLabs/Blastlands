using System.Collections.Generic;

namespace Blastlands.Core
{
    public static class BotAbilities
    {
        public const int VanishWithin = 3;

        public static bool ShouldUse(MatchState state, PlayerState bot)
        {
            if (!state.Settings.Rules.AllowsCharacters || !bot.CanUseAbility)
            {
                return false;
            }

            switch (bot.Character)
            {
                case CharacterKind.Demolisher:
                    return TriggerCatchesARival(state, bot);
                case CharacterKind.Runner:
                    return !Vision.IsHidden(state, bot) && ARivalIsClose(state, bot);
                default:
                    return false;
            }
        }

        private static bool ARivalIsClose(MatchState state, PlayerState bot)
        {
            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState other = state.Players[i];
                if (other.Id == bot.Id || !Vision.CanSee(state, bot, other))
                {
                    continue;
                }

                int dx = System.Math.Abs(other.Tile.X - bot.Tile.X);
                int dy = System.Math.Abs(other.Tile.Y - bot.Tile.Y);
                if (dx + dy <= VanishWithin)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TriggerCatchesARival(MatchState state, PlayerState bot)
        {
            int oldest = Abilities.OldestLiveBomb(state, bot.Id);
            if (oldest == Abilities.NoBomb)
            {
                return false;
            }

            var bombs = new List<Bomb>(state.Bombs.Count);
            for (int i = 0; i < state.Bombs.Count; i++)
            {
                bombs.Add(state.Bombs[i].Bomb);
            }

            var burning = new HashSet<GridPos>(
                ExplosionResolver.Resolve(state.Arena, bombs, new[] { oldest }).FlameTiles);
            if (burning.Contains(bot.Tile))
            {
                return false;
            }

            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState other = state.Players[i];
                if (other.Id != bot.Id && burning.Contains(other.Tile) && Vision.CanSee(state, bot, other))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
