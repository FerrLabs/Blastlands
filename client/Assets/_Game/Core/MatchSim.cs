using System;
using System.Collections.Generic;

namespace Blastlands.Core
{
    public static class MatchSim
    {
        // A bomb set off by fire rather than placed by anyone.
        private const int NoOwner = -1;

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
            CollectPowerUps(state);
            CollectLooseBombs(state);
            DropBombs(state, inputs);
            ExpireFlames(state);
            DetonateDueBombs(state);
            BurnPowerUps(state);
            IgniteLooseBombs(state);
            KillPlayersInFlames(state);
            ResolveOutcome(state);
            BombSpawner.Tick(state);

            // After the kill check, so a wall never shoves someone out of a blast that
            // was about to take them — the arena does not get to save anyone either.
            WallRegrower.Tick(state);

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

                PlayerInput input = i < inputs.Count ? inputs[i] : PlayerInput.None;
                StartDash(state, player, input);

                if (player.DashCooldownRemaining > 0)
                {
                    player.DashCooldownRemaining--;
                }

                if (player.Dashing)
                {
                    player.DashTicksRemaining--;
                    Move(state, player, player.DashDirection, state.Settings.DashSpeed);
                    continue;
                }

                if (input.Move != Direction.None)
                {
                    Move(state, player, input.Move, state.Settings.SpeedFor(player.SpeedSteps));
                }
            }
        }

        private static void StartDash(MatchState state, PlayerState player, PlayerInput input)
        {
            if (!input.Dash || !player.CanDash)
            {
                return;
            }

            // Nowhere to go is not a dash. Standing still and tapping it would otherwise
            // spend the cooldown on nothing, which reads as the button being broken.
            Direction into = input.Move != Direction.None ? input.Move : player.Facing;
            if (into == Direction.None)
            {
                return;
            }

            player.DashDirection = into;
            player.DashTicksRemaining = state.Settings.DashTicks;
            player.DashCooldownRemaining = state.Settings.DashCooldownTicks + state.Settings.DashTicks;
        }

        // Movement is axis-aligned. Moving along one axis also pulls the player onto
        // the centre of their corridor on the other axis: without that they snag on
        // every pillar and the game feels broken.
        private static void Move(MatchState state, PlayerState player, Direction direction, int speed)
        {
            player.Facing = direction;

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
                player.BombsHeld--;
            }
        }

        private static void CollectPowerUps(MatchState state)
        {
            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                if (!player.Alive)
                {
                    continue;
                }

                int index = state.PowerUpIndexAt(player.Tile);
                if (index < 0)
                {
                    continue;
                }

                Apply(state, player, state.PowerUps[index].Kind);
                state.RemovePowerUpAt(index);
            }
        }

        private static void CollectLooseBombs(MatchState state)
        {
            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                if (!player.Alive || !player.CanCarryMore)
                {
                    continue;
                }

                int index = state.LooseBombIndexAt(player.Tile);
                if (index < 0)
                {
                    continue;
                }

                player.BombsHeld++;
                state.RemoveLooseBombAt(index);
            }
        }

        // A bomb in a fire goes off. Anything else would have players sheltering behind
        // a pile of explosives, and it makes a stocked corner of the arena worth a shot
        // from a distance.
        private static void IgniteLooseBombs(MatchState state)
        {
            for (int i = state.LooseBombs.Count - 1; i >= 0; i--)
            {
                GridPos tile = state.LooseBombs[i];
                if (!state.HasFlameAt(tile) || state.HasBombAt(tile))
                {
                    continue;
                }

                state.RemoveLooseBombAt(i);
                state.AddBomb(new ActiveBomb(
                    new Bomb(tile, NoOwner, state.Settings.StartingFireRange, BombKind.Standard), 1));
            }
        }

        private static void Apply(MatchState state, PlayerState player, PowerUpKind kind)
        {
            switch (kind)
            {
                case PowerUpKind.BombUp:
                    if (player.CarryCapacity < state.Settings.MaxCarryCapacity)
                    {
                        player.CarryCapacity++;
                    }

                    break;

                case PowerUpKind.FireUp:
                    if (player.FireRange < state.Settings.MaxFireRange)
                    {
                        player.FireRange++;
                    }

                    break;

                case PowerUpKind.SpeedUp:
                    if (player.SpeedSteps < state.Settings.MaxSpeedSteps)
                    {
                        player.SpeedSteps++;
                    }

                    break;

                case PowerUpKind.PierceBomb:
                    player.NextBombKind = BombKind.Pierce;
                    break;

                case PowerUpKind.ClusterBomb:
                    player.NextBombKind = BombKind.Cluster;
                    break;
            }
        }

        // A later blast destroys a pickup, but the blast that uncovered it does not.
        // Flames last longer than a tick, so the test is which fire is burning, not
        // whether the tile is on fire: the flame that revealed it was lit no later
        // than the pickup appeared.
        private static void BurnPowerUps(MatchState state)
        {
            for (int i = state.PowerUps.Count - 1; i >= 0; i--)
            {
                PowerUp pickup = state.PowerUps[i];

                for (int f = 0; f < state.Flames.Count; f++)
                {
                    ActiveFlame flame = state.Flames[f];
                    if (flame.Tile == pickup.Tile && flame.SpawnedTick > pickup.RevealedTick)
                    {
                        state.RemovePowerUpAt(i);
                        break;
                    }
                }
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
                GridPos cleared = result.DestroyedSoftBlocks[i];
                state.Arena[cleared] = TileKind.Floor;
                state.ScheduleRegrowth(cleared, state.Settings.WallRegrowTicks);

                PowerUpKind revealed;
                state.TryRevealPowerUp(cleared, out revealed);
            }

            for (int i = 0; i < result.FlameTiles.Count; i++)
            {
                state.AddFlame(result.FlameTiles[i], state.Settings.FlameTicks, state.Tick);
            }

            var detonated = new List<int>(result.DetonatedBombs);
            detonated.Sort();

            // Nothing is handed back to the owner: the bomb was spent when it was
            // placed, and getting another one means finding one.
            for (int i = detonated.Count - 1; i >= 0; i--)
            {
                state.RemoveBombAt(detonated[i]);
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
