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
        private readonly List<Sighting> sightings = new List<Sighting>();

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

            // Looking is not deciding, so it happens on every tick rather than on the
            // reaction cadence. The delay is a handicap on how fast the bot acts, and
            // making it a handicap on its eyes as well would mean an easy bot walks past
            // opponents it was staring at.
            Observe(state, player);

            // Between decisions the bot keeps walking the way it was, which reads as
            // hesitation rather than as a freeze. The bomb is never repeated: it would
            // otherwise fire again on every tick of the delay.
            if (cooldown > 0)
            {
                cooldown--;
                return Steer(player, heading, false);
            }

            cooldown = settings.ReactionTicks;
            PlayerInput decision = Decide(state, player);

            // Dropping carries no direction, so taking the heading from it would leave
            // the bot standing on its own bomb for the whole reaction delay. It leaves
            // along the route the escape check already proved was open.
            heading = decision.DropBomb ? plannedEscape : decision.Move;
            return decision;
        }

        // The route is a sequence of tiles, so the bot aims at the middle of the next
        // one rather than leaning on a compass point. Free positions removed the
        // re-centring that used to do this for free, and without it a bot drifts off the
        // lane and grinds along corners: it kept moving, but it stopped clearing the
        // arena — 34 blocks a match down to 10.
        private static PlayerInput Steer(PlayerState player, Direction direction, bool dash)
        {
            if (direction == Direction.None)
            {
                return PlayerInput.None;
            }

            // A dash commits to the dominant axis of whatever vector it is given, so a
            // steering vector aimed at a tile centre can send it off at right angles to
            // the escape it was meant to take. Dashes go out as a clean cardinal.
            if (dash)
            {
                return PlayerInput.Dashing(direction);
            }

            GridPos step = Directions.Delta(direction);
            GridPos target = player.Tile.Offset(step.X, step.Y);

            int toX = SubPos.CentreOf(target.X) - player.Position.X;
            int toY = SubPos.CentreOf(target.Y) - player.Position.Y;

            int magnitude = Magnitude(toX, toY);
            if (magnitude <= 0)
            {
                return new PlayerInput(step.X * StickReader.Range, step.Y * StickReader.Range, false, dash);
            }

            return new PlayerInput(
                toX * StickReader.Range / magnitude,
                toY * StickReader.Range / magnitude,
                false,
                dash);
        }

        private static int Magnitude(int x, int y)
        {
            long squared = ((long)x * x) + ((long)y * y);
            if (squared <= 0)
            {
                return 0;
            }

            int root = 0;
            while ((long)(root + 1) * (root + 1) <= squared)
            {
                root++;
            }

            return root;
        }

        private PlayerInput Decide(MatchState state, PlayerState player)
        {
            BlastMap blast = BlastMap.From(state);
            GridPos tile = player.Tile;
            int ticksPerTile = TicksPerTile(state, player);

            // Nothing else matters while standing somewhere about to burn. A dash is
            // worth spending here and nowhere else: it is committed for its whole
            // length, so it only pays when the direction is not going to change.
            if (!blast.IsSafeFor(tile, settings.LookaheadTicks))
            {
                Direction away = Escape(state, blast, player, ticksPerTile);
                bool dash = settings.ReactionTicks > 0 && player.CanDash && away != Direction.None;
                return Steer(player, away, dash);
            }

            // Shoving comes before bombing because it is the only thing here that kills
            // on the tick it happens. A bomb is a threat somebody has two and a half
            // seconds to walk away from, and a bot that has learnt to dodge one will.
            if (ShoveKills(state, player, blast, out Direction shoveInto))
            {
                return PlayerInput.Pushing(shoveInto);
            }

            if (player.CanDropBomb
                && !state.HasBombAt(tile)
                && (WouldTrapSomebody(state, player, tile, ticksPerTile) || TouchesSoftBlock(state, tile)))
            {
                Direction escape = EscapeAfterBombing(state, player, tile, ticksPerTile);
                if (escape != Direction.None)
                {
                    plannedEscape = escape;
                    return PlayerInput.Dropping();
                }
            }

            Direction toward = StepTowardTarget(state, blast, player, tile, ticksPerTile);

            // Standing still is never the safe option, whatever the blast map currently
            // says. A bot with nothing to walk towards that plants itself is a bot
            // waiting for the next chain reaction to find it.
            if (toward == Direction.None)
            {
                toward = StepToSafety(state, blast, tile, ticksPerTile);
            }

            return Steer(player, toward, false);
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


        private Direction Escape(MatchState state, BlastMap blast, PlayerState player, int ticksPerTile)
        {
            if (settings.Planning == BotPlanning.OnArrival)
            {
                return StepToSafety(state, blast, player.Tile, ticksPerTile);
            }

            EscapeWindow window = EscapeWindow.From(state, blast, ticksPerTile, next => Walkable(state, next));
            int here = blast.TicksUntilFire(player.Tile);

            Direction best = Direction.None;
            long bestSlack = -1;

            for (int i = 0; i < Order.Length; i++)
            {
                GridPos delta = Directions.Delta(Order[i]);
                GridPos next = player.Tile.Offset(delta.X, delta.Y);
                int latest;
                if (!Walkable(state, next) || !window.TryLatestEntry(next, out latest))
                {
                    continue;
                }

                int arrival = TicksToCross(state, player, Order[i]);
                long slack = (long)latest - arrival;
                if (arrival < here && slack > bestSlack)
                {
                    best = Order[i];
                    bestSlack = slack;
                }
            }

            return best != Direction.None ? best : StepToSafety(state, blast, player.Tile, ticksPerTile);
        }

        private static int TicksToCross(MatchState state, PlayerState player, Direction direction)
        {
            int speed = state.Settings.SpeedFor(player.SpeedSteps);
            int x = WithinTile(player.Position.X);
            int y = WithinTile(player.Position.Y);

            int distance;
            switch (direction)
            {
                case Direction.Right:
                    distance = SubPos.UnitsPerTile - x;
                    break;
                case Direction.Left:
                    distance = x + 1;
                    break;
                case Direction.Down:
                    distance = SubPos.UnitsPerTile - y;
                    break;
                default:
                    distance = y + 1;
                    break;
            }

            // A tick of slack on top. Steer aims at the centre of the next tile, so a
            // bot standing off the lane travels diagonally and covers less along this
            // axis than its speed each tick. Reading the crossing as faster than it is
            // would be fatal here: this feeds the check on whether the bot clears its
            // own tile before the fire arrives.
            return speed <= 0 ? distance : ((distance + speed - 1) / speed) + 1;
        }

        private static int WithinTile(int units)
        {
            int offset = units % SubPos.UnitsPerTile;
            return offset < 0 ? offset + SubPos.UnitsPerTile : offset;
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

        // Routes taken while safe keep the same safety margin the destination does.
        // Tolerating a tile that burns on the way is right when fleeing — anything is
        // better than staying — but walking through one on an errand is how a bot ends
        // up stepping into a fuse, panicking back out, and doing it again until the
        // bomb goes off. Free positions made that visible: a body sitting on a tile
        // boundary flips which tile it is in every tick, and the decision flips with it.
        //
        // Restocking comes first for a bot with nothing to place: it cannot threaten
        // anyone and cannot open a wall, so nothing else it does leads anywhere.
        //
        // It has to be a fallback chain rather than a choice, though. An empty bot with
        // no reachable bomb that returns None stands still, and standing still next to
        // its own fuse is how it dies — which is exactly what happened when this picked
        // one target set instead of trying both.
        private Direction StepTowardTarget(
            MatchState state, BlastMap blast, PlayerState player, GridPos from, int ticksPerTile)
        {
            // Only the level that plans everywhere pays for the window here. The others
            // judge an errand by whether the fire has arrived yet, which is what lets
            // them walk somewhere they cannot leave.
            System.Func<GridPos, int, bool> leavable;
            if (settings.Planning == BotPlanning.Always)
            {
                EscapeWindow window = EscapeWindow.From(state, blast, ticksPerTile, next => Walkable(state, next));
                leavable = (tile, depth) => window.Allows(tile, (depth * ticksPerTile) + settings.SafetyMarginTicks);
            }
            else
            {
                leavable = (tile, depth) =>
                    blast.SurvivesArrival(tile, depth * ticksPerTile, settings.SafetyMarginTicks);
            }

            if (player.CanCarryMore)
            {
                Direction toBomb = FirstStepToward(
                    state,
                    from,
                    (tile, depth) => leavable(tile, depth) && state.LooseBombIndexAt(tile) >= 0,
                    leavable);

                if (toBomb != Direction.None)
                {
                    return toBomb;
                }
            }

            // Hunting outranks opening another wall, and it aims at a tile that threatens
            // the target rather than at the tile the target is standing on.
            //
            // Walking onto somebody achieves nothing: the bot arrives, has no reason to
            // bomb that it did not have a tile earlier, and the two of them stand there.
            // Aiming at firing positions is also what makes the difference measurable at
            // all — a target was previously one acceptable destination among every
            // destructible tile on the board, and there are hundreds of those, all of
            // them closer.
            GridPos target;
            if (player.CanDropBomb && NearestBelief(from, out target))
            {
                Direction toFiringPosition = FirstStepToward(
                    state,
                    from,
                    (tile, depth) => leavable(tile, depth) && Reaches(state.Arena, tile, target, player.FireRange),
                    leavable);

                if (toFiringPosition != Direction.None)
                {
                    return toFiringPosition;
                }
            }

            return FirstStepToward(
                state,
                from,
                (tile, depth) => leavable(tile, depth) && TouchesSoftBlock(state, tile),
                leavable);
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

            // Only ground the hypothetical bombs cannot reach at all counts here, with
            // none of the fallback StepToSafety allows. Settling for a tile that merely
            // burns later is reasonable when you are already in danger and have to pick
            // the least bad option; it is not reasonable when you are choosing to
            // create the danger. Taking the fallback is how a bot bombs its own last
            // exit and stands in the corner waiting.
            return FirstStepToward(
                state,
                tile,
                (candidate, depth) => after.TicksUntilFire(candidate) == BlastMap.Never,
                (candidate, depth) => after.SurvivesArrival(candidate, depth * ticksPerTile, settings.SafetyMarginTicks));
        }

        private static bool TouchesSoftBlock(MatchState state, GridPos tile)
        {
            for (int i = 0; i < Order.Length; i++)
            {
                GridPos delta = Directions.Delta(Order[i]);
                GridPos next = tile.Offset(delta.X, delta.Y);
                if (state.Arena.Contains(next) && Tiles.CanBeDestroyed(state.Arena[next]))
                {
                    return true;
                }
            }

            return false;
        }

        // What the bot last saw of one opponent. A position it is entitled to believe
        // rather than one it knows, which is the whole difference vision makes: the bot
        // hunts where you were, and is wrong about it as often as a player would be.
        private struct Sighting
        {
            public int PlayerId;
            public GridPos Tile;
            public int Tick;
        }

        // Three things happen here, and they are separate on purpose. Anyone in sight is
        // recorded where they stand. Anyone whose remembered tile is now visibly empty is
        // dropped, because a bot that keeps bombing a corner it can see nobody is in
        // looks broken rather than fooled. Everything else simply ages out.
        private void Observe(MatchState state, PlayerState self)
        {
            for (int i = sightings.Count - 1; i >= 0; i--)
            {
                Sighting stale = sightings[i];
                if (state.Tick - stale.Tick > settings.MemoryTicks
                    || Vision.CanSeeItIsEmpty(state, self.Tile, stale.Tile))
                {
                    sightings.RemoveAt(i);
                }
            }

            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState other = state.Players[i];
                if (other.Id == self.Id)
                {
                    continue;
                }

                if (!other.Alive)
                {
                    Forget(other.Id);
                    continue;
                }

                if (Vision.CanSee(state, self, other))
                {
                    Remember(other.Id, other.Tile, state.Tick);
                }
            }
        }

        private void Remember(int id, GridPos tile, int tick)
        {
            for (int i = 0; i < sightings.Count; i++)
            {
                if (sightings[i].PlayerId == id)
                {
                    sightings[i] = new Sighting { PlayerId = id, Tile = tile, Tick = tick };
                    return;
                }
            }

            sightings.Add(new Sighting { PlayerId = id, Tile = tile, Tick = tick });
        }

        private void Forget(int id)
        {
            for (int i = sightings.Count - 1; i >= 0; i--)
            {
                if (sightings[i].PlayerId == id)
                {
                    sightings.RemoveAt(i);
                }
            }
        }

        // The bot's world model, exposed because it is the thing worth asserting about.
        // A decision cannot stand in for it: the planner walks Right when it has nothing
        // to do and towards any destructible tile it can reach, so the same step comes
        // out of "sees nobody" and "sees somebody and had a better idea".
        public bool BelievesOpponentAt(GridPos tile)
        {
            return BelievesEnemyAt(tile);
        }

        private bool BelievesEnemyAt(GridPos tile)
        {
            for (int i = 0; i < sightings.Count; i++)
            {
                if (sightings[i].Tile == tile)
                {
                    return true;
                }
            }

            return false;
        }

        // Whether a bomb dropped here would leave somebody with nowhere to go.
        //
        // Bombing a target merely because they are in range does not kill anyone and was
        // measured not to: a fuse is two and a half seconds and every bot in the game
        // reads a blast map, so they simply walk out. Worse, it spends the bomb, and
        // bombs have to be found on the ground. What kills is the tile that closes the
        // last way out, so that is the only reason to spend one on a person.
        //
        // The bomb's own tile counts as closed. A bomb is solid once it is down, so
        // dropping one in the mouth of a pocket seals whoever is inside it, and that is
        // the only geometry that traps anybody: with a fuse of seventy-five ticks and a
        // radius of two, a target in an open corridor covers seven tiles before it goes
        // off and simply walks out of the blast.
        private bool WouldTrapSomebody(MatchState state, PlayerState player, GridPos tile, int ticksPerTile)
        {
            if (sightings.Count == 0)
            {
                return false;
            }

            BlastMap after = BlastMap.From(state, WithBombAt(state, player, tile));

            for (int i = 0; i < sightings.Count; i++)
            {
                GridPos target = sightings[i].Tile;

                if (after.TicksUntilFire(target) == BlastMap.Never)
                {
                    continue;
                }

                // Paced by how fast the target moves, not by how fast the bot does. They
                // are the one doing the running, and a speed pickup either side of the
                // difference turns a trap into an escape or the other way about.
                int targetTicksPerTile = TicksPerTileFor(state, sightings[i].PlayerId, ticksPerTile);

                // Judged the same way the bot judges its own escapes. On arrival safety
                // alone a target looks like it gets away whenever the fire has not
                // reached the next tile yet, even when that tile is a pocket closing
                // behind it, so the bot talks itself out of bombs that would have
                // landed. The level that plans its own way out plans the victim's too.
                System.Func<GridPos, int, bool> reachable;
                if (settings.Planning == BotPlanning.Always)
                {
                    // The bomb being considered blocks the tile it sits on. Left out,
                    // the victim's window relaxes straight through it and the bot reads
                    // an escape that the bomb it is about to place has already closed.
                    GridPos blocked = tile;
                    EscapeWindow theirs = EscapeWindow.From(
                        state, after, targetTicksPerTile, next => next != blocked && Walkable(state, next));
                    reachable = (candidate, depth) =>
                        candidate != tile && theirs.Allows(candidate, depth * targetTicksPerTile);
                }
                else
                {
                    reachable = (candidate, depth) =>
                        candidate != tile && after.SurvivesArrival(candidate, depth * targetTicksPerTile, 0);
                }

                bool escapes = FirstStepToward(
                    state,
                    target,
                    (candidate, depth) => after.TicksUntilFire(candidate) == BlastMap.Never,
                    reachable)
                    != Direction.None;

                if (!escapes)
                {
                    return true;
                }

                // A bomb that leaves one way out is worth placing too, for the level
                // that can see that far: the target has to guess right first time and
                // cannot double back. Anything looser than one door is not pressure, it
                // is a bomb somebody strolls away from.
                if (settings.Planning == BotPlanning.Always && WaysOut(state, after, target, targetTicksPerTile) <= 1)
                {
                    return true;
                }
            }

            return false;
        }

        private static int WaysOut(MatchState state, BlastMap after, GridPos target, int ticksPerTile)
        {
            int doors = 0;
            for (int i = 0; i < Order.Length; i++)
            {
                GridPos delta = Directions.Delta(Order[i]);
                GridPos next = target.Offset(delta.X, delta.Y);
                if (Walkable(state, next) && after.SurvivesArrival(next, ticksPerTile, 0))
                {
                    doors++;
                }
            }

            return doors;
        }

        private static int TicksPerTileFor(MatchState state, int playerId, int fallback)
        {
            for (int i = 0; i < state.Players.Count; i++)
            {
                if (state.Players[i].Id == playerId)
                {
                    return TicksPerTile(state, state.Players[i]);
                }
            }

            return fallback;
        }

        private static List<ActiveBomb> WithBombAt(MatchState state, PlayerState player, GridPos tile)
        {
            var bombs = new List<ActiveBomb>(state.Bombs.Count + 1);
            for (int i = 0; i < state.Bombs.Count; i++)
            {
                bombs.Add(state.Bombs[i]);
            }

            bombs.Add(new ActiveBomb(
                new Bomb(tile, player.Id, player.FireRange, player.NextBombKind), state.Settings.FuseTicks));

            return bombs;
        }

        // Whether shoving somebody right now puts them somewhere that burns.
        //
        // A shove carries the target several tiles in the direction the shover faces, so
        // the question is not where they are but where they end up. Checked against the
        // blast map that already exists rather than a hypothetical one: the danger has to
        // be on the board before the shove, which is what makes bomb-then-shove a plan
        // rather than a coincidence.
        private bool ShoveKills(MatchState state, PlayerState player, BlastMap blast, out Direction into)
        {
            into = Direction.None;

            if (!player.CanPush)
            {
                return false;
            }

            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState other = state.Players[i];
                if (other.Id == player.Id || !Vision.CanSee(state, player, other))
                {
                    continue;
                }

                for (int d = 0; d < Order.Length; d++)
                {
                    if (!WithinReach(state, player, other, Order[d]))
                    {
                        continue;
                    }

                    if (BurnsOnArrival(state, blast, other, Order[d]))
                    {
                        into = Order[d];
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool WithinReach(MatchState state, PlayerState player, PlayerState other, Direction facing)
        {
            GridPos delta = Directions.Delta(facing);
            int offX = other.Position.X - player.Position.X;
            int offY = other.Position.Y - player.Position.Y;

            if ((offX * delta.X) + (offY * delta.Y) <= 0)
            {
                return false;
            }

            long reach = state.Settings.Push.Reach;
            return ((long)offX * offX) + ((long)offY * offY) <= reach * reach;
        }

        // Walks the tiles the shove would drag them across. Any one of them burning by
        // the time they are carried into it is a kill.
        //
        // Stops where PlayerBody.Blocks stops, bombs included. A bomb is solid once it is
        // down — the assumption the whole trap rule rests on — so one lying in the path
        // halts the target short of the fire beyond it, and reading past it predicts a
        // kill the simulation will not deliver.
        private static bool BurnsOnArrival(MatchState state, BlastMap blast, PlayerState other, Direction facing)
        {
            GridPos delta = Directions.Delta(facing);
            int distance = (state.Settings.Push.Speed * state.Settings.Push.Ticks) / SubPos.UnitsPerTile;

            for (int step = 1; step <= distance; step++)
            {
                GridPos tile = other.Tile.Offset(delta.X * step, delta.Y * step);
                if (!state.Arena.Contains(tile)
                    || Tiles.BlocksMovement(state.Arena[tile])
                    || state.HasBombAt(tile))
                {
                    return false;
                }

                int fire = blast.TicksUntilFire(tile);
                if (fire != BlastMap.Never && fire <= step * SubPos.UnitsPerTile / state.Settings.Push.Speed)
                {
                    return true;
                }
            }

            return false;
        }

        // Whether a bomb at `from` would cover `target`.
        //
        // What this replaces, EnemyInBlastLine, walked the four cardinals. That was right
        // while a blast was a cross and has been wrong since it became a disc: a target
        // standing diagonally beside the bot did not count, according to a bot whose bomb
        // would have covered them. Radius and line of sight, the way ExplosionResolver
        // reads it, so the two cannot drift apart again.
        private static bool Reaches(Arena arena, GridPos from, GridPos target, int range)
        {
            int dx = target.X - from.X;
            int dy = target.Y - from.Y;

            if ((dx * dx) + (dy * dy) > range * range)
            {
                return false;
            }

            return LineOfSight.Between(arena, from, target, true);
        }

        // The opponent the bot is currently hunting: whichever it believes is nearest.
        // Straight-line rather than walking distance, because this only has to pick one
        // of them and the route is worked out by the search that follows.
        private bool NearestBelief(GridPos from, out GridPos target)
        {
            target = default;
            long best = long.MaxValue;

            for (int i = 0; i < sightings.Count; i++)
            {
                long dx = sightings[i].Tile.X - from.X;
                long dy = sightings[i].Tile.Y - from.Y;
                long distance = (dx * dx) + (dy * dy);

                if (distance < best)
                {
                    best = distance;
                    target = sightings[i].Tile;
                }
            }

            return best < long.MaxValue;
        }

        private static bool Walkable(MatchState state, GridPos tile)
        {
            return state.Arena.Contains(tile)
                && Tiles.CanBeStoodOn(state.Arena[tile])
                && !state.HasBombAt(tile)
                && !IsClosing(state, tile);
        }

        // A gap that shuts on the way through is the same death as a blast, and the
        // blast map knows nothing about walls. The bot avoids tiles once they are
        // telegraphed, which is exactly the warning a player gets to act on.
        private static bool IsClosing(MatchState state, GridPos tile)
        {
            for (int i = 0; i < state.RegrowingWalls.Count; i++)
            {
                WallRegrowth wall = state.RegrowingWalls[i];
                if (wall.Tile == tile && wall.TicksRemaining <= state.Settings.WallTelegraphTicks)
                {
                    return true;
                }
            }

            return false;
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
