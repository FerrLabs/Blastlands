using Blastlands.Core;
using NUnit.Framework;
using UnityEngine;

namespace Blastlands.Runtime.Tests
{
    public class InputReadingTests
    {
        [Test]
        public void APressOnAFrameThatRunsNoTickReachesTheNextTickOnce()
        {
            var latch = new PressLatch();

            latch.Note(true);
            latch.Note(false);

            Assert.That(latch.Take(), Is.True);
            Assert.That(latch.Take(), Is.False);
        }

        [Test]
        public void PushingTheStickUpMovesTowardTheTopRow()
        {
            int moveX, moveY;
            MoveReader.Resolve(Vector2.zero, new Vector2(0f, 0.9f), out moveX, out moveY);

            Assert.That(moveX, Is.EqualTo(0));
            Assert.That(moveY, Is.EqualTo(-900));
        }

        [Test]
        public void TheStickIsPassedThroughRatherThanSnapped()
        {
            int moveX, moveY;
            MoveReader.Resolve(Vector2.zero, new Vector2(0.6f, -0.5f), out moveX, out moveY);

            Assert.That(moveX, Is.EqualTo(600));
            Assert.That(moveY, Is.EqualTo(500));
        }

        [Test]
        public void AStickInsideTheDeadzoneDoesNotMove()
        {
            int moveX, moveY;
            MoveReader.Resolve(Vector2.zero, new Vector2(0.2f, 0.3f), out moveX, out moveY);

            Assert.That(moveX, Is.EqualTo(0));
            Assert.That(moveY, Is.EqualTo(0));
        }

        [Test]
        public void AStepWinsOverTheStickAtFullStrength()
        {
            int moveX, moveY;
            MoveReader.Resolve(new Vector2(-1f, 0f), new Vector2(0f, 0.9f), out moveX, out moveY);

            Assert.That(moveX, Is.EqualTo(-StickReader.Range));
            Assert.That(moveY, Is.EqualTo(0));
        }

        [Test]
        public void TwoHeldKeysResolveToOneDirection()
        {
            int moveX, moveY;
            MoveReader.Resolve(new Vector2(1f, 1f), Vector2.zero, out moveX, out moveY);

            Assert.That(moveX, Is.EqualTo(StickReader.Range));
            Assert.That(moveY, Is.EqualTo(0));
        }
    }
}
