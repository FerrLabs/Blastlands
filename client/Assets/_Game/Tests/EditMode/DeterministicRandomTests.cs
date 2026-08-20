using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class DeterministicRandomTests
    {
        [Test]
        public void NextUInt_ProducesTheSameSequenceForTheSameSeed()
        {
            var left = new DeterministicRandom(12345u);
            var right = new DeterministicRandom(12345u);

            for (int i = 0; i < 256; i++)
            {
                Assert.That(left.NextUInt(), Is.EqualTo(right.NextUInt()), $"diverged at draw {i}");
            }
        }

        [Test]
        public void NextUInt_ProducesDifferentSequencesForDifferentSeeds()
        {
            var left = new DeterministicRandom(1u);
            var right = new DeterministicRandom(2u);

            Assert.That(left.NextUInt(), Is.Not.EqualTo(right.NextUInt()));
        }

        [Test]
        public void NextUInt_NeverCollapsesToZeroWhenSeededWithZero()
        {
            var random = new DeterministicRandom(0u);

            for (int i = 0; i < 1024; i++)
            {
                Assert.That(random.NextUInt(), Is.Not.EqualTo(0u), $"collapsed at draw {i}");
            }
        }

        [Test]
        public void NextInt_StaysWithinTheRequestedBound()
        {
            var random = new DeterministicRandom(777u);

            for (int i = 0; i < 4096; i++)
            {
                int value = random.NextInt(100);
                Assert.That(value, Is.InRange(0, 99));
            }
        }

        [Test]
        public void NextInt_WithBoundOfOne_AlwaysReturnsZero()
        {
            var random = new DeterministicRandom(3u);

            for (int i = 0; i < 64; i++)
            {
                Assert.That(random.NextInt(1), Is.EqualTo(0));
            }
        }

        [Test]
        public void NextInt_CoversTheWholeRange()
        {
            var random = new DeterministicRandom(555u);
            var seen = new HashSet<int>();

            for (int i = 0; i < 2000; i++)
            {
                seen.Add(random.NextInt(6));
            }

            Assert.That(seen.Count, Is.EqualTo(6));
        }

        [Test]
        public void NextInt_RejectsANonPositiveBound()
        {
            var random = new DeterministicRandom(1u);

            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(-5));
        }
    }
}
