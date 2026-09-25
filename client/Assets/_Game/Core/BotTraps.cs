using System.Collections.Generic;

namespace Blastlands.Core
{
    internal static class BotTraps
    {
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
        internal static bool WouldTrapSomebody(
            MatchState state, PlayerState player, GridPos tile, int ticksPerTile, BotMemory memory, BotPlanning planning)
        {
            IReadOnlyList<BotMemory.Sighting> sightings = memory.Sightings;
            if (sightings.Count == 0)
            {
                return false;
            }

            BlastMap after = BlastMap.From(state, BotGrid.WithBombAt(state, player, tile));

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
                int targetTicksPerTile = BotPace.TicksPerTileFor(state, sightings[i].PlayerId, ticksPerTile);

                // Judged the same way the bot judges its own escapes. On arrival safety
                // alone a target looks like it gets away whenever the fire has not
                // reached the next tile yet, even when that tile is a pocket closing
                // behind it, so the bot talks itself out of bombs that would have
                // landed. The level that plans its own way out plans the victim's too.
                System.Func<GridPos, int, bool> reachable;
                if (planning == BotPlanning.Always)
                {
                    // The bomb being considered blocks the tile it sits on. Left out,
                    // the victim's window relaxes straight through it and the bot reads
                    // an escape that the bomb it is about to place has already closed.
                    GridPos blocked = tile;
                    EscapeWindow theirs = EscapeWindow.From(
                        state, after, targetTicksPerTile, next => next != blocked && BotGrid.Walkable(state, next));
                    reachable = (candidate, depth) =>
                        candidate != tile && theirs.Allows(candidate, depth * targetTicksPerTile);
                }
                else
                {
                    reachable = (candidate, depth) =>
                        candidate != tile && after.SurvivesArrival(candidate, depth * targetTicksPerTile, 0);
                }

                bool escapes = BotGrid.FirstStepToward(
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
                if (planning == BotPlanning.Always && WaysOut(state, after, target, targetTicksPerTile) <= 1)
                {
                    return true;
                }
            }

            return false;
        }

        private static int WaysOut(MatchState state, BlastMap after, GridPos target, int ticksPerTile)
        {
            int doors = 0;
            for (int i = 0; i < BotGrid.Order.Length; i++)
            {
                GridPos delta = Directions.Delta(BotGrid.Order[i]);
                GridPos next = target.Offset(delta.X, delta.Y);
                if (BotGrid.Walkable(state, next) && after.SurvivesArrival(next, ticksPerTile, 0))
                {
                    doors++;
                }
            }

            return doors;
        }
    }
}
