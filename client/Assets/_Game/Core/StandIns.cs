using System;

namespace Blastlands.Core
{
    public sealed class StandIns
    {
        private readonly BotBrain[] bots;
        private readonly BotSettings settings;

        public StandIns(int seats, BotSettings settings)
        {
            bots = new BotBrain[seats];
            this.settings = settings;
        }

        public void Fill(MatchState state, PlayerInput[] inputs, Func<int, bool> seated)
        {
            int seats = Math.Min(Math.Min(inputs.Length, bots.Length), state.Players.Count);
            for (int seat = 0; seat < seats; seat++)
            {
                PlayerState player = state.Players[seat];
                player.IsBot = !seated(seat);
                if (!player.IsBot || !player.Alive)
                {
                    continue;
                }

                if (bots[seat] == null)
                {
                    bots[seat] = new BotBrain(seat, settings);
                }

                inputs[seat] = bots[seat].Think(state);
            }
        }
    }
}
