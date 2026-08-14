using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class StickReaderTests
    {
        [Test]
        public void ACentredStickProducesNoMovement()
        {
            Assert.That(StickReader.ToDirection(0, 0), Is.EqualTo(Direction.None));
        }

        [Test]
        public void DriftInsideTheDeadzoneIsIgnored()
        {
            // Worn sticks rest slightly off centre; without this the player creeps.
            int inside = StickReader.DefaultDeadzone - 1;

            Assert.That(StickReader.ToDirection(inside, inside), Is.EqualTo(Direction.None));
            Assert.That(StickReader.ToDirection(-inside, -inside), Is.EqualTo(Direction.None));
        }

        [Test]
        public void EachCardinalPushResolvesToItsDirection()
        {
            Assert.That(StickReader.ToDirection(StickReader.Range, 0), Is.EqualTo(Direction.Right));
            Assert.That(StickReader.ToDirection(-StickReader.Range, 0), Is.EqualTo(Direction.Left));
            Assert.That(StickReader.ToDirection(0, StickReader.Range), Is.EqualTo(Direction.Down));
            Assert.That(StickReader.ToDirection(0, -StickReader.Range), Is.EqualTo(Direction.Up));
        }

        [Test]
        public void TheDominantAxisWins()
        {
            Assert.That(StickReader.ToDirection(900, 500), Is.EqualTo(Direction.Right));
            Assert.That(StickReader.ToDirection(500, 900), Is.EqualTo(Direction.Down));
            Assert.That(StickReader.ToDirection(-900, 500), Is.EqualTo(Direction.Left));
            Assert.That(StickReader.ToDirection(500, -900), Is.EqualTo(Direction.Up));
        }

        [Test]
        public void AnExactDiagonalAlwaysResolvesTheSameWay()
        {
            // The value matters less than the determinism: a tie that flips between
            // frames makes the player judder on the spot.
            Assert.That(StickReader.ToDirection(700, 700), Is.EqualTo(Direction.Right));
            Assert.That(StickReader.ToDirection(700, 700), Is.EqualTo(StickReader.ToDirection(700, 700)));
        }

        [Test]
        public void OneAxisPastTheDeadzoneIsEnoughEvenIfTheOtherIsNot()
        {
            int outside = StickReader.DefaultDeadzone + 1;

            Assert.That(StickReader.ToDirection(outside, 10), Is.EqualTo(Direction.Right));
            Assert.That(StickReader.ToDirection(10, -outside), Is.EqualTo(Direction.Up));
        }

        [Test]
        public void ACustomDeadzoneIsRespected()
        {
            Assert.That(StickReader.ToDirection(300, 0, 200), Is.EqualTo(Direction.Right));
            Assert.That(StickReader.ToDirection(300, 0, 500), Is.EqualTo(Direction.None));
        }
    }
}
