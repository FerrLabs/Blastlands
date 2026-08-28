namespace Blastlands.Core
{
    // Which board to lay out, asked directly rather than inferred from the game mode.
    //
    // The generator used to test the mode, which held only while one mode meant one
    // board. Classic and Classic Blinded are the same board played under different
    // rules, so a test on the mode would have quietly given the second one an island.
    // Naming the board is what lets a mode be added without touching generation.
    public enum BoardKind : byte
    {
        // An eroded island with nothing permanent on it and cover grown in clumps.
        Island = 0,

        // The rectangle a bomberman inherits: a border ring, a pillar on every even/even
        // coordinate, and soft blocks sprinkled over the rest.
        Lattice = 1
    }
}
