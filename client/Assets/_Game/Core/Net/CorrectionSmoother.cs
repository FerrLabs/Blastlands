using System;

namespace Blastlands.Core.Net
{
    public sealed class CorrectionSmoother
    {
        public const int DefaultSnapDistance = SubPos.UnitsPerTile;

        private const int KeepNumerator = 3;
        private const int KeepDenominator = 4;

        private readonly int snapDistance;
        private int offsetX;
        private int offsetY;

        public CorrectionSmoother(int snapDistance = DefaultSnapDistance)
        {
            this.snapDistance = Math.Max(0, snapDistance);
        }

        public SubPos Offset
        {
            get { return new SubPos(offsetX, offsetY); }
        }

        public void Corrected(SubPos before, SubPos after)
        {
            int x = offsetX + (before.X - after.X);
            int y = offsetY + (before.Y - after.Y);

            if (Math.Abs(x) > snapDistance || Math.Abs(y) > snapDistance)
            {
                Clear();
                return;
            }

            offsetX = x;
            offsetY = y;
        }

        public void Tick()
        {
            offsetX = offsetX * KeepNumerator / KeepDenominator;
            offsetY = offsetY * KeepNumerator / KeepDenominator;
        }

        public void Clear()
        {
            offsetX = 0;
            offsetY = 0;
        }

        public SubPos Apply(SubPos predicted)
        {
            return new SubPos(predicted.X + offsetX, predicted.Y + offsetY);
        }
    }
}
