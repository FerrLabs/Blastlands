using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class LooseBombTests
    {
        private static readonly MatchSettings Settings = MatchSettings.Default;

        // The spawner tops the arena up on its own, which is what the spawning tests
        // below check. Everything about picking up and spending needs a floor that only
        // holds what the test put there.
        private static readonly MatchSettings NoSpawning = Settings.WithTilesPerLooseBomb(0);

        private static MatchState OpenMatch(params GridPos[] spawns)
        {
            var state = new MatchState(new Arena(9, 9), NoSpawning, 11u);
            foreach (GridPos spawn in spawns)
            {
                state.AddPlayer(spawn);
            }

            return state;
        }

        private static void Run(MatchState state, int ticks, params PlayerInput[] inputs)
        {
            for (int i = 0; i < ticks; i++)
            {
                MatchSim.Tick(state, inputs);
            }
        }

        [Test]
        public void WalkingOverABombPicksItUp()
        {
            MatchState state = OpenMatch(new GridPos(1, 1));
            state.Players[0].CarryCapacity = 2;
            state.AddLooseBomb(new GridPos(2, 1));

            int before = state.Players[0].BombsHeld;
            Run(state, 12, PlayerInput.Moving(Direction.Right));

            Assert.That(state.Players[0].Tile.X, Is.EqualTo(2), "the player reached the tile");
            Assert.That(state.Players[0].BombsHeld, Is.EqualTo(before + 1));
            Assert.That(state.LooseBombs, Is.Empty, "and it leaves the floor");
        }

        [Test]
        public void AFullPlayerLeavesTheBombOnTheFloor()
        {
            MatchState state = OpenMatch(new GridPos(1, 1));
            PlayerState player = state.Players[0];
            player.BombsHeld = player.CarryCapacity;

            state.AddLooseBomb(new GridPos(2, 1));
            Run(state, 12, PlayerInput.Moving(Direction.Right));

            Assert.That(player.BombsHeld, Is.EqualTo(player.CarryCapacity), "no overflow");
            Assert.That(state.LooseBombs.Count, Is.EqualTo(1), "it is still there to come back for");
        }

        [Test]
        public void PlacingSpendsOneAndDetonatingDoesNotRefund()
        {
            MatchState state = OpenMatch(new GridPos(1, 1), new GridPos(7, 7));
            PlayerState player = state.Players[0];
            player.BombsHeld = 2;

            MatchSim.Tick(state, new[] { PlayerInput.Dropping(), PlayerInput.None });
            Assert.That(player.BombsHeld, Is.EqualTo(1));

            Run(state, Settings.FuseTicks + Settings.FlameTicks + 5,
                PlayerInput.Moving(Direction.Right), PlayerInput.None);

            Assert.That(player.BombsHeld, Is.EqualTo(1), "the spent bomb never comes back");
        }

        [Test]
        public void ABombLyingInFireGoesOff()
        {
            // Otherwise players would shelter behind a pile of explosives.
            MatchState state = OpenMatch(new GridPos(1, 1), new GridPos(7, 7));
            state.AddLooseBomb(new GridPos(5, 4));
            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(4, 4), 99, 2), 2));

            Run(state, 6, PlayerInput.None, PlayerInput.None);

            Assert.That(state.LooseBombs, Is.Empty, "it left the floor");
            Assert.That(state.HasFlameAt(new GridPos(5, 4)), Is.True);

            // It gets a short fuse rather than none, so there is a beat before it goes.
            Run(state, Settings.LooseBombFuseTicks + 2, PlayerInput.None, PlayerInput.None);

            Assert.That(state.HasFlameAt(new GridPos(7, 4)), Is.True, "and it took the tiles beyond it");
        }

        [Test]
        public void TheArenaIsSeededWithBombsAndKeepsToppingUp()
        {
            MatchState state = MatchFactory.Create(ArenaSettings.Default, Settings, 2, 4242u);

            // Not necessarily the full target at creation: every player starts sealed in
            // a small pocket, so there is only so much reachable floor to put them on.
            int seeded = state.LooseBombs.Count;
            Assert.That(seeded, Is.GreaterThan(0), "seeded at creation");
            Assert.That(seeded, Is.LessThanOrEqualTo(BombSpawner.TargetFor(state)));

            while (state.LooseBombs.Count > 0)
            {
                state.RemoveLooseBombAt(0);
            }

            Run(state, Settings.BombRespawnTicks * BombSpawner.TargetFor(state) + 1,
                PlayerInput.None, PlayerInput.None);

            Assert.That(state.LooseBombs.Count, Is.GreaterThanOrEqualTo(seeded), "topped back up");
        }

        [Test]
        public void BombsAreNeverSuppliedFasterThanTheyBurn()
        {
            // The supply rate is not a matter of taste. Below the fuse, a player finds
            // their next bomb before the last has gone off, which is the only way to
            // have two live at once — and two live bombs is how you wall yourself into
            // your own blast. Measured solo survival falls off a cliff at exactly this
            // boundary: 35 of 40 seeds at 90 ticks, 11 at 60.
            Assert.That(
                MatchSettings.Default.BombRespawnTicks,
                Is.GreaterThan(MatchSettings.Default.FuseTicks),
                "lowering this below the fuse lets players seal themselves in");
        }

        [Test]
        public void TheSpawnerNeverOverfillsTheArena()
        {
            MatchState state = MatchFactory.Create(ArenaSettings.Default, Settings, 2, 77u);

            Run(state, Settings.BombRespawnTicks * 10, PlayerInput.None, PlayerInput.None);

            Assert.That(state.LooseBombs.Count, Is.LessThanOrEqualTo(BombSpawner.TargetFor(state)));
        }

        [Test]
        public void BombsNeverSpawnOnTopOfEachOther()
        {
            MatchState state = MatchFactory.Create(ArenaSettings.Default, Settings, 2, 909u);

            Run(state, Settings.BombRespawnTicks * 6, PlayerInput.None, PlayerInput.None);

            for (int i = 0; i < state.LooseBombs.Count; i++)
            {
                for (int j = i + 1; j < state.LooseBombs.Count; j++)
                {
                    Assert.That(state.LooseBombs[i], Is.Not.EqualTo(state.LooseBombs[j]));
                }
            }
        }

        [Test]
        public void BombsOnlySpawnWhereAPlayerCanReachThem()
        {
            // A free tile can be sealed inside a ring of soft blocks. Bombs dropped
            // there make the count say the arena is stocked while nobody can get to a
            // single one, which is the failure the target exists to prevent — and it
            // looks like working code, because the number is right.
            MatchState state = MatchFactory.Create(ArenaSettings.Default, Settings, 4, 2026u);

            Run(state, Settings.BombRespawnTicks * 8, PlayerInput.None, PlayerInput.None,
                PlayerInput.None, PlayerInput.None);

            // From every living player, not just the first: each one starts sealed in
            // its own pocket until it blasts out, so a bomb only the third player can
            // get to is still a bomb somebody can get to.
            var reachable = new System.Collections.Generic.HashSet<GridPos>();
            var queue = new System.Collections.Generic.Queue<GridPos>();

            foreach (PlayerState player in state.Players)
            {
                if (player.Alive && reachable.Add(player.Tile))
                {
                    queue.Enqueue(player.Tile);
                }
            }

            var steps = new[]
            {
                new GridPos(1, 0), new GridPos(0, 1), new GridPos(-1, 0), new GridPos(0, -1)
            };

            while (queue.Count > 0)
            {
                GridPos current = queue.Dequeue();
                foreach (GridPos step in steps)
                {
                    GridPos next = current.Offset(step.X, step.Y);
                    if (state.Arena.Contains(next)
                        && Tiles.CanBeStoodOn(state.Arena[next])
                        && reachable.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            Assert.That(state.LooseBombs.Count, Is.GreaterThan(0), "the arena has bombs at all");
            foreach (GridPos tile in state.LooseBombs)
            {
                Assert.That(reachable, Has.Member(tile), $"{tile} is walled off from the players");
            }
        }

        [Test]
        public void BombsOnlyLieWhereSomebodyCanStand()
        {
            MatchState state = MatchFactory.Create(ArenaSettings.Default, Settings, 4, 313u);

            Run(state, Settings.BombRespawnTicks * 8, PlayerInput.None, PlayerInput.None,
                PlayerInput.None, PlayerInput.None);

            foreach (GridPos tile in state.LooseBombs)
            {
                // Bushes count: a bomb lying in a hedge is a bomb somebody has to walk
                // into cover to fetch, which is the point of having hedges.
                Assert.That(Tiles.CanBeStoodOn(state.Arena[tile]), Is.True, $"{tile} cannot be walked onto");
            }
        }
    }
}
