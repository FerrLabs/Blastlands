namespace Blastlands.Core
{
    // Who can see whom. Two rules, and they answer different questions: line of sight
    // covers what is between you, and cover covers what you are standing in.
    //
    // Standing in a bush hides you from anyone outside it, but does not blind you: the
    // Bresenham walk skips both endpoints, so your own tile never blocks your view. That
    // asymmetry is the point of the tile and not an oversight. A bush that blinded its
    // occupant would be a place to avoid rather than a place to use, and the cost of
    // hiding is already there: you have to stop and stay still to keep it.
    public static class Vision
    {
        public static bool CanSee(MatchState state, PlayerState viewer, PlayerState target)
        {
            if (viewer == null || target == null || !target.Alive)
            {
                return false;
            }

            if (viewer.Id == target.Id)
            {
                return true;
            }

            return viewer.Alive
                && !IsHidden(state, target)
                && LineOfSight.Between(state.Arena, viewer.Tile, target.Tile, true);
        }

        // Bombs and flames are deliberately not part of this. A blast you could not see
        // coming is not a fair death, and a hiding place that also hides the fuse under
        // your feet kills the player using it more often than the one hunting them.
        public static bool IsHidden(MatchState state, PlayerState player)
        {
            return player.RevealTicksRemaining <= 0
                && state.Arena.Contains(player.Tile)
                && state.Arena[player.Tile] == TileKind.Bush;
        }

        // Whether the viewer can see a tile well enough to conclude nobody is standing
        // on it. A bush never qualifies: seeing the bush tells you nothing about what is
        // inside it, which is what lets someone break contact by stepping into one.
        public static bool CanSeeItIsEmpty(MatchState state, GridPos from, GridPos tile)
        {
            return state.Arena.Contains(tile)
                && state.Arena[tile] != TileKind.Bush
                && LineOfSight.Between(state.Arena, from, tile, true);
        }
    }
}
