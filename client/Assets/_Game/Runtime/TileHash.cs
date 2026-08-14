namespace Blastlands.Runtime
{
    // Per-tile variation has to be stable: picked once and never re-rolled, or the
    // arena flickers as objects are rebuilt. Random would also differ between two
    // clients watching the same match, so this is a pure hash of tile and seed.
    public static class TileHash
    {
        public static int At(int x, int y, uint seed)
        {
            unchecked
            {
                uint hash = seed;
                hash ^= (uint)(x * 0x9E3779B1);
                hash = (hash ^ (hash >> 15)) * 0x85EBCA6Bu;
                hash ^= (uint)(y * 0xC2B2AE35);
                hash = (hash ^ (hash >> 13)) * 0xC2B2AE35u;
                hash ^= hash >> 16;
                return (int)(hash & 0x7FFFFFFF);
            }
        }
    }
}
