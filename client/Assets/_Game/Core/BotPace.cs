namespace Blastlands.Core
{
    internal static class BotPace
    {
        internal static int TicksPerTile(MatchState state, PlayerState player)
        {
            int speed = ClassicItems.Speed(player, state.Settings);
            return speed <= 0 ? SubPos.UnitsPerTile : ((SubPos.UnitsPerTile + speed - 1) / speed);
        }

        internal static int TicksPerTileFor(MatchState state, int playerId, int fallback)
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

        internal static int TicksToCross(MatchState state, PlayerState player, Direction direction)
        {
            int speed = ClassicItems.Speed(player, state.Settings);
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
    }
}
