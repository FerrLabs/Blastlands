using System.Collections.Generic;

namespace Blastlands.Core
{
    public static class Abilities
    {
        public const int NoBomb = -1;

        public static bool Has(CharacterKind character)
        {
            return character != CharacterKind.None;
        }

        public static void Resolve(MatchState state, IReadOnlyList<PlayerInput> inputs)
        {
            Crumble(state);

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

        public static bool CanUseNow(MatchState state, PlayerState player)
        {
            if (!player.CanUseAbility || !state.Settings.Rules.AllowsCharacters)
            {
                return false;
            }

            switch (player.Character)
            {
                case CharacterKind.Demolisher:
                    return OldestLiveBomb(state, player.Id) != NoBomb;
                case CharacterKind.Runner:
                    return true;
                case CharacterKind.Grenadier:
                    return player.CanDropBomb && TryLanding(state, player, out _);
                default:
                    return false;
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
                case CharacterKind.Grenadier:
                    return Throw(state, player);
                case CharacterKind.Sapper:
                    return RaiseWall(state, player);
                default:
                    return false;
            }
        }

        public static bool TryWallTile(MatchState state, PlayerState player, out GridPos tile)
        {
            GridPos step = Directions.Delta(player.Facing);
            tile = player.Tile.Offset(step.X, step.Y);
            if ((step.X == 0 && step.Y == 0)
                || !state.Arena.Contains(tile)
                || state.Arena[tile] != TileKind.Floor
                || state.HasBombAt(tile)
                || state.HasFlameAt(tile)
                || state.PowerUpIndexAt(tile) >= 0
                || state.LooseBombIndexAt(tile) >= 0
                || state.IsRegrowing(tile))
            {
                return false;
            }

            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState other = state.Players[i];
                if (other.Alive && PlayerBody.Covers(other.Position, state.Settings.PlayerRadius, tile))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool RaiseWall(MatchState state, PlayerState player)
        {
            if (!TryWallTile(state, player, out GridPos tile))
            {
                return false;
            }

            state.Arena[tile] = TileKind.SoftBlock;
            state.AddRaisedWall(new RaisedWall(tile, state.Settings.Abilities.WallTicks));
            return true;
        }

        private static void Crumble(MatchState state)
        {
            for (int i = state.RaisedWalls.Count - 1; i >= 0; i--)
            {
                RaisedWall wall = state.RaisedWalls[i];
                wall.TicksRemaining--;
                if (wall.TicksRemaining > 0)
                {
                    continue;
                }

                if (state.Arena[wall.Tile] == TileKind.SoftBlock)
                {
                    state.Arena[wall.Tile] = TileKind.Floor;
                }

                state.RemoveRaisedWallAt(i);
            }
        }

        public static bool TryLanding(MatchState state, PlayerState player, out GridPos landing)
        {
            landing = player.Tile;
            GridPos step = Directions.Delta(player.Facing);
            if (step.X == 0 && step.Y == 0)
            {
                return false;
            }

            for (int distance = 1; distance <= state.Settings.Abilities.ThrowRange; distance++)
            {
                GridPos next = player.Tile.Offset(step.X * distance, step.Y * distance);
                if (!state.Arena.Contains(next) || !Tiles.CanBeStoodOn(state.Arena[next]) || state.HasBombAt(next))
                {
                    break;
                }

                landing = next;
            }

            return landing != player.Tile;
        }

        private static bool Throw(MatchState state, PlayerState player)
        {
            if (!player.CanDropBomb || !TryLanding(state, player, out GridPos landing))
            {
                return false;
            }

            var bomb = new Bomb(landing, player.Id, player.FireRange, player.NextBombKind);
            state.AddBomb(new ActiveBomb(bomb, state.Settings.FuseTicks));
            player.BombsHeld--;
            return true;
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
