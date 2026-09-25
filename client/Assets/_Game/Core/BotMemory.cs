using System.Collections.Generic;

namespace Blastlands.Core
{
    internal sealed class BotMemory
    {
        private const int ZombieSighting = -1;

        private readonly int memoryTicks;
        private readonly List<Sighting> sightings = new List<Sighting>();

        internal BotMemory(int memoryTicks)
        {
            this.memoryTicks = memoryTicks;
        }

        // What the bot last saw of one opponent. A position it is entitled to believe
        // rather than one it knows, which is the whole difference vision makes: the bot
        // hunts where you were, and is wrong about it as often as a player would be.
        internal struct Sighting
        {
            public int PlayerId;
            public GridPos Tile;
            public int Tick;
        }

        internal IReadOnlyList<Sighting> Sightings
        {
            get { return sightings; }
        }

        // Three things happen here, and they are separate on purpose. Anyone in sight is
        // recorded where they stand. Anyone whose remembered tile is now visibly empty is
        // dropped, because a bot that keeps bombing a corner it can see nobody is in
        // looks broken rather than fooled. Everything else simply ages out.
        internal void Observe(MatchState state, PlayerState self)
        {
            if (state.Settings.Survival.Enabled)
            {
                ObserveZombies(state);
                return;
            }

            for (int i = sightings.Count - 1; i >= 0; i--)
            {
                Sighting stale = sightings[i];
                if (state.Tick - stale.Tick > memoryTicks
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

        internal bool BelievesEnemyAt(GridPos tile)
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

        // The opponent the bot is currently hunting: whichever it believes is nearest.
        // Straight-line rather than walking distance, because this only has to pick one
        // of them and the route is worked out by the search that follows.
        internal bool NearestBelief(GridPos from, out GridPos target)
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

        private void ObserveZombies(MatchState state)
        {
            sightings.Clear();
            for (int i = 0; i < state.Zombies.Count; i++)
            {
                Zombie zombie = state.Zombies[i];
                sightings.Add(new Sighting { PlayerId = ZombieSighting - zombie.Id, Tile = zombie.Tile, Tick = state.Tick });
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
    }
}
