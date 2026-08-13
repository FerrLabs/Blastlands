using System;
using System.Collections.Generic;

namespace Blastlands.Core
{
    public static class MatchSim
    {
        public static void Tick(MatchState state, IReadOnlyList<PlayerInput> inputs)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (inputs == null)
            {
                throw new ArgumentNullException(nameof(inputs));
            }

            if (state.Outcome != RoundOutcome.Running)
            {
                return;
            }

            MovePlayers(state, inputs);
            DropBombs(state, inputs);
            ExpireFlames(state);
            DetonateDueBombs(state);
            KillPlayersInFlames(state);
            ResolveOutcome(state);

            state.Tick++;
        }

        private static void MovePlayers(MatchState state, IReadOnlyList<PlayerInput> inputs)
        {
            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                if (!player.Alive)
                {
                    continue;
                }

                Direction move = i < inputs.Count ? inputs[i].Move : Direction.None;
                if (move != Direction.None)
                {
                    Move(state, player, move);
                }
            }
        }

        // Movement is axis-aligned. Moving along one axis also pulls the player onto
        // the centre of their corridor on the other axis: without that they snag on
        // every pillar and the game feels broken.
        private static void Move(MatchState state, PlayerState player, Direction direction)
        {
            player.Facing = direction;

            int speed = state.Settings.SpeedFor(player.SpeedSteps);
            GridPos delta = Directions.Delta(direction);
            GridPos tile = player.Tile;
            SubPos position = player.Position;

            if (Directions.IsHorizontal(direction))
            {
                position = position.WithY(StepToward(position.Y, SubPos.CentreOf(tile.Y), speed));

                int target = position.X + (delta.X * speed);
                if (!CanEnter(state, new GridPos(tile.X + delta.X, tile.Y)))
                {
                    int limit = SubPos.CentreOf(tile.X);
                    target = delta.X > 0 ? Math.Min(target, limit) : Math.Max(target, limit);
                }

                position = position.WithX(target);
            }
            else
            {
                position = position.WithX(StepToward(position.X, SubPos.CentreOf(tile.X), speed));

                int target = position.Y + (delta.Y * speed);
                if (!CanEnter(state, new GridPos(tile.X, tile.Y + delta.Y)))
                {
                    int limit = SubPos.CentreOf(tile.Y);
                    target = delta.Y > 0 ? Math.Min(target, limit) : Math.Max(target, limit);
                }

                position = position.WithY(target);
            }

            player.Position = position;
        }

        // Only ever asked about a tile the player is entering, never the one they are
        // standing on, which is what lets a player step off their own bomb.
        private static bool CanEnter(MatchState state, GridPos tile)
        {
            return state.Arena.Contains(tile)
                && state.Arena[tile] == TileKind.Floor
                && !state.HasBombAt(tile);
        }

        private static int StepToward(int value, int target, int step)
        {
            if (value == target)
            {
                return value;
            }

            int difference = target - value;
            return difference > 0
                ? value + Math.Min(step, difference)
                : value - Math.Min(step, -difference);
        }

        private static void DropBombs(MatchState state, IReadOnlyList<PlayerInput> inputs)
        {
            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                bool wants = i < inputs.Count && inputs[i].DropBomb;
                if (!wants || !player.CanDropBomb || state.HasBombAt(player.Tile))
                {
                    continue;
                }

                var bomb = new Bomb(player.Tile, player.Id, player.FireRange, player.NextBombKind);
                state.AddBomb(new ActiveBomb(bomb, state.Settings.FuseTicks));
                player.BombsPlaced++;
            }
        }

        private static void ExpireFlames(MatchState state)
        {
            for (int i = state.Flames.Count - 1; i >= 0; i--)
            {
                ActiveFlame flame = state.Flames[i];
                flame.TicksRemaining--;
                if (flame.TicksRemaining <= 0)
                {
                    state.RemoveFlameAt(i);
                }
            }
        }

        private static void DetonateDueBombs(MatchState state)
        {
            var triggered = new List<int>();
            var definitions = new List<Bomb>(state.Bombs.Count);

            for (int i = 0; i < state.Bombs.Count; i++)
            {
                ActiveBomb bomb = state.Bombs[i];
                bomb.FuseRemaining--;
                definitions.Add(bomb.Bomb);

                if (bomb.FuseRemaining <= 0)
                {
                    triggered.Add(i);
                }
            }

            if (triggered.Count == 0)
            {
                return;
            }

            ExplosionResult result = ExplosionResolver.Resolve(state.Arena, definitions, triggered);

            for (int i = 0; i < result.DestroyedSoftBlocks.Count; i++)
            {
                state.Arena[result.DestroyedSoftBlocks[i]] = TileKind.Floor;
            }

            for (int i = 0; i < result.FlameTiles.Count; i++)
            {
                state.AddFlame(result.FlameTiles[i], state.Settings.FlameTicks);
            }

            var detonated = new List<int>(result.DetonatedBombs);
            detonated.Sort();

            for (int i = detonated.Count - 1; i >= 0; i--)
            {
                int index = detonated[i];
                ReturnCapacity(state, state.Bombs[index].Bomb.OwnerId);
                state.RemoveBombAt(index);
            }
        }

        private static void ReturnCapacity(MatchState state, int ownerId)
        {
            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                if (player.Id == ownerId && player.BombsPlaced > 0)
                {
                    player.BombsPlaced--;
                    return;
                }
            }
        }

        // Every player is checked against the same flame set, so two players who blow
        // each other up on the same tick both die rather than the loop order deciding.
        private static void KillPlayersInFlames(MatchState state)
        {
            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                if (player.Alive && state.HasFlameAt(player.Tile))
                {
                    player.Alive = false;
                }
            }
        }

        private static void ResolveOutcome(MatchState state)
        {
            if (state.Players.Count < 2)
            {
                return;
            }

            int alive = state.AliveCount;
            if (alive == 0)
            {
                state.Outcome = RoundOutcome.Draw;
                state.WinnerId = -1;
                return;
            }

            if (alive > 1)
            {
                return;
            }

            for (int i = 0; i < state.Players.Count; i++)
            {
                if (state.Players[i].Alive)
                {
                    state.Outcome = RoundOutcome.Winner;
                    state.WinnerId = state.Players[i].Id;
                    return;
                }
            }
        }
    }
}
