namespace Blastlands.Core
{
    internal static class BotEscapes
    {
        internal static Direction Escape(
            MatchState state, BlastMap blast, PlayerState player, int ticksPerTile, BotSettings settings)
        {
            if (settings.Planning == BotPlanning.OnArrival)
            {
                return StepToSafety(state, blast, player.Tile, ticksPerTile, settings.SafetyMarginTicks);
            }

            EscapeWindow window = EscapeWindow.From(state, blast, ticksPerTile, next => BotGrid.Walkable(state, next));
            int here = blast.TicksUntilFire(player.Tile);

            Direction best = Direction.None;
            long bestSlack = -1;

            for (int i = 0; i < BotGrid.Order.Length; i++)
            {
                GridPos delta = Directions.Delta(BotGrid.Order[i]);
                GridPos next = player.Tile.Offset(delta.X, delta.Y);
                int latest;
                if (!BotGrid.Walkable(state, next) || !window.TryLatestEntry(next, out latest))
                {
                    continue;
                }

                int arrival = BotPace.TicksToCross(state, player, BotGrid.Order[i]);
                long slack = (long)latest - arrival;
                if (arrival < here && slack > bestSlack)
                {
                    best = BotGrid.Order[i];
                    bestSlack = slack;
                }
            }

            return best != Direction.None
                ? best
                : StepToSafety(state, blast, player.Tile, ticksPerTile, settings.SafetyMarginTicks);
        }

        // Fleeing to a tile that merely burns later is what gets a bot cornered: it
        // outruns one blast into the next one, and each hop has fewer ways out than the
        // last. Aim for ground the current bombs cannot reach at all, and settle for
        // buying time only when there is none.
        internal static Direction StepToSafety(
            MatchState state, BlastMap blast, GridPos from, int ticksPerTile, int safetyMarginTicks)
        {
            Direction clear = BotGrid.FirstStepToward(
                state,
                from,
                (tile, depth) => blast.TicksUntilFire(tile) == BlastMap.Never,
                (tile, depth) => blast.SurvivesArrival(tile, depth * ticksPerTile, 0));

            if (clear != Direction.None)
            {
                return clear;
            }

            return BotGrid.FirstStepToward(
                state,
                from,
                (tile, depth) => blast.SurvivesArrival(tile, depth * ticksPerTile, safetyMarginTicks),
                (tile, depth) => blast.SurvivesArrival(tile, depth * ticksPerTile, 0));
        }

        // The check that stops a bot killing itself: place the bomb it is considering,
        // recompute the danger it would create, and return the way out, or None when
        // there is not one.
        internal static Direction AfterBombing(
            MatchState state, PlayerState player, GridPos tile, int ticksPerTile, int safetyMarginTicks)
        {
            BlastMap after = BlastMap.From(state, BotTraps.WithBombAt(state, player, tile));

            // Only ground the hypothetical bombs cannot reach at all counts here, with
            // none of the fallback StepToSafety allows. Settling for a tile that merely
            // burns later is reasonable when you are already in danger and have to pick
            // the least bad option; it is not reasonable when you are choosing to
            // create the danger. Taking the fallback is how a bot bombs its own last
            // exit and stands in the corner waiting.
            return BotGrid.FirstStepToward(
                state,
                tile,
                (candidate, depth) => after.TicksUntilFire(candidate) == BlastMap.Never,
                (candidate, depth) => after.SurvivesArrival(candidate, depth * ticksPerTile, safetyMarginTicks));
        }
    }
}
