namespace Blastlands.Core
{
    public readonly struct SurvivalSettings
    {
        public SurvivalSettings(
            int waves,
            int firstWaveTicks,
            int breatherTicks,
            int firstWaveZombies,
            int zombiesPerWave,
            int zombiesPerExtraPlayer,
            int mostZombies,
            int zombieSpeed,
            int zombieSpeedPerWave,
            int contactReach,
            int chewTicks,
            int spawnDistance,
            bool fireHurtsPlayers)
        {
            FireHurtsPlayers = fireHurtsPlayers;
            Waves = waves;
            FirstWaveTicks = firstWaveTicks;
            BreatherTicks = breatherTicks;
            FirstWaveZombies = firstWaveZombies;
            ZombiesPerWave = zombiesPerWave;
            ZombiesPerExtraPlayer = zombiesPerExtraPlayer;
            MostZombies = mostZombies;
            ZombieSpeed = zombieSpeed;
            ZombieSpeedPerWave = zombieSpeedPerWave;
            ContactReach = contactReach;
            ChewTicks = chewTicks;
            SpawnDistance = spawnDistance;
        }

        public int Waves { get; }

        public int FirstWaveTicks { get; }

        public int BreatherTicks { get; }

        public int FirstWaveZombies { get; }

        public int ZombiesPerWave { get; }

        public int ZombiesPerExtraPlayer { get; }

        public int MostZombies { get; }

        public int ZombieSpeed { get; }

        public int ZombieSpeedPerWave { get; }

        public int ContactReach { get; }

        public int ChewTicks { get; }

        public int SpawnDistance { get; }

        public bool FireHurtsPlayers { get; }

        public bool Enabled
        {
            get { return Waves > 0; }
        }

        public int ZombiesIn(int wave, int players)
        {
            int extra = players > 1 ? (players - 1) * ZombiesPerExtraPlayer * wave / 2 : 0;
            int count = FirstWaveZombies + ((wave - 1) * ZombiesPerWave) + extra;
            return count > MostZombies ? MostZombies : count;
        }

        public int SpeedIn(int wave)
        {
            return ZombieSpeed + ((wave - 1) * ZombieSpeedPerWave);
        }

        public static SurvivalSettings Default
        {
            get { return new SurvivalSettings(5, 90, 150, 3, 2, 1, 20, 14, 2, 128, 90, 5, false); }
        }

        public static SurvivalSettings Off
        {
            get { return new SurvivalSettings(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, true); }
        }
    }
}
