using System.Collections.Generic;

namespace Blastlands.Core
{
    public static class ClassicItems
    {
        public const int SlideTicksPerTile = 3;
        public const int CurseSeconds = 10;
        public const int RemoteCooldownTicks = 6;

        public static bool Apply(MatchState state, PlayerState player, PowerUpKind kind)
        {
            switch (kind)
            {
                case PowerUpKind.Kick:
                    player.CanKick = true;
                    return true;

                case PowerUpKind.Remote:
                    player.HasRemote = true;
                    return true;

                case PowerUpKind.Skull:
                    player.Curse = (CurseKind)(1 + state.Random.NextInt((int)CurseKind.Dry));
                    player.CurseTicksRemaining = state.Settings.TicksPerSecond * CurseSeconds;
                    return true;

                default:
                    return false;
            }
        }

        public static IReadOnlyList<PlayerInput> Cursed(MatchState state, IReadOnlyList<PlayerInput> inputs)
        {
            PlayerInput[] changed = null;
            for (int i = 0; i < state.Players.Count && i < inputs.Count; i++)
            {
                PlayerState player = state.Players[i];
                if (player.Curse == CurseKind.None || !player.Alive)
                {
                    continue;
                }

                changed = changed ?? Copy(inputs);
                changed[i] = Cursed(player.Curse, inputs[i]);
            }

            return changed ?? inputs;
        }

        public static PlayerInput Cursed(CurseKind curse, PlayerInput input)
        {
            switch (curse)
            {
                case CurseKind.Reversed:
                    return new PlayerInput(-input.MoveX, -input.MoveY, input.DropBomb, input.Dash, input.Push, input.Ability);
                case CurseKind.Leaky:
                    return new PlayerInput(input.MoveX, input.MoveY, true, input.Dash, input.Push, input.Ability);
                case CurseKind.Dry:
                    return new PlayerInput(input.MoveX, input.MoveY, false, input.Dash, input.Push, input.Ability);
                default:
                    return input;
            }
        }

        public static int Speed(PlayerState player, MatchSettings settings)
        {
            switch (player.Curse)
            {
                case CurseKind.Slow:
                    return System.Math.Max(1, settings.SpeedFor(0) / 2);
                case CurseKind.Hasty:
                    return settings.SpeedFor(settings.MaxSpeedSteps) * 3 / 2;
                default:
                    return settings.SpeedFor(player.SpeedSteps);
            }
        }

        public static void TickCurses(MatchState state)
        {
            foreach (PlayerState player in state.Players)
            {
                if (player.CurseTicksRemaining > 0 && --player.CurseTicksRemaining == 0)
                {
                    player.Curse = CurseKind.None;
                }
            }
        }

        public static void Kick(MatchState state, PlayerState player)
        {
            if (!player.CanKick)
            {
                return;
            }

            GridPos step = Directions.Delta(player.Facing);
            if (step.X == 0 && step.Y == 0)
            {
                return;
            }

            int radius = state.Settings.PlayerRadius;
            GridPos at = player.Tile;
            GridPos ahead = new GridPos(at.X + step.X, at.Y + step.Y);
            if (PlayerBody.Covers(player.Position, radius, ahead))
            {
                return;
            }

            int gap;
            if (step.X > 0)
            {
                gap = (ahead.X * SubPos.UnitsPerTile) - (player.Position.X + radius);
            }
            else if (step.X < 0)
            {
                gap = (player.Position.X - radius) - ((ahead.X + 1) * SubPos.UnitsPerTile);
            }
            else if (step.Y > 0)
            {
                gap = (ahead.Y * SubPos.UnitsPerTile) - (player.Position.Y + radius);
            }
            else
            {
                gap = (player.Position.Y - radius) - ((ahead.Y + 1) * SubPos.UnitsPerTile);
            }

            if (gap > 1)
            {
                return;
            }

            for (int i = 0; i < state.Bombs.Count; i++)
            {
                ActiveBomb bomb = state.Bombs[i];
                if (bomb.Position == ahead && bomb.Sliding == Direction.None
                    && Free(state, new GridPos(ahead.X + step.X, ahead.Y + step.Y)))
                {
                    bomb.Sliding = player.Facing;
                    bomb.SlideCountdown = SlideTicksPerTile;
                    return;
                }
            }
        }

        public static void SlideBombs(MatchState state)
        {
            for (int i = 0; i < state.Bombs.Count; i++)
            {
                ActiveBomb bomb = state.Bombs[i];
                if (bomb.Sliding == Direction.None || --bomb.SlideCountdown > 0)
                {
                    continue;
                }

                GridPos step = Directions.Delta(bomb.Sliding);
                var next = new GridPos(bomb.Position.X + step.X, bomb.Position.Y + step.Y);
                if (!Free(state, next))
                {
                    bomb.Sliding = Direction.None;
                    continue;
                }

                bomb.MoveTo(next);
                bomb.SlideCountdown = SlideTicksPerTile;
                if (state.HasFlameAt(next))
                {
                    bomb.Remote = false;
                    bomb.FuseRemaining = System.Math.Min(bomb.FuseRemaining, 1);
                }
            }
        }

        public static void Detonate(MatchState state, IReadOnlyList<PlayerInput> inputs)
        {
            for (int i = 0; i < state.Players.Count && i < inputs.Count; i++)
            {
                PlayerState player = state.Players[i];
                if (!player.Alive || !player.HasRemote || !inputs[i].Ability || player.AbilityCooldownRemaining > 0)
                {
                    continue;
                }

                int oldest = OldestRemoteBomb(state, player.Id);
                if (oldest < 0)
                {
                    continue;
                }

                state.Bombs[oldest].Remote = false;
                state.Bombs[oldest].FuseRemaining = 1;
                player.AbilityCooldownRemaining = RemoteCooldownTicks;
            }
        }

        public static int OldestRemoteBomb(MatchState state, int playerId)
        {
            for (int i = 0; i < state.Bombs.Count; i++)
            {
                if (state.Bombs[i].Remote && state.Bombs[i].Bomb.OwnerId == playerId)
                {
                    return i;
                }
            }

            return -1;
        }

        public static bool FuseBurns(MatchState state, ActiveBomb bomb)
        {
            if (!bomb.Remote)
            {
                return true;
            }

            int owner = bomb.Bomb.OwnerId;
            if (owner < 0 || owner >= state.Players.Count)
            {
                return true;
            }

            PlayerState holder = state.Players[owner];
            return !holder.Alive || holder.IsBot;
        }

        private static bool Free(MatchState state, GridPos tile)
        {
            if (!state.Arena.Contains(tile) || Tiles.BlocksMovement(state.Arena[tile]) || state.HasBombAt(tile))
            {
                return false;
            }

            foreach (PlayerState player in state.Players)
            {
                if (player.Alive && PlayerBody.Covers(player.Position, state.Settings.PlayerRadius, tile))
                {
                    return false;
                }
            }

            return true;
        }

        private static PlayerInput[] Copy(IReadOnlyList<PlayerInput> inputs)
        {
            var copy = new PlayerInput[inputs.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = inputs[i];
            }

            return copy;
        }
    }
}
