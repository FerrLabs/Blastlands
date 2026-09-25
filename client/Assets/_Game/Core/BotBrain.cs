namespace Blastlands.Core
{
    // Server-side AI producing the same PlayerInput a human socket does, so the
    // simulation cannot tell the two apart. Deterministic: no clock, no unseeded
    // randomness, directions always walked in the same order.
    public sealed class BotBrain
    {
        private readonly int playerId;
        private readonly BotSettings settings;
        private readonly BotMemory memory;

        private int cooldown;
        private Direction heading;
        private Direction plannedEscape;

        public BotBrain(int playerId, BotSettings settings)
        {
            this.playerId = playerId;
            this.settings = settings;
            memory = new BotMemory(settings.MemoryTicks);
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
            memory.Observe(state, player);

            // Between decisions the bot keeps walking the way it was, which reads as
            // hesitation rather than as a freeze. The bomb is never repeated: it would
            // otherwise fire again on every tick of the delay.
            if (cooldown > 0)
            {
                cooldown--;
                return BotSteering.Steer(player, heading, false);
            }

            cooldown = settings.ReactionTicks;
            PlayerInput decision = Decide(state, player);

            // Dropping carries no direction, so taking the heading from it would leave
            // the bot standing on its own bomb for the whole reaction delay. It leaves
            // along the route the escape check already proved was open.
            if (decision.Ability)
            {
                heading = Direction.None;
            }
            else
            {
                heading = decision.DropBomb ? plannedEscape : decision.Move;
            }

            return decision;
        }

        // The bot's world model, exposed because it is the thing worth asserting about.
        // A decision cannot stand in for it: the planner walks Right when it has nothing
        // to do and towards any destructible tile it can reach, so the same step comes
        // out of "sees nobody" and "sees somebody and had a better idea".
        public bool BelievesOpponentAt(GridPos tile)
        {
            return memory.BelievesEnemyAt(tile);
        }

        private PlayerInput Decide(MatchState state, PlayerState player)
        {
            BlastMap blast = BlastMap.From(state);
            GridPos tile = player.Tile;
            int ticksPerTile = BotPace.TicksPerTile(state, player);

            // Nothing else matters while standing somewhere about to burn. A dash is
            // worth spending here and nowhere else: it is committed for its whole
            // length, so it only pays when the direction is not going to change.
            if (!blast.IsSafeFor(tile, settings.LookaheadTicks))
            {
                Direction away = BotEscapes.Escape(state, blast, player, ticksPerTile, settings);
                bool dash = settings.ReactionTicks > 0 && player.CanDash && away != Direction.None;
                return BotSteering.Steer(player, away, dash);
            }

            if (state.Settings.Survival.Enabled && BotZombies.ClosestZombie(state, tile) <= 2)
            {
                if (player.CanDropBomb && !state.HasBombAt(tile) && BotZombies.ZombieInReach(state, tile, player.FireRange))
                {
                    Direction escape = BotEscapes.AfterBombing(state, player, tile, ticksPerTile, settings.SafetyMarginTicks);
                    if (escape != Direction.None)
                    {
                        plannedEscape = escape;
                        return PlayerInput.Dropping();
                    }
                }

                Direction away = BotZombies.AwayFromZombies(state, blast, tile);
                if (away != Direction.None)
                {
                    return BotSteering.Steer(player, away, false);
                }
            }

            // Shoving comes before bombing because it is the only thing here that kills
            // on the tick it happens. A bomb is a threat somebody has two and a half
            // seconds to walk away from, and a bot that has learnt to dodge one will.
            if (BotShove.Kills(state, player, blast, out Direction shoveInto))
            {
                return PlayerInput.Pushing(shoveInto);
            }

            if (BotAbilities.ShouldUse(state, player))
            {
                return PlayerInput.UsingAbility();
            }

            if (player.CanDropBomb
                && !state.HasBombAt(tile)
                && (BotTraps.WouldTrapSomebody(state, player, tile, ticksPerTile, memory, settings.Planning)
                    || BotGrid.TouchesSoftBlock(state, tile)))
            {
                Direction escape = BotEscapes.AfterBombing(state, player, tile, ticksPerTile, settings.SafetyMarginTicks);
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
                toward = BotEscapes.StepToSafety(state, blast, tile, ticksPerTile, settings.SafetyMarginTicks);
            }

            return BotSteering.Steer(player, toward, false);
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
                EscapeWindow window = EscapeWindow.From(state, blast, ticksPerTile, next => BotGrid.Walkable(state, next));
                leavable = (tile, depth) => window.Allows(tile, (depth * ticksPerTile) + settings.SafetyMarginTicks);
            }
            else
            {
                leavable = (tile, depth) =>
                    blast.SurvivesArrival(tile, depth * ticksPerTile, settings.SafetyMarginTicks);
            }

            if (player.CanCarryMore)
            {
                Direction toBomb = BotGrid.FirstStepToward(
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
            if (player.CanDropBomb && memory.NearestBelief(from, out target))
            {
                Direction toFiringPosition = BotGrid.FirstStepToward(
                    state,
                    from,
                    (tile, depth) => leavable(tile, depth) && BotGrid.Reaches(state.Arena, tile, target, player.FireRange, state.Settings.Rules.Blast),
                    leavable);

                if (toFiringPosition != Direction.None)
                {
                    return toFiringPosition;
                }
            }

            return BotGrid.FirstStepToward(
                state,
                from,
                (tile, depth) => leavable(tile, depth) && BotGrid.TouchesSoftBlock(state, tile),
                leavable);
        }
    }
}
