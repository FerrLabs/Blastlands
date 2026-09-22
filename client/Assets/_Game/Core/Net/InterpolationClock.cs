namespace Blastlands.Core.Net
{
    public sealed class InterpolationClock
    {
        public const int UnitsPerTick = 1000;

        private const int SteerDivisor = 10;
        private const int DeadBand = UnitsPerTick / 2;

        private readonly long delay;
        private readonly long snapDistance;
        private long renderTime;
        private int newestTick = -1;

        public InterpolationClock(int delayTicks, int snapTicks)
        {
            delay = (long)System.Math.Max(0, delayTicks) * UnitsPerTick;
            snapDistance = (long)System.Math.Max(1, snapTicks) * UnitsPerTick;
        }

        public bool Started
        {
            get { return newestTick >= 0; }
        }

        public int NewestTick
        {
            get { return newestTick; }
        }

        public long RenderTime
        {
            get { return renderTime; }
        }

        public int ServerTick
        {
            get { return (int)FloorDiv(renderTime + delay, UnitsPerTick); }
        }

        public void Heard(int tick)
        {
            if (tick <= newestTick)
            {
                return;
            }

            bool first = newestTick < 0;
            newestTick = tick;

            if (first)
            {
                renderTime = Target;
            }
        }

        public void Advance(int elapsedUnits)
        {
            if (!Started || elapsedUnits <= 0)
            {
                return;
            }

            long error = Target - renderTime;
            if (error > snapDistance)
            {
                renderTime = Target;
                return;
            }

            long step = elapsedUnits;
            if (error > DeadBand)
            {
                step += elapsedUnits / SteerDivisor;
            }
            else if (error < -DeadBand)
            {
                step -= elapsedUnits / SteerDivisor;
            }

            renderTime = System.Math.Min(renderTime + step, (long)newestTick * UnitsPerTick);
        }

        public int TicksPast(int stateTick)
        {
            return System.Math.Max(0, ServerTick - stateTick);
        }

        private long Target
        {
            get { return ((long)newestTick * UnitsPerTick) - delay; }
        }

        private static long FloorDiv(long value, long divisor)
        {
            long quotient = value / divisor;
            return value % divisor != 0 && value < 0 ? quotient - 1 : quotient;
        }
    }
}
