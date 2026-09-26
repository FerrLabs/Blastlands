using Blastlands.Core;
using Blastlands.Runtime;
using NUnit.Framework;

namespace Blastlands.Runtime.Tests
{
    public class PlayerPaceTests
    {
        private static readonly Gait Run = new Gait(PlayerPace.Running, 1.2f);
        private static readonly Gait Stop = new Gait(PlayerPace.Still, PlayerPace.RestingCadence);

        [Test]
        public void OneTickWithoutMovementDoesNotDropARunIntoIdle()
        {
            Assert.That(PlayerPace.Held(Stop, Run, 1).Speed, Is.EqualTo(PlayerPace.Running));
            Assert.That(PlayerPace.Held(Stop, Run, PlayerPace.StillTicksBeforeStopping).Speed, Is.EqualTo(PlayerPace.Running));
        }

        [Test]
        public void StandingStillLongerThanThatStops()
        {
            Assert.That(PlayerPace.Held(Stop, Run, PlayerPace.StillTicksBeforeStopping + 1).Speed, Is.EqualTo(PlayerPace.Still));
        }

        [Test]
        public void StartingToMoveIsNeverHeldBack()
        {
            Assert.That(PlayerPace.Held(Run, Stop, 0).Speed, Is.EqualTo(PlayerPace.Running));
        }

        // The band Player.controller runs in, read off its own transitions: Run above
        // 0.5, back to Idle below 0.25. Every test here is about which clip the number
        // selects, so asserting the number on its own would say nothing.
        private const float IdleBelow = 0.25f;
        private const float RunAbove = 0.5f;

        // The Run clip's authored ground speed, from its root motion.
        private const float RunClip = 2.6f;

        private static readonly MatchSettings Settings = MatchSettings.Default;

        private static PlayerState At(int x, int y)
        {
            return new PlayerState(0, new SubPos(x, y), Settings);
        }

        private static PlayerState MovingAt(int speedSteps)
        {
            PlayerState player = At(1000 + Settings.SpeedFor(speedSteps), 1000);
            player.SpeedSteps = speedSteps;
            return player;
        }

        private static SubPos Origin
        {
            get { return new SubPos(1000, 1000); }
        }

        [Test]
        public void APlayerWhoDidNotMoveIsIdle()
        {
            PlayerState player = At(1000, 1000);

            Assert.That(PlayerPace.For(player, player.Position, Settings, RunClip).Speed, Is.LessThan(IdleBelow));
        }

        [Test]
        public void ScrapingAlongAWallIsStillIdleRatherThanRunningOnTheSpot()
        {
            // Collision resolves a unit or two before it stops someone pushed into a
            // wall. Below a tenth of their own step that is not running, and animating
            // it as running leaves them sprinting against the wall for as long as the
            // key is held.
            PlayerState player = At(1000, 1000);
            var previous = new SubPos(999, 1000);

            Assert.That(PlayerPace.For(player, previous, Settings, RunClip).Speed, Is.LessThan(IdleBelow));
        }

        [Test]
        public void MovingAtAllRunsRatherThanWalking()
        {
            // Base speed is over three tiles a second, well past what any walk clip is
            // drawn for. Anything short of Run there is what makes a character look like
            // it is being dragged along the floor.
            Assert.That(PlayerPace.For(MovingAt(0), Origin, Settings, RunClip).Speed, Is.GreaterThan(RunAbove));
        }

        [Test]
        public void TheStrideCoversTheGroundTheSimulationMoved()
        {
            // The whole point of the cadence. At base speed the player covers 3.05 units
            // a second and the clip was drawn for 2.6, so it has to play faster than
            // authored or the character glides over the ground its feet never reach.
            float cadence = PlayerPace.For(MovingAt(0), Origin, Settings, RunClip).Cadence;
            float expected = Settings.SpeedFor(0) * Settings.TicksPerSecond / (float)SubPos.UnitsPerTile / RunClip;

            Assert.That(cadence, Is.EqualTo(expected).Within(0.001f));
            Assert.That(cadence, Is.GreaterThan(1f), "base speed plays the run clip at or below its authored rate");
        }

        [Test]
        public void EverySpeedPickupShowsInTheStride()
        {
            // Why the cadence is measured rather than banded: a player who has taken
            // pickups is genuinely faster, and the people they are chasing should be
            // able to see it.
            var seen = new System.Collections.Generic.List<float>();
            for (int steps = 0; steps <= Settings.MaxSpeedSteps; steps++)
            {
                seen.Add(PlayerPace.For(MovingAt(steps), Origin, Settings, RunClip).Cadence);
            }

            for (int i = 1; i < seen.Count; i++)
            {
                Assert.That(seen[i], Is.GreaterThan(seen[i - 1]), $"pickup {i} did not show in the stride");
            }
        }

        [Test]
        public void TheFastestLegalRunIsNeverClamped()
        {
            // The clamp is for dashes and shoves. A player who has taken every speed
            // pickup is simply running, and pinning their stride short of the ground
            // they cover brings back the skating the cadence exists to prevent.
            PlayerState player = MovingAt(Settings.MaxSpeedSteps);
            float cadence = PlayerPace.For(player, Origin, Settings, RunClip).Cadence;
            float expected = Settings.SpeedFor(Settings.MaxSpeedSteps) * Settings.TicksPerSecond / (float)SubPos.UnitsPerTile / RunClip;

            Assert.That(cadence, Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void ADeadPlayerIsIdleRatherThanRunningWhereTheyFell()
        {
            PlayerState player = MovingAt(0);
            player.Alive = false;

            Gait gait = PlayerPace.For(player, Origin, Settings, RunClip);

            Assert.That(gait.Speed, Is.EqualTo(PlayerPace.Still));
            Assert.That(gait.Cadence, Is.EqualTo(PlayerPace.RestingCadence));
        }

        [Test]
        public void ADashDoesNotBlurTheLegsIntoNonsense()
        {
            // A dash crosses more ground in a tick than any stride accounts for. The
            // clamp is what keeps it from spinning the clip into a smear.
            PlayerState player = At(1000 + (Settings.SpeedFor(0) * 8), 1000);

            float cadence = PlayerPace.For(player, Origin, Settings, RunClip).Cadence;

            Assert.That(cadence, Is.LessThanOrEqualTo(3f));
            Assert.That(cadence, Is.GreaterThan(1f), "a dash played no faster than a walk");
        }

        [Test]
        public void MovementIsMeasuredOnBothAxesNotJustOne()
        {
            // A diagonal covers ground on two axes at once. Measuring one of them makes
            // a player moving diagonally read as slower than they are, and their stride
            // would drag behind them for as long as they held the diagonal.
            int step = Settings.SpeedFor(0);
            PlayerState straight = At(1000 + step, 1000);
            PlayerState diagonal = At(1000 + step, 1000 + step);

            Assert.That(
                PlayerPace.For(diagonal, Origin, Settings, RunClip).Cadence,
                Is.GreaterThan(PlayerPace.For(straight, Origin, Settings, RunClip).Cadence),
                "a diagonal covered more ground and did not stride any faster for it");
        }

        [Test]
        public void AnAnimationPackWithNoMeasuredRunLeavesTheClipAlone()
        {
            // runClipSpeed is a serialized number describing an asset. Zero means nobody
            // measured it, and dividing by it would send the animator to infinity.
            Gait gait = PlayerPace.For(MovingAt(0), Origin, Settings, 0f);

            Assert.That(gait.Cadence, Is.EqualTo(PlayerPace.RestingCadence));
        }
    }
}
