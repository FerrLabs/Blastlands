using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class SurvivalTests
    {
        private static readonly MatchSettings Settings = MatchSettings.SurvivalMode;

        private static MatchState Open(int width, int height, params GridPos[] spawns)
        {
            var state = new MatchState(new Arena(width, height), Settings, 5u);
            foreach (GridPos spawn in spawns)
            {
                state.AddPlayer(spawn);
            }

            return state;
        }

        private static void Run(MatchState state, int ticks)
        {
            var inputs = new PlayerInput[state.Players.Count];
            for (int i = 0; i < ticks && state.Outcome == RoundOutcome.Running; i++)
            {
                MatchSim.Tick(state, inputs);
            }
        }

        [Test]
        public void ASoloRunDoesNotEndBeforeItStarts()
        {
            MatchState state = Open(15, 13, new GridPos(1, 1));

            Run(state, 5);

            Assert.That(state.Outcome, Is.EqualTo(RoundOutcome.Running), "one player is not a winner of a deathmatch here");
        }

        [Test]
        public void TheFirstWaveArrivesAfterTheCountdownAndAwayFromThePlayer()
        {
            MatchState state = Open(15, 13, new GridPos(1, 1));

            Run(state, Settings.Survival.FirstWaveTicks - 1);
            Assert.That(state.Zombies.Count, Is.Zero);

            Run(state, 1);
            Assert.That(state.Wave, Is.EqualTo(1));
            Assert.That(state.Zombies.Count, Is.EqualTo(Settings.Survival.ZombiesIn(1, 1)));
            foreach (Zombie zombie in state.Zombies)
            {
                int steps = System.Math.Abs(zombie.Tile.X - 1) + System.Math.Abs(zombie.Tile.Y - 1);
                Assert.That(steps, Is.GreaterThanOrEqualTo(Settings.Survival.SpawnDistance));
            }
        }

        [Test]
        public void AZombieWalksTheCorridorsToThePlayerAndKillsOnContact()
        {
            MatchState state = Open(15, 13, new GridPos(1, 1));
            state.WaveCountdown = 1000;
            state.Wave = 1;
            state.AddZombie(SubPos.AtTileCentre(new GridPos(9, 7)));

            Run(state, 600);

            Assert.That(state.Players[0].Alive, Is.False);
            Assert.That(state.Outcome, Is.EqualTo(RoundOutcome.Overrun));
        }

        [Test]
        public void AZombieDoesNotWalkThroughABomb()
        {
            MatchState state = Open(9, 3, new GridPos(1, 1));
            state.Wave = 1;
            state.WaveCountdown = 1000;
            for (int x = 0; x < 9; x++)
            {
                state.Arena[new GridPos(x, 0)] = TileKind.HardBlock;
                state.Arena[new GridPos(x, 2)] = TileKind.HardBlock;
            }

            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(4, 1), 0, 1), 100000));
            state.AddZombie(SubPos.AtTileCentre(new GridPos(7, 1)));

            Run(state, 200);

            Assert.That(state.Zombies[0].Tile.X, Is.GreaterThanOrEqualTo(5), "the bomb holds the corridor");
            Assert.That(state.Players[0].Alive, Is.True);
        }

        [Test]
        public void AZombieShutOutByWallsChewsItsWayThrough()
        {
            MatchState state = Open(9, 3, new GridPos(1, 1));
            state.Wave = 1;
            state.WaveCountdown = 1000;
            for (int x = 0; x < 9; x++)
            {
                state.Arena[new GridPos(x, 0)] = TileKind.HardBlock;
                state.Arena[new GridPos(x, 2)] = TileKind.HardBlock;
            }

            state.Arena[new GridPos(4, 1)] = TileKind.SoftBlock;
            state.AddZombie(SubPos.AtTileCentre(new GridPos(7, 1)));

            Run(state, 400);

            Assert.That(state.Arena[new GridPos(4, 1)], Is.Not.EqualTo(TileKind.SoftBlock), "or a sealed pocket would never end the wave");
            Assert.That(state.Players[0].Alive, Is.False);
        }

        [Test]
        public void FireKillsZombiesAndCountsThem()
        {
            MatchState state = Open(15, 13, new GridPos(1, 1));
            state.Wave = 1;
            state.WaveCountdown = 1000;
            state.AddZombie(SubPos.AtTileCentre(new GridPos(9, 9)));
            state.AddFlame(new GridPos(9, 9), 10, state.Tick);

            Run(state, 1);

            Assert.That(state.Zombies.Count, Is.Zero);
            Assert.That(state.ZombiesSlain, Is.EqualTo(1));
        }

        [Test]
        public void ClearingTheLastWaveWinsTheRun()
        {
            MatchState state = Open(15, 13, new GridPos(1, 1));
            state.Wave = Settings.Survival.Waves;

            Run(state, 1);

            Assert.That(state.Outcome, Is.EqualTo(RoundOutcome.Survived));
        }

        [Test]
        public void ABreatherSeparatesTheWaves()
        {
            MatchState state = Open(15, 13, new GridPos(1, 1));
            state.Wave = 1;
            state.WaveCountdown = Settings.Survival.BreatherTicks;

            Run(state, Settings.Survival.BreatherTicks - 1);
            Assert.That(state.Wave, Is.EqualTo(1));

            Run(state, 1);
            Assert.That(state.Wave, Is.EqualTo(2));
            Assert.That(state.Zombies.Count, Is.EqualTo(Settings.Survival.ZombiesIn(2, 1)));
        }

        [Test]
        public void WavesGrowWithTheWaveAndThePlayers()
        {
            SurvivalSettings survival = Settings.Survival;

            Assert.That(survival.ZombiesIn(2, 1), Is.GreaterThan(survival.ZombiesIn(1, 1)));
            Assert.That(survival.ZombiesIn(1, 4), Is.GreaterThan(survival.ZombiesIn(1, 1)));
            Assert.That(survival.ZombiesIn(99, 8), Is.EqualTo(survival.MostZombies));
            Assert.That(survival.SpeedIn(survival.Waves), Is.LessThan(Settings.SpeedFor(0)), "a zombie never outruns a player who has not picked up a speed boot");
        }

        [Test]
        public void TheOtherModesHaveNoZombies()
        {
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic, GameMode.ClassicBlinded })
            {
                Assert.That(MatchSettings.For(mode).Survival.Enabled, Is.False, mode.ToString());
            }

            Assert.That(MatchSettings.For(GameMode.Survival).Survival.Enabled, Is.True);
            Assert.That(MatchSettings.For(GameMode.Survival).SuddenDeath.Enabled, Is.False, "the coast would end every run on a timer");
        }

        [Test]
        public void BotsHoldOutAgainstTheFirstWaves()
        {
            int pastTheFirst = 0;
            for (uint seed = 1; seed <= 6; seed++)
            {
                MatchState state = MatchFactory.Create(ArenaSettings.Classic, Settings, 2, seed);
                var brains = new[] { new BotBrain(0, BotSettings.Hard), new BotBrain(1, BotSettings.Hard) };
                var inputs = new PlayerInput[2];
                for (int tick = 0; tick < 6000 && state.Outcome == RoundOutcome.Running; tick++)
                {
                    inputs[0] = brains[0].Think(state);
                    inputs[1] = brains[1].Think(state);
                    MatchSim.Tick(state, inputs);
                }

                if (state.Wave >= 2)
                {
                    pastTheFirst++;
                }
            }

            Assert.That(pastTheFirst, Is.GreaterThanOrEqualTo(4), "two Hard bots should get past the first wave most runs");
        }
    }
}
