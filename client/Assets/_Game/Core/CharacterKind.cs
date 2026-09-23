namespace Blastlands.Core
{
    // Who a player is, which decides what they start with. Every kit is one of the
    // power-ups the arena already hands out, taken from the start rather than found:
    // the simulation gains no rule, the wire gains no field, and the choice is still a
    // real one because each kit shapes the opening differently.
    public enum CharacterKind : byte
    {
        // No kit. What every player is in a mode whose rules have no room for one.
        None,

        // Starts with a longer reach.
        Demolisher,

        // Starts one step faster.
        Runner,

        // Starts with bombs that flare around the tip of each arm.
        Grenadier,

        // Starts with bombs that go through soft blocks.
        Sapper
    }
}
