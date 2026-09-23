namespace Blastlands.Core
{
    // The rules that differ between modes, asked for by name.
    //
    // A struct of named questions rather than an enum the simulation switches on: a
    // reader of DropBombs should see "do bombs come back" rather than "is this Classic",
    // and a third mode later answers the same questions instead of adding a third branch
    // everywhere.
    //
    // Movement is deliberately absent. It is free in every mode: locking Classic to four
    // axes would mean a second movement system to maintain, and PlayerBody already
    // carries the corner assist that exists so one-tile corridors feel right without it.
    public readonly struct RuleSet
    {
        public RuleSet(bool bombsReturn, bool allowsDash, bool allowsShove, bool hidesTheUnseen, bool allowsCharacters)
        {
            BombsReturn = bombsReturn;
            AllowsDash = allowsDash;
            AllowsShove = allowsShove;
            HidesTheUnseen = hidesTheUnseen;
            AllowsCharacters = allowsCharacters;
        }

        // Whether a bomb comes back to whoever placed it once it has gone off. False is
        // the Blastlands economy: bombs are found on the ground, spent when placed, and
        // running out is what sends a player back into the open.
        public bool BombsReturn { get; }

        public bool AllowsDash { get; }

        public bool AllowsShove { get; }

        // Whether a player can be hidden from another. False makes the whole board
        // visible to everyone, which also makes bushes pointless, so Classic does not
        // generate any.
        public bool HidesTheUnseen { get; }

        // Whether players start with the kit of the character they are. False puts
        // everyone on the same footing, which is part of what Classic is: one verb and
        // the same tools for all.
        public bool AllowsCharacters { get; }

        public static RuleSet Arena
        {
            get { return new RuleSet(false, true, true, true, true); }
        }

        // Everything the arena added, off. What is left is the board, the fuse and one
        // verb, which is the whole of what makes a match read as classic.
        public static RuleSet Classic
        {
            get { return new RuleSet(true, false, false, false, false); }
        }

        // The classic board and the classic bombs, with the lights off.
        //
        // Cover in Arena is a tile you stand in; here it is the lattice itself. A pillar
        // every other tile means a board made almost entirely of things to be behind, so
        // switching sight off changes how it plays far more than it would on open ground.
        public static RuleSet ClassicBlinded
        {
            get { return new RuleSet(true, false, false, true, false); }
        }

        public static RuleSet For(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Classic:
                    return Classic;
                case GameMode.ClassicBlinded:
                    return ClassicBlinded;
                default:
                    return Arena;
            }
        }
    }
}
