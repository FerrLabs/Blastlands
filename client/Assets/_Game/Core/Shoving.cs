using System.Collections.Generic;

namespace Blastlands.Core
{
    // Who shoves whom, and what it costs them.
    //
    // A shove never kills: bombs are the only source of damage. What it does is move
    // someone somewhere worse, which is why the interesting uses are all interactions —
    // into a blast, into a wall that is about to close, off the bomb they were guarding.
    public static class Shoving
    {
        public static void Resolve(MatchState state, IReadOnlyList<PlayerInput> inputs)
        {
            // Every shove is decided against the same starting positions, so two players
            // shoving each other on the same tick both land rather than the loop order
            // picking a winner.
            var targets = new int[state.Players.Count];

            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                bool wants = i < inputs.Count && inputs[i].Push;

                targets[i] = wants && player.CanPush ? FindTarget(state, player) : -1;
            }

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] < 0)
                {
                    continue;
                }

                PlayerState pusher = state.Players[i];
                PlayerState target = state.Players[targets[i]];

                pusher.PushCooldownRemaining = state.Settings.Push.CooldownTicks;
                target.ShoveDirection = pusher.Facing;
                target.ShoveTicksRemaining = state.Settings.Push.Ticks;
            }
        }

        // The nearest living player within reach, on the side the pusher is facing.
        private static int FindTarget(MatchState state, PlayerState pusher)
        {
            int reach = state.Settings.Push.Reach;
            GridPos facing = Directions.Delta(pusher.Facing);

            if (facing.X == 0 && facing.Y == 0)
            {
                return -1;
            }

            int best = -1;
            long bestDistance = long.MaxValue;

            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState other = state.Players[i];
                if (other.Id == pusher.Id || !other.Alive)
                {
                    continue;
                }

                int offX = other.Position.X - pusher.Position.X;
                int offY = other.Position.Y - pusher.Position.Y;

                // In front, not merely nearby: a shove that reaches behind the shover
                // reads as a bug however good the range check is.
                if ((offX * facing.X) + (offY * facing.Y) <= 0)
                {
                    continue;
                }

                long distance = ((long)offX * offX) + ((long)offY * offY);
                if (distance > (long)reach * reach || distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                best = i;
            }

            return best;
        }

        // Carries a shoved player along, and stops them when they hit something. Running
        // out of room is what turns a reposition into a punish.
        public static void Carry(MatchState state, PlayerState player)
        {
            player.ShoveTicksRemaining--;

            GridPos delta = Directions.Delta(player.ShoveDirection);
            SubPos before = player.Position;

            PlayerBody.Move(
                state,
                player,
                delta.X * state.Settings.Push.Speed,
                delta.Y * state.Settings.Push.Speed);

            if (player.Position.Equals(before))
            {
                player.ShoveTicksRemaining = 0;
                player.StunTicksRemaining = state.Settings.Push.StunTicks;
            }
        }
    }
}
