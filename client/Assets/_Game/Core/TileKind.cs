namespace Blastlands.Core
{
    public enum TileKind : byte
    {
        Floor = 0,

        // A breakable wall. Stops movement, sight and fire.
        SoftBlock = 1,

        // Permanent structure.
        HardBlock = 2,

        // Cover you stand in rather than behind: walkable, but nothing sees through it,
        // and it burns like a soft block.
        Bush = 3,

        // Off the island: inside the arena's bounds and not part of the board. That is a
        // third answer the grid never had, because a coordinate used to be either a tile
        // or outside the rectangle, and the rectangle edge was the world edge.
        //
        // It stops movement and nothing else. You can see across a drop, and a blast
        // carries over one, so the only thing the gap costs you is the ground.
        Void = 4
    }

    // Bush and Void each broke the assumption the rest of the code was built on, that a
    // tile is either floor or an obstacle. Every question has to be asked by name now,
    // because "not floor", "blocks sight" and "cannot be walked through" stopped being
    // the same question the moment one tile answered them differently.
    public static class Tiles
    {
        public static bool BlocksMovement(TileKind kind)
        {
            return kind == TileKind.HardBlock || kind == TileKind.SoftBlock || kind == TileKind.Void;
        }

        public static bool CanBeDestroyed(TileKind kind)
        {
            return kind == TileKind.SoftBlock || kind == TileKind.Bush;
        }

        public static bool CanBeStoodOn(TileKind kind)
        {
            return kind == TileKind.Floor || kind == TileKind.Bush;
        }
    }
}
