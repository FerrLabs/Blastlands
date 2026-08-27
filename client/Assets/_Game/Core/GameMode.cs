namespace Blastlands.Core
{
    // What the lobby offers. The simulation never reads this: it reads the RuleSet the
    // mode resolves to, so a rule is asked for by name rather than inferred from which
    // mode happens to be running.
    public enum GameMode : byte
    {
        Arena = 0,
        Classic = 1
    }
}
