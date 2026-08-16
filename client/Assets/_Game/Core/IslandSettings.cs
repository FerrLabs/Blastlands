namespace Blastlands.Core
{
    public readonly struct IslandSettings
    {
        public IslandSettings(int radiusPercent, int erosionPasses, int erosionPercent)
        {
            RadiusPercent = radiusPercent;
            ErosionPasses = erosionPasses;
            ErosionPercent = erosionPercent;
        }

        // How much of the rectangle the ellipse fills, before erosion. Under a hundred
        // so the island floats clear of the bounds on every side: at a hundred it touches
        // them, and an island flush against the edge of the world is just a rectangle
        // with the corners taken off.
        public int RadiusPercent { get; }

        // Passes and per-tile chance of the coast retreating. Together these are how
        // ragged the outline gets. Few deep passes carve bays; many shallow ones fray the
        // whole coast evenly, which reads as noise rather than as a shape.
        public int ErosionPasses { get; }

        public int ErosionPercent { get; }

        public static IslandSettings Default
        {
            // Measured on the 25x21 bounds over 200 seeds, counting tiles that are not
            // void. The rectangle held 525.
            //
            //   radius/passes/percent   ground   thinnest seed
            //     100 / 0 /  0            373         373      (a plain ellipse)
            //      92 / 0 /  0            321         321
            //      92 / 3 / 20            281         259      <- here
            //      92 / 5 / 30            222         189
            //      92 / 8 / 40            118          86
            //
            // Erosion costs far more than it looks like it should, because a coast that
            // pinches through gets a whole limb thrown away by the flood rather than a
            // few tiles trimmed. Past three passes that stops being an occasional
            // accident and becomes the usual outcome.
            get { return new IslandSettings(92, 3, 20); }
        }
    }
}
