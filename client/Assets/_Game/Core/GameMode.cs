namespace Blastlands.Core
{
    // What the lobby offers. The simulation never reads this: it reads the RuleSet the
    // mode resolves to, so a rule is asked for by name rather than inferred from which
    // mode happens to be running.
    public enum GameMode : byte
    {
        Arena = 0,

        // The real thing: the whole board visible to everyone, and the only verb is
        // placing a bomb.
        Classic = 1,

        // The same board and the same bombs, played blind. You see a player only when
        // nothing stands between you, and a lattice is nothing but things standing
        // between you.
        ClassicBlinded = 2,

        Survival = 3
    }
}
