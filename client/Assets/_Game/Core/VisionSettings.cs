namespace Blastlands.Core
{
    // Grouped rather than added to MatchSettings one field at a time, for the reason
    // PushSettings gives: that constructor is already twenty-three positional ints of
    // the same type.
    public readonly struct VisionSettings
    {
        public VisionSettings(int revealTicks)
        {
            RevealTicks = revealTicks;
        }

        // How long acting gives you away. A bush that keeps hiding someone while they
        // dash out of it or shove you is not cover, it is an ambush with no counterplay:
        // the victim never had anything to react to. Roughly a second, so the hider has
        // to choose between staying hidden and doing something.
        public int RevealTicks { get; }

        public static VisionSettings Default
        {
            get { return new VisionSettings(30); }
        }
    }
}
