using System.Collections.Generic;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class PowerUpTests
    {
        private static readonly MatchSettings Settings = MatchSettings.Default;

        private static MatchState OpenMatch(params GridPos[] spawns)
        {
            var state = new MatchState(new Arena(9, 9), Settings, 3u);
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

        private static void RunUntilFlames(MatchState state, PlayerInput[] inputs)
        {
            for (int i = 0; i < 400 && state.Flames.Count == 0; i++)
            {
                MatchSim.Tick(state, inputs);
            }
        }

        // Drops a pickup straight onto the floor, through the same path a blast uses.
        private static void PlaceVisible(MatchState state, GridPos tile, PowerUpKind kind)
        {
            state.HidePowerUp(tile, kind);

            PowerUpKind revealed;
            state.TryRevealPowerUp(tile, out revealed);
        }

        [Test]
        public void ABlastRevealsWhatWasHiddenUnderABlock()
        {
            MatchState state = OpenMatch(new GridPos(1, 1), new GridPos(7, 7));
            state.Arena[new GridPos(2, 1)] = TileKind.SoftBlock;
            state.HidePowerUp(new GridPos(2, 1), PowerUpKind.FireUp);

            MatchSim.Tick(state, new[] { PlayerInput.Dropping(), PlayerInput.None });
            RunUntilFlames(state, new[] { PlayerInput.None, PlayerInput.None });

            Assert.That(state.PowerUps.Count, Is.EqualTo(1));
            Assert.That(state.PowerUps[0].Tile, Is.EqualTo(new GridPos(2, 1)));
        }

        [Test]
        public void APickupSurvivesTheBlastThatUncoveredIt()
        {
            // Otherwise a power-up could never be collected: the flame that frees it
            // would destroy it on the same tick.
            MatchState state = OpenMatch(new GridPos(1, 1), new GridPos(7, 7));
            state.Arena[new GridPos(2, 1)] = TileKind.SoftBlock;
            state.HidePowerUp(new GridPos(2, 1), PowerUpKind.BombUp);

            MatchSim.Tick(state, new[] { PlayerInput.Dropping(), PlayerInput.None });
            RunUntilFlames(state, new[] { PlayerInput.None, PlayerInput.None });

            Assert.That(state.HasFlameAt(new GridPos(2, 1)), Is.True, "the tile is on fire");
            Assert.That(state.PowerUps.Count, Is.EqualTo(1), "and the pickup is still there");
        }

        [Test]
        public void APickupOutlivesTheWholeFlameThatUncoveredIt()
        {
            // Checking only the tick of the reveal is not enough: flames burn for
            // FlameTicks, so a pickup that merely survives its first tick was still
            // being destroyed on the next one and never became collectable.
            // The bomb belongs to nobody standing nearby, so the round cannot end and
            // freeze the simulation before the flame has burnt out.
            MatchState state = OpenMatch(new GridPos(1, 1), new GridPos(7, 7));
            state.Arena[new GridPos(5, 4)] = TileKind.SoftBlock;
            state.HidePowerUp(new GridPos(5, 4), PowerUpKind.BombUp);
            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(4, 4), 99, 2), 2));

            RunUntilFlames(state, new[] { PlayerInput.None, PlayerInput.None });
            Assert.That(state.PowerUps.Count, Is.EqualTo(1), "the blast revealed it");

            for (int i = 0; i < Settings.FlameTicks + 2; i++)
            {
                MatchSim.Tick(state, new[] { PlayerInput.None, PlayerInput.None });
                Assert.That(state.PowerUps.Count, Is.EqualTo(1), $"gone {i} ticks after the reveal");
            }

            Assert.That(state.HasFlameAt(new GridPos(5, 4)), Is.False, "the flame has burnt out");
        }

        [Test]
        public void ALaterBlastDestroysAnUncollectedPickup()
        {
            MatchState state = OpenMatch(new GridPos(1, 1), new GridPos(7, 7));
            state.Arena[new GridPos(5, 4)] = TileKind.SoftBlock;
            state.HidePowerUp(new GridPos(5, 4), PowerUpKind.FireUp);
            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(4, 4), 99, 2), 2));

            RunUntilFlames(state, new[] { PlayerInput.None, PlayerInput.None });
            Assert.That(state.PowerUps.Count, Is.EqualTo(1), "revealed by the first blast");

            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(5, 4), 99, 1), 1));
            Run(state, 40, PlayerInput.None, PlayerInput.None);

            Assert.That(state.PowerUps, Is.Empty, "a second blast takes it");
        }

        [Test]
        public void WalkingOntoAPickupAppliesIt()
        {
            MatchState state = OpenMatch(new GridPos(1, 1));
            PlaceVisible(state, new GridPos(2, 1), PowerUpKind.FireUp);

            int before = state.Players[0].FireRange;
            Run(state, 12, PlayerInput.Moving(Direction.Right));

            Assert.That(state.Players[0].Tile.X, Is.EqualTo(2), "the player reached the tile");
            Assert.That(state.Players[0].FireRange, Is.EqualTo(before + 1));
            Assert.That(state.PowerUps, Is.Empty, "and it is consumed");
        }

        [Test]
        public void EachKindChangesTheRightThing()
        {
            var cases = new Dictionary<PowerUpKind, System.Func<PlayerState, int>>
            {
                { PowerUpKind.BombUp, p => p.CarryCapacity },
                { PowerUpKind.FireUp, p => p.FireRange },
                { PowerUpKind.SpeedUp, p => p.SpeedSteps },
            };

            foreach (KeyValuePair<PowerUpKind, System.Func<PlayerState, int>> entry in cases)
            {
                MatchState state = OpenMatch(new GridPos(1, 1));
                PlaceVisible(state, new GridPos(2, 1), entry.Key);

                int before = entry.Value(state.Players[0]);
                Run(state, 12, PlayerInput.Moving(Direction.Right));

                Assert.That(entry.Value(state.Players[0]), Is.EqualTo(before + 1), entry.Key.ToString());
            }
        }

        [Test]
        public void BombKindPickupsArmTheNextBomb()
        {
            MatchState state = OpenMatch(new GridPos(1, 1));
            PlaceVisible(state, new GridPos(2, 1), PowerUpKind.ClusterBomb);

            Run(state, 12, PlayerInput.Moving(Direction.Right));

            Assert.That(state.Players[0].NextBombKind, Is.EqualTo(BombKind.Cluster));
        }

        [Test]
        public void StatsStopAtTheirCap()
        {
            MatchState state = OpenMatch(new GridPos(1, 1));
            PlayerState player = state.Players[0];
            player.FireRange = Settings.MaxFireRange;
            player.CarryCapacity = Settings.MaxCarryCapacity;

            PlaceVisible(state, new GridPos(2, 1), PowerUpKind.FireUp);
            Run(state, 12, PlayerInput.Moving(Direction.Right));

            Assert.That(player.FireRange, Is.EqualTo(Settings.MaxFireRange), "no screen-wide blasts");
        }

        [Test]
        public void PlacementIsFixedByTheSeed()
        {
            Arena first = ArenaGenerator.Generate(ArenaSettings.Default, 77u);
            Arena second = ArenaGenerator.Generate(ArenaSettings.Default, 77u);

            Dictionary<GridPos, PowerUpKind> a = PowerUpPlacer.Place(first, 35, 77u);
            Dictionary<GridPos, PowerUpKind> b = PowerUpPlacer.Place(second, 35, 77u);

            Assert.That(a.Count, Is.EqualTo(b.Count));
            foreach (KeyValuePair<GridPos, PowerUpKind> entry in a)
            {
                Assert.That(b[entry.Key], Is.EqualTo(entry.Value), $"{entry.Key} differs between runs");
            }
        }

        [Test]
        public void DifferentSeedsHideDifferentThings()
        {
            Arena arena = ArenaGenerator.Generate(ArenaSettings.Default, 77u);

            Dictionary<GridPos, PowerUpKind> a = PowerUpPlacer.Place(arena, 35, 77u);
            Dictionary<GridPos, PowerUpKind> b = PowerUpPlacer.Place(arena, 35, 78u);

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void PickupsOnlyEverHideUnderSomethingBreakable()
        {
            Arena arena = ArenaGenerator.Generate(ArenaSettings.Default, 500u);

            foreach (KeyValuePair<GridPos, PowerUpKind> entry in PowerUpPlacer.Place(arena, 60, 500u))
            {
                Assert.That(Tiles.CanBeDestroyed(arena[entry.Key]), Is.True, $"{entry.Key} is not breakable");
            }
        }
    }
}
