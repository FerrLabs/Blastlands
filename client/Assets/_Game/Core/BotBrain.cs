using System.Collections.Generic;

namespace Blastlands.Core
{
    // Server-side AI producing the same PlayerInput a human socket does, so the
    // simulation cannot tell the two apart. Deterministic: no clock, no unseeded
    // randomness, directions always walked in the same order.
    public sealed class BotBrain
    {
        private static readonly Direction[] Order =
        {
            Direction.Right,
            Direction.Down,
            Direction.Left,
            Direction.Up
        };

        private readonly int playerId;
        private readonly BotSettings settings;

        private int cooldown;
        private Direction heading;
        private Direction plannedEscape;

        public BotBrain(int playerId, BotSettings settings)
        {
            this.playerId = playerId;
            this.settings = settings;
        }

        public PlayerInput Think(MatchState state)
        {
            PlayerState player = FindPlayer(state);
            if (player == null || !player.Alive)
            {
                return PlayerInput.None;
            }

            // Between decisions the bot keeps walking the way it was, which reads as
            // hesitation rather than as a freeze. The bomb is never repeated: it would
            // otherwise fire again on every tick of the delay.
            if (cooldown > 0)
            {
                cooldown--;
                return PlayerInput.Moving(heading);
            }

            cooldown = settings.ReactionTicks;
            PlayerInput decision = Decide(state, player);

            // Dropping carries no direction, so taking the heading from it would leave
            // the bot standing on its own bomb for the whole reaction delay. It leaves
            // along the route the escape check already proved was open.
            heading = decision.DropBomb ? plannedEscape : decision.Move;
            return decision;
        }

        private PlayerInput Decide(MatchState state, PlayerState player)
        {
            BlastMap blast = BlastMap.From(state);
            GridPos tile = player.Tile;
            int ticksPerTile = TicksPerTile(state, player);

            // Nothing else matters while standing somewhere about to burn.
            if (!blast.IsSafeFor(tile, settings.LookaheadTicks))
            {
                return PlayerInput.Moving(StepToSafety(state, blast, tile, ticksPerTile));
            }

            if (player.CanDropBomb
                && !state.HasBombAt(tile)
                && (TouchesSoftBlock(state, tile) || EnemyInBlastLine(state, player, tile)))
            {
                Direction escape = EscapeAfterBombing(state, player, tile, ticksPerTile);
                if (escape != Direction.None)
                {
                    plannedEscape = escape;
                    return PlayerInput.Dropping();
                }
            }

            return PlayerInput.Moving(StepTowardTarget(state, blast, player, tile, ticksPerTile));
        }

        private PlayerState FindPlayer(MatchState state)
        {
            for (int i = 0; i < state.Players.Count; i++)
            {
                if (state.Players[i].Id == playerId)
                {
                    return state.Players[i];
                }
            }

            return null;
        }

        private static int TicksPerTile(MatchState state, PlayerState player)
        {
            int speed = state.Settings.SpeedFor(player.SpeedSteps);
            return speed <= 0 ? SubPos.UnitsPerTile : ((SubPos.UnitsPerTile + speed - 1) / speed);
        }

        // Fleeing to a tile that merely burns later is what gets a bot cornered: it
        // outruns one blast into the next one, and each hop has fewer ways out than the
        // last. Aim for ground the current bombs cannot reach at all, and settle for
        // buying time only when there is none.
        private Direction StepToSafety(MatchState state, BlastMap blast, GridPos from, int ticksPerTile)
        {
            Direction clear = FirstStepToward(
                state,
                from,
                (tile, depth) => blast.TicksUntilFire(tile) == BlastMap.Never,
                (tile, depth) => blast.SurvivesArrival(tile, depth * ticksPerTile, 0));

            if (clear != Direction.None)
            {
                return clear;
            }

            return FirstStepToward(
                state,
                from,
                (tile, depth) => blast.SurvivesArrival(tile, depth * ticksPerTile, settings.SafetyMarginTicks),
                (tile, depth) => blast.SurvivesArrival(tile, depth * ticksPerTile, 0));
        }

