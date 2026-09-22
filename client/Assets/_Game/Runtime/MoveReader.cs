using Blastlands.Core;
using UnityEngine;

namespace Blastlands.Runtime
{
    public static class MoveReader
    {
        public static void Resolve(Vector2 step, Vector2 stick, out int moveX, out int moveY)
        {
            Direction stepped = StickReader.ToDirection(ToGridX(step), ToGridY(step));
            if (stepped != Direction.None)
            {
                GridPos delta = Directions.Delta(stepped);
                moveX = delta.X * StickReader.Range;
                moveY = delta.Y * StickReader.Range;
                return;
            }

            int x = ToGridX(stick);
            int y = ToGridY(stick);

            if (StickReader.ToDirection(x, y) == Direction.None)
            {
                moveX = 0;
                moveY = 0;
                return;
            }

            moveX = x;
            moveY = y;
        }

        private static int ToGridX(Vector2 value)
        {
            return Mathf.RoundToInt(value.x * StickReader.Range);
        }

        private static int ToGridY(Vector2 value)
        {
            return Mathf.RoundToInt(-value.y * StickReader.Range);
        }
    }
}
