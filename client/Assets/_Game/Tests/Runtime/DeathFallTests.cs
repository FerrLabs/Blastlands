using Blastlands.Core;
using NUnit.Framework;
using UnityEngine;

namespace Blastlands.Runtime.Tests
{
    public class DeathFallTests
    {
        private const float North = 0f;
        private const float East = 90f;
        private const float South = 180f;
        private const float West = 270f;

        private static readonly GridPos Bomb = new GridPos(7, 7);

        private static MatchState Arena()
        {
            return new MatchState(new Arena(15, 15), MatchSettings.Default, 6u);
        }

        private static MatchState CrossAround(GridPos bomb, int range)
        {
            MatchState state = Arena();
            state.AddFlame(bomb, 10, 0);
            for (int step = 1; step <= range; step++)
            {
                state.AddFlame(new GridPos(bomb.X + step, bomb.Y), 10, 0);
                state.AddFlame(new GridPos(bomb.X - step, bomb.Y), 10, 0);
                state.AddFlame(new GridPos(bomb.X, bomb.Y + step), 10, 0);
                state.AddFlame(new GridPos(bomb.X, bomb.Y - step), 10, 0);
            }

            return state;
        }

        private static Vector3 WorldStep(int dx, int dy)
        {
            return MatchView.ToWorld(new GridPos(dx, dy), 0f) - MatchView.ToWorld(new GridPos(0, 0), 0f);
        }

        [Test]
        public void SomebodyOnAnArmIsThrownAwayFromTheBomb()
        {
            MatchState state = CrossAround(Bomb, 3);

            Vector3 east = DeathFall.PushAt(state, new GridPos(Bomb.X + 2, Bomb.Y));
            Vector3 north = DeathFall.PushAt(state, new GridPos(Bomb.X, Bomb.Y - 2));

            Assert.That(Vector3.Angle(east, WorldStep(1, 0)), Is.LessThan(1f));
            Assert.That(Vector3.Angle(north, WorldStep(0, -1)), Is.LessThan(1f));
        }

        [Test]
        public void TheEndOfAnArmStillFallsOutwards()
        {
            MatchState state = CrossAround(Bomb, 3);

            Vector3 push = DeathFall.PushAt(state, new GridPos(Bomb.X + 3, Bomb.Y));

            Assert.That(Vector3.Angle(push, WorldStep(1, 0)), Is.LessThan(1f));
        }

        [Test]
        public void StandingOnTheBombHasNoSideToFallTowards()
        {
            MatchState state = CrossAround(Bomb, 3);

            Vector3 push = DeathFall.PushAt(state, Bomb);

            Assert.That(push.sqrMagnitude, Is.LessThan(0.0001f));
            Assert.That(DeathFall.StateFor(push, North), Is.EqualTo(DeathFall.FallBackward));
        }

        [Test]
        public void FireInAnotherRowDoesNotPush()
        {
            MatchState state = Arena();
            GridPos victim = new GridPos(5, 5);
            state.AddFlame(victim, 10, 0);
            state.AddFlame(new GridPos(4, 4), 10, 0);
            state.AddFlame(new GridPos(6, 6), 10, 0);
            state.AddFlame(new GridPos(4, 5), 10, 0);

            Vector3 push = DeathFall.PushAt(state, victim);

            Assert.That(Vector3.Angle(push, WorldStep(1, 0)), Is.LessThan(1f));
        }

        [Test]
        public void SomebodyBittenFallsAwayFromTheZombie()
        {
            MatchState state = Arena();
            GridPos victim = new GridPos(5, 5);
            state.AddZombie(SubPos.AtTileCentre(new GridPos(4, 5)));

            Vector3 push = DeathFall.PushAt(state, victim);

            Assert.That(Vector3.Angle(push, WorldStep(1, 0)), Is.LessThan(1f));
        }

        [Test]
        public void AZombieAcrossTheArenaDidNotDoIt()
        {
            MatchState state = Arena();
            state.AddZombie(SubPos.AtTileCentre(new GridPos(12, 12)));

            Vector3 push = DeathFall.PushAt(state, new GridPos(2, 2));

            Assert.That(push, Is.EqualTo(Vector3.zero));
        }

        [TestCase(East, 1, 0, "FallForward")]
        [TestCase(West, 1, 0, "FallBackward")]
        [TestCase(North, 1, 0, "FallRight")]
        [TestCase(South, 1, 0, "FallLeft")]
        [TestCase(North, 0, -1, "FallForward")]
        [TestCase(East, 0, -1, "FallLeft")]
        public void TheFallIsTakenRelativeToWhereTheyWereFacing(float heading, int dx, int dy, string expected)
        {
            int state = DeathFall.StateFor(WorldStep(dx, dy), heading);

            Assert.That(state, Is.EqualTo(Animator.StringToHash(expected)));
        }
    }
}