        private Direction StepTowardTarget(
            MatchState state, BlastMap blast, PlayerState player, GridPos from, int ticksPerTile)
        {
            return FirstStepToward(
                state,
                from,
                (tile, depth) => blast.SurvivesArrival(tile, depth * ticksPerTile, settings.SafetyMarginTicks)
                                 && (TouchesSoftBlock(state, tile) || HoldsEnemy(state, player.Id, tile)),
                (tile, depth) => blast.SurvivesArrival(tile, depth * ticksPerTile, 0));
        }

        // The check that stops a bot killing itself: place the bomb it is considering,
        // recompute the danger it would create, and return the way out, or None when
        // there is not one.
        private Direction EscapeAfterBombing(MatchState state, PlayerState player, GridPos tile, int ticksPerTile)
        {
            var hypothetical = new List<ActiveBomb>(state.Bombs.Count + 1);
            for (int i = 0; i < state.Bombs.Count; i++)
            {
                hypothetical.Add(state.Bombs[i]);
            }

            hypothetical.Add(new ActiveBomb(
                new Bomb(tile, player.Id, player.FireRange, player.NextBombKind),
                state.Settings.FuseTicks));

            BlastMap after = BlastMap.From(state, hypothetical);

            return StepToSafety(state, after, tile, ticksPerTile);
        }

        private static bool TouchesSoftBlock(MatchState state, GridPos tile)
        {
            for (int i = 0; i < Order.Length; i++)
            {
                GridPos delta = Directions.Delta(Order[i]);
                GridPos next = tile.Offset(delta.X, delta.Y);
                if (state.Arena.Contains(next) && state.Arena[next] == TileKind.SoftBlock)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HoldsEnemy(MatchState state, int selfId, GridPos tile)
        {
            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState other = state.Players[i];
                if (other.Id != selfId && other.Alive && other.Tile == tile)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool EnemyInBlastLine(MatchState state, PlayerState player, GridPos from)
        {
            for (int d = 0; d < Order.Length; d++)
            {
                GridPos delta = Directions.Delta(Order[d]);

                for (int step = 1; step <= player.FireRange; step++)
                {
                    GridPos tile = from.Offset(delta.X * step, delta.Y * step);
                    if (!state.Arena.Contains(tile) || state.Arena[tile] == TileKind.HardBlock)
                    {
                        break;
                    }

                    if (HoldsEnemy(state, player.Id, tile))
                    {
                        return true;
                    }

                    if (state.Arena[tile] == TileKind.SoftBlock)
                    {
                        break;
                    }
                }
            }

            return false;
        }

        private static bool Walkable(MatchState state, GridPos tile)
        {
            return state.Arena.Contains(tile)
                && state.Arena[tile] == TileKind.Floor
                && !state.HasBombAt(tile);
        }

        private struct Step
        {
            public GridPos Tile;
            public int Depth;
            public Direction First;
        }

        // Breadth-first from `from`, returning the first move of the shortest route to
        // a tile satisfying `accept`. `canPass` keeps the route out of tiles that will
        // already be burning by the time the bot walks through them.
        private static Direction FirstStepToward(
            MatchState state,
            GridPos from,
            System.Func<GridPos, int, bool> accept,
            System.Func<GridPos, int, bool> canPass)
        {
            var seen = new HashSet<GridPos> { from };
            var queue = new Queue<Step>();

            for (int i = 0; i < Order.Length; i++)
            {
                GridPos delta = Directions.Delta(Order[i]);
                GridPos next = from.Offset(delta.X, delta.Y);

                if (Walkable(state, next) && seen.Add(next) && canPass(next, 1))
                {
                    queue.Enqueue(new Step { Tile = next, Depth = 1, First = Order[i] });
                }
            }

            while (queue.Count > 0)
            {
                Step current = queue.Dequeue();

                if (accept(current.Tile, current.Depth))
                {
                    return current.First;
                }

                for (int i = 0; i < Order.Length; i++)
                {
                    GridPos delta = Directions.Delta(Order[i]);
                    GridPos next = current.Tile.Offset(delta.X, delta.Y);

                    if (Walkable(state, next) && seen.Add(next) && canPass(next, current.Depth + 1))
                    {
                        queue.Enqueue(new Step { Tile = next, Depth = current.Depth + 1, First = current.First });
                    }
                }
            }

            return Direction.None;
        }
    }
}
