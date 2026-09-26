using Blastlands.Core;
using UnityEngine;

namespace Blastlands.Runtime
{
    // What a player did last tick, turned into the two numbers the character controller
    // is driven by: which clip to be in, and how fast to play it.
    //
    // There is no walk. Base speed in this game is a little over three tiles a second,
    // and a walk clip covers well under half of that, so walking would mean legs cycling
    // far slower than the ground the character is actually covering. That reads as
    // gliding. Everyone runs here; the difference between a fresh player and one who
    // has taken every speed pickup is in the cadence, not in the clip.
    public readonly struct Gait
    {
        public Gait(float speed, float cadence)
        {
            Speed = speed;
            Cadence = cadence;
        }

        // Feeds Speed_f. The controller goes to Run above 0.5 and back to Idle below 0.25,
        // the gap keeping a player who barely moves from flickering between the two.
        public float Speed { get; }

        // Feeds Animator.speed, so a stride covers the ground the simulation moved the
        // player over rather than whatever the clip was authored at.
        public float Cadence { get; }
    }

    public static class PlayerPace
    {
        public const float Still = 0f;
        public const float Running = 0.8f;
        public const float RestingCadence = 1f;

        // A player pressed into a wall still resolves a step or two of sub-tile movement
        // before they stop. Reading that as running leaves them jogging on the spot
        // against the wall, so a fraction of their own step is the floor for "moving".
        private const float MovingAtAll = 0.1f;

        // A dash covers far more ground in a tick than a stride can account for, and a
        // stunned player being shoved covers it without taking a step at all. Past these
        // the legs stop being able to explain the movement, and blurring them faster
        // only makes it worse.
        private const float SlowestCadence = 0.5f;
        private const float FastestCadence = 3f;

        public const int StillTicksBeforeStopping = 2;

        public static Gait Held(Gait measured, Gait previous, int stillTicks)
        {
            bool stoppedJustNow = measured.Speed < Running && previous.Speed >= Running;
            return stoppedJustNow && stillTicks <= StillTicksBeforeStopping ? previous : measured;
        }

        public static Gait For(PlayerState player, SubPos previous, MatchSettings settings, float runClipSpeed)
        {
            if (!player.Alive)
            {
                return new Gait(Still, RestingCadence);
            }

            int step = ClassicItems.Speed(player, settings);
            if (step <= 0 || runClipSpeed <= 0f)
            {
                return new Gait(Still, RestingCadence);
            }

            float dx = player.Position.X - previous.X;
            float dy = player.Position.Y - previous.Y;
            float movedInUnits = Mathf.Sqrt((dx * dx) + (dy * dy));

            if (movedInUnits / step < MovingAtAll)
            {
                return new Gait(Still, RestingCadence);
            }

            // One world unit per tile, so tiles per second is world units per second and
            // the ratio against the clip's own authored speed is the playback rate that
            // puts the feet where the ground is.
            float tilesPerSecond = movedInUnits * settings.TicksPerSecond / SubPos.UnitsPerTile;
            float cadence = Mathf.Clamp(tilesPerSecond / runClipSpeed, SlowestCadence, FastestCadence);

            return new Gait(Running, cadence);
        }
    }
}
