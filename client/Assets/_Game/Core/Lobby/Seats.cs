namespace Blastlands.Core.Lobby
{
    public static class Seats
    {
        public const int Fewest = 2;
        public const int Most = ArenaGenerator.MostSpawns;
        public const int Default = 4;

        public static int Clamp(int seats)
        {
            return seats < Fewest ? Fewest : (seats > Most ? Most : seats);
        }

        public static int Step(int from, int delta)
        {
            return Clamp(Clamp(from) + delta);
        }
    }
}
