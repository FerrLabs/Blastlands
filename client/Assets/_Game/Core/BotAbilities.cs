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
                case CharacterKind.Grenadier:
                    return ThrowCatchesARival(state, bot);
                case CharacterKind.Sapper:
                    return WallShields(state, bot);
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
            return oldest != Abilities.NoBomb && CatchesARival(state, bot, BombsOnBoard(state), oldest);
        }

        private static bool ThrowCatchesARival(MatchState state, PlayerState bot)
        {
            if (!bot.CanDropBomb || !Abilities.TryLanding(state, bot, out GridPos landing))
            {
                return false;
            }

            List<Bomb> bombs = BombsOnBoard(state);
            bombs.Add(new Bomb(landing, bot.Id, bot.FireRange, bot.NextBombKind));
            return CatchesARival(state, bot, bombs, bombs.Count - 1);
        }

        private static bool WallShields(MatchState state, PlayerState bot)
        {
            if (state.HasFlameAt(bot.Tile) || !Abilities.TryWallTile(state, bot, out GridPos wall))
            {
                return false;
            }

            List<Bomb> bombs = BombsOnBoard(state);
            if (!Burns(state.Arena, bombs, bot.Tile))
            {
                return false;
            }

            return !Burns(WithWall(state.Arena, wall), bombs, bot.Tile);
        }

        private static bool Burns(Arena arena, List<Bomb> bombs, GridPos tile)
        {
            var trigger = new int[1];
            for (int i = 0; i < bombs.Count; i++)
            {
                trigger[0] = i;
                IReadOnlyList<GridPos> flames = ExplosionResolver.Resolve(arena, bombs, trigger).FlameTiles;
                for (int f = 0; f < flames.Count; f++)
                {
                    if (flames[f] == tile)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Arena WithWall(Arena arena, GridPos wall)
        {
            var copy = new Arena(arena.Width, arena.Height);
            for (int y = 0; y < arena.Height; y++)
            {
                for (int x = 0; x < arena.Width; x++)
                {
                    var tile = new GridPos(x, y);
                    copy[tile] = arena[tile];
                }
            }

            copy[wall] = TileKind.SoftBlock;
            return copy;
        }

        private static List<Bomb> BombsOnBoard(MatchState state)
        {
            var bombs = new List<Bomb>(state.Bombs.Count + 1);
            for (int i = 0; i < state.Bombs.Count; i++)
            {
                bombs.Add(state.Bombs[i].Bomb);
            }

            return bombs;
        }

        private static bool CatchesARival(MatchState state, PlayerState bot, List<Bomb> bombs, int triggered)
        {
            var burning = new HashSet<GridPos>(
                ExplosionResolver.Resolve(state.Arena, bombs, new[] { triggered }).FlameTiles);
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
